namespace FoodSave.Models;

public sealed class RecipeHistory
{
    public int RecipeID { get; init; }
    public int UserID { get; init; }
    public string MealApiID { get; init; } = "";
    public string NamaResep { get; init; } = "";
    public DateTime TanggalAkses { get; init; }
}
