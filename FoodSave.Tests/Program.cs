using FoodSave.Models;
using FoodSave.Services;
using Npgsql;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Gagal: " + name);
    Console.WriteLine("OK: " + name);
}

static bool Throws(Action action)
{
    try { action(); return false; }
    catch (ArgumentException) { return true; }
}

var today = new DateTime(2026, 10, 17);
Check(ExpiryStatus.For(today.AddDays(-1), today) == "Kedaluwarsa", "kedaluwarsa");
Check(ExpiryStatus.For(today, today) == "Hari ini", "hari ini");
Check(ExpiryStatus.For(today.AddDays(3), today) == "Segera kedaluwarsa", "batas 3 hari");
Check(ExpiryStatus.For(today.AddDays(4), today) == "Aman", "lebih dari 3 hari");

var food = new FoodItem { NamaMakanan = "Telur", Kategori = "Protein", Jumlah = 2,
    TanggalPembelian = today, TanggalKedaluwarsa = today.AddDays(7) };
InventoryRules.Validate(food);
Check(Throws(() => InventoryRules.Validate(new FoodItem { NamaMakanan = "Telur", Kategori = "Protein", Jumlah = 0,
    TanggalPembelian = today, TanggalKedaluwarsa = today.AddDays(1) })), "stok harus positif");
Check(Throws(() => InventoryRules.Validate(new FoodItem { NamaMakanan = "Telur", Kategori = "Protein", Jumlah = 1,
    TanggalPembelian = today, TanggalKedaluwarsa = today.AddDays(-1) })), "tanggal valid");
Check(Throws(() => InventoryRules.ValidateUsage(-1, "Digunakan")), "jumlah penggunaan valid");
Check(Throws(() => InventoryRules.ValidateUsage(1, "Lainnya")), "status penggunaan valid");

var hash = PasswordService.Hash("password-rahasia");
Check(!hash.Contains("password-rahasia") && PasswordService.Verify("password-rahasia", hash), "hash kata sandi");
Check(!PasswordService.Verify("salah", hash), "tolak kata sandi salah");

var testConnection = Environment.GetEnvironmentVariable("FOODSAVE_TEST_CONNECTION_STRING");
if (testConnection != null)
{
    Environment.SetEnvironmentVariable("FOODSAVE_CONNECTION_STRING", testConnection);
    var repository = new FoodSaveRepository();
    await repository.InitializeAsync();
    var suffix = Guid.NewGuid().ToString("N");
    var emailA = "test-a-" + suffix + "@example.test";
    var emailB = "test-b-" + suffix + "@example.test";
    try
    {
        var a = await repository.RegisterAsync("Pengguna A", emailA, "rahasia-123");
        var b = await repository.RegisterAsync("Pengguna B", emailB, "rahasia-123");
        await repository.SaveFoodAsync(a.UserID, food);
        var aFood = (await repository.GetFoodsAsync(a.UserID)).Single();
        Check((await repository.GetFoodsAsync(b.UserID)).Count == 0, "isolasi daftar stok");
        Check(await ThrowsAsync(() => repository.ArchiveFoodAsync(b.UserID, aFood.FoodID)), "tolak hapus lintas akun");
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(b.UserID, aFood.FoodID, 1, "Digunakan")), "tolak penggunaan lintas akun");
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(a.UserID, aFood.FoodID, 3, "Digunakan")), "tolak penggunaan melebihi stok");
        await repository.RecordUsageAsync(a.UserID, aFood.FoodID, 1, "Digunakan");
        Check((await repository.GetFoodsAsync(a.UserID)).Single().Jumlah == 1, "stok berkurang atomik");
        Check((await repository.GetUsageAsync(a.UserID)).Count == 1 && (await repository.GetUsageAsync(b.UserID)).Count == 0, "isolasi riwayat");
    }
    finally
    {
        await using var connection = new NpgsqlConnection(testConnection);
        await connection.OpenAsync();
        await using var cleanup = new NpgsqlCommand("DELETE FROM usage_history WHERE food_id IN (SELECT food_id FROM food_items WHERE user_id IN (SELECT user_id FROM users WHERE email IN (@a,@b))); DELETE FROM users WHERE email IN (@a,@b)", connection);
        cleanup.Parameters.AddWithValue("a", emailA);
        cleanup.Parameters.AddWithValue("b", emailB);
        await cleanup.ExecuteNonQueryAsync();
    }
}

static async Task<bool> ThrowsAsync(Func<Task> action)
{
    try { await action(); return false; }
    catch (InvalidOperationException) { return true; }
}
