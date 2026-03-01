using VeloStore.ViewModels;

namespace VeloStore.Application.Models;

public record ProductContext(
    ProductDetailsVM Product,
    double Score
);
