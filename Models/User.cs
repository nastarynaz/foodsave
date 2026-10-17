namespace FoodSave.Models;

public sealed class User
{
    public int UserID { get; init; }
    public string Nama { get; init; } = "";
    public string Email { get; init; } = "";
}
