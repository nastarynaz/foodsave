namespace FoodSave.Models;

public sealed class Recipe
{
    public string ApiID { get; init; } = "";
    public string NamaResep { get; init; } = "";
    public string ImageURL { get; init; } = "";
    public string Instructions { get; init; } = "";
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MatchedIngredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingIngredients { get; init; } = Array.Empty<string>();
    public string MatchText => $"{MatchedIngredients.Count} bahan cocok • {MissingIngredients.Count} bahan tambahan";
}
