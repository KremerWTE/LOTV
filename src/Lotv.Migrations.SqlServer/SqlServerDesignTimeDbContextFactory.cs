using Lotv.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lotv.Migrations.SqlServer;

/// <summary>
/// Used only by `dotnet ef migrations add/remove` when scaffolding against
/// this project (--project src/Lotv.Migrations.SqlServer). The connection
/// string here is never used to connect - EF only inspects the model and
/// provider to generate migration code. Real connections use the
/// DefaultConnection string configured in Lotv.Api at runtime.
/// </summary>
public class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LotvDbContext>
{
    public LotvDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LotvDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=LotvDesignTime;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsAssembly("Lotv.Migrations.SqlServer"));

        return new LotvDbContext(optionsBuilder.Options);
    }
}
