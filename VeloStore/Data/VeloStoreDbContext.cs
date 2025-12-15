using Microsoft.EntityFrameworkCore;
using VeloStore.Models;

namespace VeloStore.Data
{
    public class VeloStoreDbContext : DbContext
    {
        public VeloStoreDbContext(DbContextOptions<VeloStoreDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
    }
}
