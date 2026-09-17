using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodedByKay.ArtShow.CLI.Data;

/// <summary>
/// Design-time-only factory so `dotnet ef` can construct <see cref="ArtworkDbContext"/> — this
/// app has no DI container (it builds <see cref="DbContextOptions{TContext}"/> from config
/// itself in Program.cs), which is the normal case `dotnet ef` needs this for. The connection
/// string here only matters for design-time model comparison, not runtime — Program.cs's own
/// `Catalog:DbPath` config is still what the running app actually connects to.
/// </summary>
public class ArtworkDbContextFactory : IDesignTimeDbContextFactory<ArtworkDbContext>
{
    public ArtworkDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ArtworkDbContext>()
            .UseSqlite("Data Source=Db/artwork.db")
            .Options;
        return new ArtworkDbContext(options);
    }
}
