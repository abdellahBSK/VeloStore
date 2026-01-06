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

    }
}
