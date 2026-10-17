namespace FoodSave.Models;

public sealed class UsageHistory
{
    public int HistoryID { get; init; }
    public int FoodID { get; init; }
    public string NamaMakanan { get; init; } = "";
    public DateTime TanggalPenggunaan { get; init; }
    public int JumlahTerpakai { get; init; }
    public string StatusPenggunaan { get; init; } = "";
}
