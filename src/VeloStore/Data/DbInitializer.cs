using VeloStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace VeloStore.Data
{
    public static class DbInitializer
    {
        public static void Seed(IApplicationBuilder applicationBuilder)
        {
            using (var serviceScope = applicationBuilder.ApplicationServices.CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<VeloStoreDbContext>();

                if (context == null) return;

                // Apply EF Core migrations (creates the database on first run).
                // Non-relational providers, such as the in-memory one used in tests, have no migrations.
                if (context.Database.IsRelational())
                {
                    context.Database.Migrate();
                }
                else
                {
                    context.Database.EnsureCreated();
                }

                // Seed Categories
                if (!context.Categories.Any())
                {
                    context.Categories.AddRange(
                        new Category { Name = "Mountain Bikes" },
                        new Category { Name = "Road Bikes" },
                        new Category { Name = "Gravel Bikes" },
                        new Category { Name = "Electric Bikes" },
                        new Category { Name = "Accessories" }
                    );
                    context.SaveChanges();
                }

                // Seed Products
                if (!context.Products.Any())
                {
                    var mtb = context.Categories.FirstOrDefault(c => c.Name == "Mountain Bikes");
                    var road = context.Categories.FirstOrDefault(c => c.Name == "Road Bikes");
                    var gravel = context.Categories.FirstOrDefault(c => c.Name == "Gravel Bikes");
                    var ebikes = context.Categories.FirstOrDefault(c => c.Name == "Electric Bikes");
                    var acc = context.Categories.FirstOrDefault(c => c.Name == "Accessories");

                    context.Products.AddRange(
                        new Product
                        {
                            Name = "Trail Blazer 5000",
                            Description = "A rugged mountain bike for the toughest trails.",
                            Price = 1299.99m,
                            Stock = 15,
                            ImageUrl = "/images/products/trail-blazer.jpg",
                            CategoryId = mtb.Id
                        },
                        new Product
                        {
                            Name = "Speed Demon XR",
                            Description = "Lightweight carbon fiber road bike for maximum speed.",
                            Price = 2499.99m,
                            Stock = 8,
                            ImageUrl = "/images/products/speed-demon.jpg",
                            CategoryId = road.Id
                        },
                        new Product
                        {
                            Name = "Gravel Grinder Pro",
                            Description = "Versatile gravel bike for any surface.",
                            Price = 1799.50m,
                            Stock = 12,
                            ImageUrl = "/images/products/gravel-grinder.jpg",
                            CategoryId = gravel.Id
                        },
                        new Product
                        {
                            Name = "Volt Cruiser E-Bike",
                            Description = "Effortless commuting with our powerful electric motor.",
                            Price = 3200.00m,
                            Stock = 5,
                            ImageUrl = "/images/products/volt-cruiser.jpg",
                            CategoryId = ebikes.Id
                        },
                        new Product
                        {
                            Name = "Aero Helmet V2",
                            Description = "Aerodynamic helmet for serious racers.",
                            Price = 149.99m,
                            Stock = 50,
                            ImageUrl = "/images/products/helmet.jpg",
                            CategoryId = acc.Id
                        },
                        new Product
                        {
                            Name = "Premium Padded Gloves",
                            Description = "Comfortable gloves for long rides.",
                            Price = 29.99m,
                            Stock = 100,
                            ImageUrl = "/images/products/gloves.jpg",
                            CategoryId = acc.Id
                        }
                    );
                    context.SaveChanges();
                }
            }
        }
    }
}
