using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VeloStore.Data
{
    /// <summary>
    /// Used by the <c>dotnet ef</c> tools to create the DbContext at design time
    /// (for example when adding a migration). Reads the same settings as the app.
    /// </summary>
    public class VeloStoreDbContextFactory : IDesignTimeDbContextFactory<VeloStoreDbContext>
    {
        public VeloStoreDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddUserSecrets<VeloStoreDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<VeloStoreDbContext>();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

            return new VeloStoreDbContext(optionsBuilder.Options);
        }
    }
}
