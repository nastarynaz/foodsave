namespace FoodSave.Models;

public sealed class FoodItem
{
    public int FoodID { get; init; }
    public int UserID { get; init; }
    public string NamaMakanan { get; set; } = "";
    public string Kategori { get; set; } = "";
    public int Jumlah { get; set; }
    public DateTime TanggalPembelian { get; set; }
    public DateTime TanggalKedaluwarsa { get; set; }
    public string Status => ExpiryStatus.For(TanggalKedaluwarsa, DateTime.Today);
}

public static class ExpiryStatus
{
    public static string For(DateTime expiry, DateTime today)
    {
        var days = (expiry.Date - today.Date).Days;
        return days < 0 ? "Kedaluwarsa" : days == 0 ? "Hari ini" : days <= 3 ? "Segera kedaluwarsa" : "Aman";
    }
}
