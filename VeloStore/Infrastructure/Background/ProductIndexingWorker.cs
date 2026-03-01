using VeloStore.Domain.Constants;
using VeloStore.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VeloStore.Data;
using Microsoft.EntityFrameworkCore;

namespace VeloStore.Infrastructure.Background;

public class ProductIndexingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductIndexingWorker> _logger;

    public ProductIndexingWorker(IServiceProvider serviceProvider, ILogger<ProductIndexingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Product Indexing Worker started.");

        // Wait to allow app startup to settle
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<VeloStoreDbContext>();
                var vectorRepo = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                var indexedProductIds = await vectorRepo.GetIndexedProductIdsAsync(EmbeddingConstants.CurrentVersion, stoppingToken);
                var allProducts = await dbContext.Products.Include(p => p.Category).ToListAsync(stoppingToken);
                var missingProducts = allProducts.Where(p => !indexedProductIds.Contains(p.Id)).ToList();

                if (missingProducts.Any())
                {
                    _logger.LogInformation("Found {Count} products requiring indexing for version {Version}.", missingProducts.Count, EmbeddingConstants.CurrentVersion);
                    
                    foreach (var product in missingProducts)
                    {
                        var categoryName = product.Category?.Name ?? "Uncategorized";
                        var textToEmbed = $"Product: {product.Name}\nCategory: {categoryName}\nPrice: {product.Price:C}\nDescription: {product.Description}";
                        
                        var vector = await embeddingService.GenerateEmbeddingAsync(textToEmbed, stoppingToken);
                        
                        if (vector != null && vector.Length > 0)
                        {
                            await vectorRepo.UpsertProductEmbeddingAsync(
                                product.Id, 
                                product.Name, 
                                product.Description, 
                                product.Price, 
                                categoryName, 
                                vector, 
                                EmbeddingConstants.CurrentVersion, 
                                stoppingToken);
                                
                            _logger.LogInformation("Successfully indexed product {ProductId}: {ProductName}", product.Id, product.Name);
                        }
                    }
                }
                
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Product Indexing Worker is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Product Indexing Worker.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Backoff on error
            }
        }
    }
}
