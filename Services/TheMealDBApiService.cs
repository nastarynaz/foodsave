using System.Net.Http;
using System.Text.Json;
using FoodSave.Models;

namespace FoodSave.Services;

public sealed class TheMealDBApiService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static string BaseUrl => "https://www.themealdb.com/api/json/v1/" +
        (Environment.GetEnvironmentVariable("FOODSAVE_MEALDB_KEY") ?? "1") + "/";
    private static readonly Dictionary<string, string> IngredientNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ayam"] = "chicken", ["dada ayam"] = "chicken breast", ["telur"] = "egg",
        ["kentang"] = "potato", ["tomat"] = "tomato", ["bawang putih"] = "garlic",
        ["bawang bombai"] = "onion", ["wortel"] = "carrot", ["susu"] = "milk",
        ["keju"] = "cheese", ["daging sapi"] = "beef", ["ikan"] = "fish",
        ["udang"] = "prawns", ["beras"] = "rice", ["nasi"] = "rice",
        ["jamur"] = "mushrooms", ["bayam"] = "spinach", ["brokoli"] = "broccoli"
    };

    public async Task<List<Recipe>> FetchRecipesAsync(IEnumerable<FoodItem> foods)
    {
        var available = foods.Where(f => f.Jumlah > 0).OrderBy(f => f.TanggalKedaluwarsa)
            .Select(f => Normalize(f.NamaMakanan)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (available.Count == 0) return new List<Recipe>();
        var ids = new HashSet<string>();
        foreach (var ingredient in available.Take(5))
        {
            using var document = await GetAsync("filter.php?i=" + Uri.EscapeDataString(ingredient.Replace(' ', '_')));
            if (document.RootElement.TryGetProperty("meals", out var meals) && meals.ValueKind == JsonValueKind.Array)
            {
                foreach (var meal in meals.EnumerateArray().Take(8))
                    if (meal.TryGetProperty("idMeal", out var id) && id.GetString() is { } value) ids.Add(value);
            }
        }
        var recipes = new List<Recipe>();
        foreach (var id in ids.Take(24))
        {
            var recipe = await FetchRecipeAsync(id, available);
            if (recipe != null) recipes.Add(recipe);
        }
        return recipes.OrderByDescending(r => r.MatchedIngredients.Count)
            .ThenBy(r => r.MissingIngredients.Count).ThenBy(r => r.NamaResep).ToList();
    }

    public async Task<Recipe?> FetchRecipeAsync(string id, IEnumerable<string> available)
    {
        if (!id.All(char.IsDigit) || id.Length > 30) return null;
        using var document = await GetAsync("lookup.php?i=" + Uri.EscapeDataString(id));
        if (!document.RootElement.TryGetProperty("meals", out var meals) || meals.ValueKind != JsonValueKind.Array || meals.GetArrayLength() == 0) return null;
        var meal = meals[0];
        var ingredients = new List<string>();
        for (var n = 1; n <= 20; n++)
        {
            var ingredient = Value(meal, "strIngredient" + n).Trim();
            if (ingredient.Length > 0) ingredients.Add(ingredient);
        }
        var stock = available.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matched = ingredients.Where(i => stock.Contains(Normalize(i))).ToList();
        var missing = ingredients.Except(matched, StringComparer.OrdinalIgnoreCase).ToList();
        return new Recipe
        {
            ApiID = Value(meal, "idMeal"), NamaResep = Value(meal, "strMeal"),
            ImageURL = Value(meal, "strMealThumb"), Instructions = Value(meal, "strInstructions"),
            Ingredients = ingredients, MatchedIngredients = matched, MissingIngredients = missing
        };
    }

    private static string Normalize(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        return IngredientNames.TryGetValue(key, out var translated) ? translated : key;
    }

    private static string Value(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static async Task<JsonDocument> GetAsync(string path)
    {
        using var response = await Client.GetAsync(BaseUrl + path);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
