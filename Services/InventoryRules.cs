using FoodSave.Models;

namespace FoodSave.Services;

public static class InventoryRules
{
    public static void Validate(FoodItem item)
    {
        if (item.NamaMakanan.Trim().Length is < 1 or > 150 ||
            item.Kategori.Trim().Length is < 1 or > 80 || item.Jumlah <= 0 ||
            item.TanggalKedaluwarsa.Date < item.TanggalPembelian.Date)
            throw new ArgumentException("Periksa nama, kategori, jumlah, dan tanggal bahan.");
    }

    public static void ValidateUsage(int amount, string status)
    {
        if (amount <= 0 || status is not ("Digunakan" or "Dibuang"))
            throw new ArgumentException("Jumlah dan status tidak valid.");
    }
}
