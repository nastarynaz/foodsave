using FoodSave.Models;

namespace FoodSave.Services;

/// <summary>Proses "Pengingat Kedaluwarsa" pada Dashboard (Modul 9).</summary>
public static class ExpiryReminderService
{
    /// <summary>
    /// Menyusun daftar pengingat untuk bahan yang masih memiliki stok.
    /// Method ini hanya mengenal tipe dasar <see cref="ExpiryReminder"/>; perilaku
    /// NeedsAttention, Priority, dan Message ditentukan oleh kelas turunannya saat runtime.
    /// </summary>
    public static List<ExpiryReminder> BuildReminders(IEnumerable<FoodItem> foods, DateTime today) =>
        foods.Where(HasStock)
            .Select(food => ExpiryReminder.Create(food, today))
            .Where(reminder => reminder.NeedsAttention)
            .OrderBy(reminder => reminder.Priority)
            .ThenBy(reminder => reminder.DaysLeft)
            .ThenBy(reminder => reminder.NamaMakanan, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    // private: aturan internal yang tidak perlu diketahui pemanggil.
    private static bool HasStock(FoodItem food) => food.Jumlah > 0;
}
