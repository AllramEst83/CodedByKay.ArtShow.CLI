# CodedByKay.ArtShow.CLI — Agent Guide

## What this is

A .NET console tool run manually from Kay's machine — **not a hosted
service**. It is the sole writer of artwork metadata and media for the
ArtShow site. Full architecture and rationale live in
`CodedByKay.ArtShow/plan.md` (sibling repo) — read that first; this file
covers only what's specific to this tool's implementation.

## Responsibilities

1. Own the SQLite catalog (`Db\\artwork.db`) — the single source of truth for
   artwork metadata.
2. Upload new images to Cloudflare R2, generating a thumbnail at upload
   time (no on-demand image CDN — see plan.md for why).
3. Record video metadata for clips Kay uploads to YouTube himself — this
   tool never uploads video anywhere, it just stores the YouTube video id.
4. Publish a static JSON file into the sibling `CodedByKay.ArtShow` repo,
   which Netlify then serves as-is.

This tool never runs as a server and the public site never calls it live —
publishing is a deliberate, manual step (run CLI → review diff → commit →
push → Netlify deploys).

## Interface: interactive TUI, not flags/args

**Decided 2026-09-10** — this is not a `--flag`-driven CLI. It's an
interactive terminal app built on **Spectre.Console**: launch it with no
arguments, it shows a menu (`AnsiConsole.Prompt` with `SelectionPrompt`),
and each action walks Kay through prompts (text input, confirmations,
picking an existing artwork from a list) rather than requiring
memorized command-line syntax. This was chosen over `System.CommandLine`
specifically because the workflow is manual/occasional (add a piece,
publish later) and a menu is easier to pick up cold than remembering flags.
`System.CommandLine` was added then removed during scaffolding — don't
re-add it, this is a deliberate choice, not an oversight.

## Menu actions

| Action | Does | Touches R2? |
|---|---|---|
| Add artwork | Prompts for type (Image/Video) first. **Image**: file path + metadata, generates a thumbnail (SkiaSharp), uploads original + thumbnail to R2. **Video**: a YouTube video URL/id (you upload the clip yourself via YouTube Studio first — this tool never uploads video anywhere) + metadata. No file, no R2 touch for video rows. Category/Medium/Tags/Groups are picked from known values (see "Classifiers" below), not retyped. | Yes (images only) |
| Edit artwork | Lists existing artworks, prompts which to edit, updates metadata fields on that SQLite row (plus the YouTube id, if it's a video row). No file re-upload. | No |
| View artwork | Straight to a searchable artwork picker (no R2 round trip), then renders **every** field plus its derived `imageUrl`/`thumbnailUrl`/`videoUrl` in one bordered table — a read-only look at one full record, no SQL needed. Loops ("View another?") until you say no. | No |
| Manage classifiers | Add/rename/delete known Category, Medium, Tag, and Group values (see "Classifiers" below). Not required for day-to-day use — Add/Edit already remembers a newly typed value automatically. This is for cleanup: fixing a typo, merging near-duplicates, retiring one you don't want offered anymore. | No |
| Publish | Reads all SQLite rows, derives URLs from config, writes the static JSON into the site repo. Shows as **"Publish (pending changes)"** in the main menu whenever the catalog has changed since the last successful publish (see "Pending-publish tracking" below). | **No** — metadata-only, safe to re-run any time |
| Status / sync check | Lists everything in the R2 bucket, cross-references it against the SQLite catalog: per-row table (original/thumbnail present in R2? YouTube id set?), paginated (↑/↓/PageUp/PageDown, any other key to finish — Spectre's `Table` has no scrolling of its own, this is hand-rolled so the view stays usable as the catalog grows), plus a summary of missing keys (catalog references a key R2 doesn't have) and orphaned objects (in R2, not referenced by any row — e.g. the old test uploads). Read-only, and self-contained — no detail drill-down here, that's "View artwork". | Read-only (R2) |

**Invariant (within this CLI):** only Add artwork ever writes to R2. Publish
and Status are pure reads and can be re-run as many times as needed without
side effects. There's no import flow anymore — the one-time legacy migration
is done and its code was removed (see "Known local paths" below).

**This CLI is no longer the only writer, workspace-wide.** As of the
2026-09-17 rebuild, `CodedByKay.ArtShow.Mcp` (sibling repo) does the full
add/edit/delete/publish workflow too — its own R2 client (Node's
`@aws-sdk/client-s3`, not this CLI's `AWSSDK.S3`), its own imaging pipeline
(`sharp`, not `SkiaSharp`), and its own `data/artwork.json` writer built to
byte-match this CLI's `System.Text.Json` output (see that repo's
`src/publishing/dotnet-json-encoder.ts` and its README's "Publish parity").
Both talk to the same `Db/artwork.db` in WAL mode (safe for concurrent
readers + a writer) and read R2 credentials from the same dotnet
user-secrets store this CLI uses — no separate credential setup. If you
change this CLI's imaging quality settings, R2 key layout, or
`ArtworkJsonItem`'s shape/property order, the MCP's equivalents need
updating in the same change or the two tools drift. Full rationale:
`ArtShow.Workspace/working-on/mcp-rebuild-plan.md`.

## Classifiers — "dropdown" pickers for Category/Medium/Tags/Groups

**Decided 2026-09-11.** These four fields are backed by a `Classifier` table
(`Data/Classifier.cs`: `Kind` + `Name`, unique together — `Kind` is one of
`ClassifierKind.Category/Medium/Tag/Group`), not free re-typed text and not
a static taxonomy file. `Menus/CatalogPicker.cs` is what `AddFlow`/`EditFlow`
call: Category/Medium get a `SelectionPrompt` (single choice, current value
pre-highlighted on Edit so Enter alone keeps it, plus "enter a new value"
and "none"), Tags/Groups get a `MultiSelectionPrompt` (space to toggle,
existing selections pre-checked on Edit) followed by a free-text field for
anything new. **Typing a brand-new value auto-adds it to `Classifiers`** —
that's what makes this self-maintaining: you never have to visit "Manage
classifiers" just to make a value available later, only to clean one up.

`Data/ClassifierSeeder.cs` runs once at startup (`Program.cs`, right after
`db.Database.MigrateAsync()`) and backfills `Classifiers` from whatever's
already on existing `Artwork` rows — a no-op once the table has any rows, so it only
ever does real work the first run after this feature shipped.

`Menus/ManageClassifiersFlow.cs` (the "Manage classifiers" menu action)
handles rename/delete, and **cascades into every `Artwork` row that uses
the old value** — a rename rewrites `Category`/`Medium`/each matching entry
in `Tags`/`Groups`; a delete on a still-used value asks for confirmation
first (shows the affected count) before doing the same. Don't add a
rename/delete path that only touches the `Classifier` row itself — that
would silently desync the catalog from what "Manage classifiers" shows.

## Pending-publish tracking

**Decided 2026-09-11.** `Data/PublishState.cs` is a singleton row (`Id = 1`)
with one `Dirty` bool — `Data/PublishStateStore.cs` is the only thing that
reads/writes it (`IsDirtyAsync`/`MarkDirtyAsync`/`MarkPublishedAsync`).
`Program.cs` checks it fresh on every trip through the main menu loop and
relabels the choice **"Publish (pending changes)"** when true — that's the
whole feature; there's no other UI for it.

**Call `MarkDirtyAsync` from anywhere that changes what `Publish` would
write** — currently: `AddFlow` (after inserting), `EditFlow` (after
saving), and `ManageClassifiersFlow`'s rename/delete *only when they
actually touched an `Artwork` row* (a rename that matched nothing, or
adding/renaming an unused classifier, correctly leaves it alone — no point
flagging "pending changes" for an edit that wouldn't change the published
JSON). `PublishFlow` calls `MarkPublishedAsync` after a successful write.
If you add a new way to mutate `Artwork` rows, mark it dirty there too —
this is easy to silently forget since nothing else depends on `Dirty` being
correct except the menu label.

## Fullscreen (alternate screen buffer)

**Decided 2026-09-11.** `Terminal/AlternateScreen.cs`, entered via
`using var alternateScreen = new AlternateScreen();` at the very top of
`Program.cs`, wraps the whole session in the terminal's alternate screen
buffer (`ESC[?1049h` / `ESC[?1049l` — the same mechanism `vim`/`htop`/`less`
use) so the interactive session doesn't scroll through prior shell history,
and whatever was on screen before is restored exactly on exit.

**Guarded by `Console.IsOutputRedirected`** — skips entirely (both the
escape codes and `Console.Clear()`) when output isn't a real console (piped
to a file, non-interactive/CI context). This isn't optional polish: if
`Console.Clear()` throws in that situation (it does — `IOException: the
handle is invalid`) *inside the constructor*, `using` never gets to run
`Dispose()`, and the enter sequence would already be written with no
matching exit sequence — the real terminal would be stuck in the alternate
buffer. Hit this exact bug once already (`< /dev/null` smoke test) — don't
remove the guard while "simplifying" this class.

**Ctrl+C needs its own handling** — `Console.CancelKeyPress` bypasses the
normal stack unwind that would otherwise run `using`'s `Dispose()`, so
`Program.cs` also subscribes `Console.CancelKeyPress += (_, _) =>
alternateScreen.Dispose();` explicitly. Don't drop this thinking `using`
alone covers it; it doesn't, for this specific exit path.

## Data model (SQLite via EF Core)

Mirrors the shape in `plan.md`, with one deliberate deviation:

```csharp
public class Artwork
{
    public int Id { get; set; } // SQLite-generated (rowid), not "art-NNN" strings
    public DateOnly AddedDate { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? CreatedDate { get; set; }
    public string? Category { get; set; }
    public string? Medium { get; set; }
    public string? Type { get; set; }          // null = image, "video" = video
    public string OriginalPath { get; set; }   // local path at import/add time; "youtube:<id>" for video rows
    public string? R2Key { get; set; }         // bucket-relative, e.g. originals/2026/foo.webp (images only)
    public string? ThumbR2Key { get; set; }    // bucket-relative thumbnail key (images only)
    public int? Width { get; set; }            // oriented original, px (images only; null until backfilled)
    public int? Height { get; set; }
    public string? DisplayR2Key { get; set; }  // ~2000px long-edge WebP; null when the original is already that small
    public string? VideoId { get; set; }       // YouTube video id (video rows only)
    public List<string> Tags { get; set; }
    public List<string> Groups { get; set; }
}
```

**Critical invariant:** `R2Key`/`ThumbR2Key`/`VideoId` are stored as
bucket-relative keys / a bare provider id — **never as full URLs**.
`publish` derives absolute URLs from config/convention at generation time:

```csharp
var imageUrl     = $"{options.R2PublicBaseUrl}/{artwork.R2Key}";
var thumbnailUrl = $"{options.R2PublicBaseUrl}/{artwork.ThumbR2Key}";
// Video rows instead:
var videoUrl     = $"https://www.youtube-nocookie.com/embed/{artwork.VideoId}";
var thumbnailUrl = $"https://img.youtube.com/vi/{artwork.VideoId}/hqdefault.jpg"; // YouTube's own thumbnail, no upload
```

If this were reversed (URLs stored per-row), changing the CDN domain later
would mean rewriting every row instead of one config value. Don't
reverse it.

**`Id` is a plain SQLite autoincrement `int`, decided 2026-09-10** — not the
legacy `"art-NNN"` string scheme. `Publishing/ArtworkJsonWriter.cs`
stringifies it for the JSON contract (`a.Id.ToString()`), but the value
itself is whatever SQLite assigns on insert.

## Known gotchas (already hit, already fixed — don't reintroduce)

- **R2 upload throws `STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not
  implemented`** — AWSSDK.S3 v4 defaults to a chunked upload with a trailing
  checksum, which R2 doesn't implement. The client-level
  `RequestChecksumCalculation`/`ResponseChecksumValidation = WHEN_REQUIRED`
  settings in `Storage/R2Client.cs` are necessary but **not sufficient** on
  their own (first fix attempt, still failed). The actual fix is two flags
  on the **request itself**, not the client config:
  `PutObjectRequest.DisablePayloadSigning = true` and
  `.DisableDefaultChecksumValidation = true` (also in `R2Client.cs`,
  `UploadAsync`). There is no `AmazonS3Config.UseChunkEncoding` in this SDK
  version — don't reach for that, it doesn't exist and won't compile.
- **`SixLabors.ImageSharp` v4+ requires a paid commercial license** (build
  fails with a `SixLaborsLicenseKey` error). Use `SkiaSharp` instead — see
  Key libraries below.
- **R2 credentials must never go in `appsettings.json`** — that file is
  read by the whole team of one (Kay) but is a plain-text project file. Use
  `dotnet user-secrets set "R2:AccessKeyId" "..."` /
  `"R2:SecretAccessKey"` instead (this already happened once by accident
  and was corrected 2026-09-10).

## Known local paths (Kay's machine)

| What | Path |
|---|---|
| Site repo root | `C:\Users\kaywi\dev\CodedByKay.ArtShow` |
| Site's published JSON (`publish` writes here) | `C:\Users\kaywi\dev\CodedByKay.ArtShow\data\artwork.json` |

`Publish:SiteRepoPath` (see Config below) defaults to the site repo root
above.

**One-time legacy migration is complete and its code has been removed.**
All 51 original images were uploaded to R2 via the old `import` flow and
confirmed live at `https://kaysartshow.fyi/...` (all image/thumbnail URLs
HEAD-checked 200 on 2026-09-11); `assets/drawings/` in the site repo has
since been deleted. There's nothing left to import — new artwork only ever
enters the catalog through "Add artwork" from here on.

## Config

Not committed — use `dotnet user-secrets` locally, environment variables
in CI if this is ever automated:

| Key | Purpose |
|---|---|
| `R2:AccountId`, `R2:AccessKeyId`, `R2:SecretAccessKey`, `R2:Bucket` | S3-compatible client credentials |
| `R2:PublicBaseUrl` | `https://kaysartshow.fyi` — the connected custom domain. **Never** the bucket's `r2.dev` URL (no caching, rate-limited; see plan.md) |
| `Catalog:DbPath` | Path to `Db\\artwork.db` |
| `Publish:SiteRepoPath` | Absolute path to the sibling `CodedByKay.ArtShow` clone; `publish` writes to `<SiteRepoPath>/data/artwork.json` |

## Key libraries

- `AWSSDK.S3` — R2 is S3-compatible; just point it at R2's endpoint URL.
- `SkiaSharp` — thumbnail generation (resize to 600w, encode WebP). **Not**
  `SixLabors.ImageSharp` — v4+ of that requires a paid commercial license
  (a build-time license-key error surfaced this); SkiaSharp is MIT-licensed
  and free, and encodes WebP natively (`SKImage.Encode(SKEncodedImageFormat.Webp, quality)`).
- `Microsoft.EntityFrameworkCore.Sqlite` + `.Design` — catalog + migrations.
- `Spectre.Console` — the TUI: menus, prompts, tables. See "Interface"
  section above — this replaced `System.CommandLine`.
- Target the current .NET LTS (.NET 10 as of this writing) — confirmed
  installed (`10.0.201` SDK) 2026-09-10.

## Suggested project layout

```
CodedByKay.ArtShow.CLI/
├── Program.cs         (menu loop)
├── Menus/              (AddFlow, EditFlow, ViewFlow, ManageClassifiersFlow, PublishFlow,
│                        StatusFlow, CatalogPicker, YouTubeIdParser — each flow drives its own
│                        Spectre prompts)
├── Data/              (ArtworkDbContext, Artwork, Classifier, ClassifierSeeder, PublishState,
│                        PublishStateStore, EF migrations)
├── Terminal/          (AlternateScreen — fullscreen/alternate-screen-buffer helper)
├── Storage/           (R2 client wrapper)
├── Imaging/           (thumbnail generation)
└── Publishing/        (JSON writer — single file today, shard by year later per plan.md)
```

## R2 key layout (decided)

```
originals/<year>/<slug>.webp
thumbs/<year>/<slug>-600w.webp
display/<year>/<slug>-2000w.webp   (only when the original's long edge is over 2000px)
```
Year-prefixed, not flat. `display/` is what the site's viewer shows by default (the
originals are scans up to ~7000px, multi-MB); the original is loaded only when the
visitor zooms in. 2000px / WebP q82 in both this CLI and the MCP (`DisplayLongEdge`
here, `DISPLAY_LONG_EDGE` there) — change both together.

## Video (YouTube) — decided 2026-09-11

Videos are hosted on YouTube, not R2 or a dedicated streaming provider
(Bunny Stream was the original plan — see plan.md history — but Kay decided
against it before it was ever set up). Kay uploads each clip himself via
YouTube Studio (unlisted or public); this tool never talks to YouTube's
API — "Add artwork" → Video just prompts for the resulting video URL/id
(parsed by `Menus/YouTubeIdParser.cs`) and stores it as `Artwork.VideoId`.
`publish` derives both the embed URL and the thumbnail from that id alone
(see "Data model" above) — no upload, no thumbnail generation, no R2 touch
for video rows at all.

The two videos from the old Bunny-era plan (`elephant_man_v1_video_2.mp4`,
`elephant_man_v2_video.mp4`) have gone through this: uploaded to YouTube and
added to the catalog (ids `FTVJG6KiknU`/`jr1o6kQvHpI`, artwork rows
`#52`/`#53`) — `ArtShow.Workspace/working-on/videos-to-upload/` (where they
were staged) can be deleted now that both are in. A third video (`#55`,
"flying organic spaceship sketch", id `9erwbRAsUuQ`) was added the same way
since, confirming the everyday Add-artwork-→-Video path works end to end.

## Open questions (resolve as they come up, don't guess silently)

- Does `publish` also `git commit`/push into the site repo, or does Kay do
  that manually after reviewing the diff? Default to **manual** until
  told otherwise — less surprising for a one-person workflow.
- JSON sharding threshold (single file vs. per-year) — not needed yet at
  53 items; revisit per `plan.md`'s note on this.

## Getting started

R2 credentials aren't committed anywhere — set them with `dotnet user-secrets`
before running Add artwork on an image (Edit and Publish don't need them):

```
dotnet user-secrets set "R2:AccessKeyId" "..."
dotnet user-secrets set "R2:SecretAccessKey" "..."
```

`R2:AccountId`, `R2:Bucket`, and `R2:PublicBaseUrl` are already filled in
`appsettings.json` (not secret — see `working-on/r2-api-docs.md`).

Then `dotnet run` from this folder — it shows a menu, no arguments needed.

## Current status

- [x] Project scaffolded (`dotnet new console`, targeting `net10.0`) —
      2026-09-10
- [x] Packages installed: `AWSSDK.S3`, `Microsoft.EntityFrameworkCore.Sqlite`
      + `.Design`, `SkiaSharp`, `Spectre.Console`, `Microsoft.Extensions.Configuration.*`
- [x] EF Core model (`Data/Artwork.cs`, `Data/Classifier.cs`,
      `Data/ArtworkDbContext.cs` — Tags/Groups stored as JSON columns via a
      value converter, not separate tables).
- [x] **Formal EF Core migrations adopted 2026-09-11** (`Data/Migrations/`) —
      `Program.cs` now calls `db.Database.MigrateAsync()` at startup, not
      `EnsureCreatedAsync`. Needed a design-time factory
      (`Data/ArtworkDbContextFactory.cs`) since this app has no DI container.
      The real `Db/artwork.db` already had an `Artworks` table from the old
      `EnsureCreatedAsync` days — the `InitialCreate` migration was applied
      to it by hand (create just the new `Classifiers` table/index + insert
      the `__EFMigrationsHistory` row, skip re-creating `Artworks`) instead
      of via `dotnet ef database update`, which would have tried to
      `CREATE TABLE Artworks` again and failed. **From here on, use the
      normal command** — `dotnet ef migrations add <Name> --output-dir
      Data/Migrations` then `dotnet ef database update` — this by-hand step
      was a one-time bootstrap, not the ongoing workflow.
- [x] R2 client (`Storage/R2Client.cs`) — thin wrapper over `AmazonS3Client`
      pointed at R2's S3-compatible endpoint.
- [x] Thumbnail/encode helper (`Imaging/ThumbnailGenerator.cs`) — SkiaSharp,
      resize to 600w + WebP for thumbnails, full-res WebP re-encode for new
      original uploads.
- [x] Menu loop (`Program.cs`) + `AddFlow.cs`, `EditFlow.cs`,
      `PublishFlow.cs`, `StatusFlow.cs` implemented and compiling clean.
      **Confirmed working end-to-end against real R2** — all 51 catalog
      images are live at `kaysartshow.fyi` (verified 2026-09-11).
- [x] Legacy `import` flow retired — its one-time job is done, code removed
      2026-09-11.
- [x] Video handling switched from Bunny Stream (never set up) to YouTube
      manual-upload-and-paste — see "Video (YouTube)" above. Both legacy
      videos uploaded and added to the catalog as of 2026-09-11.
- [x] Classifier "dropdown" pickers (`Menus/CatalogPicker.cs`,
      `Menus/ManageClassifiersFlow.cs`) — see "Classifiers" above.
- [x] `View artwork` menu action (`Menus/ViewFlow.cs`) — read-only full-record
      detail view.
- [x] Pending-publish indicator on the main menu (`Data/PublishState.cs`,
      `Data/PublishStateStore.cs`) — see "Pending-publish tracking" above.
- [x] Fullscreen TUI via the terminal's alternate screen buffer
      (`Terminal/AlternateScreen.cs`) — see "Fullscreen" above.

## Related

- `CodedByKay.ArtShow/plan.md` — full migration plan and rationale
- `CodedByKay.ArtShow` — the site repo this tool publishes into
- `CodedByKay.ArtShow.Mcp` — the MCP server that does this same workflow from
  an agent instead of this TUI; see its README and "Invariant" note above
- `ArtShow.Workspace/AGENTS.md` — cross-repo overview


## 2026-10 data-contract additions (gallery overhaul, Phase 4)

- **`artwork.json`** items gained `width`, `height`, `displayUrl`, `datePrecision`
  and `media[]`, appended after `thumbnailUrl` (`Publishing/ArtworkJsonItem.cs`).
  `media[]` is derived from `Medium` + tags (`Publishing/MediaDerivation.cs`);
  `datePrecision` is `"year"` for a January 1st, `"day"` otherwise. The same
  patterns live in the MCP's `src/publishing/derive.ts` and the site's
  `js/modules/data.js` fallback — keep all three identical.
- **`data/collections.json`** (`Publishing/CollectionsJsonWriter.cs`): one entry
  per Group classifier in use — `slug`, `name`, `blurb`, `coverId`, `order`.
  Presentation details live on the Group's `Classifier` row (`Blurb`,
  `CoverArtworkId`, `SortOrder`); edit them under *Manage classifiers → Group →
  Collection details*. All optional; the site has defaults.
- **Migration** `AddImageMetadataAndCollectionDetails` is additive/nullable.
  Rows added before it carry no size until the MCP's `backfill_image_metadata`
  has run (done for the live catalog 2026-10-14: 140 images, 127 display files).
- **`ArtShow.CLI --publish`** writes both JSON files and exits (no TUI) — same
  code path as the menu, and handy for byte-parity checks against the MCP.
- Add / Delete / Status / View all know about the display key. **Anything that
  decides what is "orphaned" must list `DisplayR2Key`**, or pruning deletes the
  viewer images.
