using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VeloStore.Models;

namespace VeloStore.Data
{
    public class VeloStoreDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public VeloStoreDbContext(DbContextOptions<VeloStoreDbContext> options)
            : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            builder.Entity<CartItem>()
                .Property(c => c.Price)
                .HasPrecision(18, 2);
        }
    }
}
