using Microsoft.EntityFrameworkCore;

namespace CodedByKay.ArtShow.CLI.Data;

public static class PublishStateStore
{
    private const int SingletonId = 1;

    public static async Task<bool> IsDirtyAsync(ArtworkDbContext db) =>
        (await db.PublishStates.FindAsync(SingletonId))?.Dirty ?? false;

    public static Task MarkDirtyAsync(ArtworkDbContext db) => SetAsync(db, dirty: true);

    public static Task MarkPublishedAsync(ArtworkDbContext db) => SetAsync(db, dirty: false);

    private static async Task SetAsync(ArtworkDbContext db, bool dirty)
    {
        var state = await db.PublishStates.FindAsync(SingletonId);
        if (state is null)
            db.PublishStates.Add(new PublishState { Id = SingletonId, Dirty = dirty });
        else
            state.Dirty = dirty;

        await db.SaveChangesAsync();
    }
}
