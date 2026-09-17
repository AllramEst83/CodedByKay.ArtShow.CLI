using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CodedByKay.ArtShow.CLI.Data;

public class ArtworkDbContext(DbContextOptions<ArtworkDbContext> options) : DbContext(options)
{
    public DbSet<Artwork> Artworks => Set<Artwork>();
    public DbSet<Classifier> Classifiers => Set<Classifier>();
    public DbSet<PublishState> PublishStates => Set<PublishState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Artwork>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Tags)
                .HasConversion(
                    tags => JsonSerializer.Serialize(tags, JsonOptions),
                    raw => JsonSerializer.Deserialize<List<string>>(raw, JsonOptions) ?? new())
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(a => a.Groups)
                .HasConversion(
                    groups => JsonSerializer.Serialize(groups, JsonOptions),
                    raw => JsonSerializer.Deserialize<List<string>>(raw, JsonOptions) ?? new())
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<Classifier>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.Kind, c.Name }).IsUnique();
        });

        modelBuilder.Entity<PublishState>(entity => entity.HasKey(s => s.Id));
    }

    private static readonly JsonSerializerOptions JsonOptions = new();
}
