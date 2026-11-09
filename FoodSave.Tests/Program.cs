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
Check(Throws(() => InventoryRules.ValidateUsage(0, "Digunakan")), "jumlah penggunaan nol ditolak");
Check(Throws(() => InventoryRules.ValidateUsage(1, "Lainnya")), "status penggunaan valid");

var hash = PasswordService.Hash("password-rahasia");
Check(!hash.Contains("password-rahasia") && PasswordService.Verify("password-rahasia", hash), "hash kata sandi");
Check(!PasswordService.Verify("salah", hash), "tolak kata sandi salah");

var testConnection = Environment.GetEnvironmentVariable("FOODSAVE_TEST_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(testConnection))
{
    Environment.SetEnvironmentVariable("FOODSAVE_CONNECTION_STRING", testConnection);
    var repository = new FoodSaveRepository();
    await repository.InitializeAsync();
    await using (var connection = new NpgsqlConnection(testConnection))
    {
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT user_id,nama,email,password_hash FROM users LIMIT 0; SELECT food_id,user_id,nama_makanan,kategori,jumlah,tanggal_pembelian,tanggal_kedaluwarsa,is_archived FROM food_items LIMIT 0; SELECT history_id,food_id,tanggal_penggunaan,jumlah_terpakai,status_penggunaan FROM usage_history LIMIT 0; SELECT recipe_id,user_id,meal_api_id,nama_resep,tanggal_akses FROM recipe_history LIMIT 0", connection);
        await query.ExecuteNonQueryAsync();
        Check(true, "D: koneksi PostgreSQL dan query seluruh tabel berhasil");
    }
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

        var successFood = await AddFoodAsync(repository, a.UserID, "Skenario A", 10);
        await repository.RecordUsageAsync(a.UserID, successFood.FoodID, 2, "Digunakan");
        await CheckStateAsync(repository, a.UserID, successFood.FoodID, 8, 1, "A: 10 - 2 = 8, satu riwayat");
        var entry = (await repository.GetUsageAsync(a.UserID)).Single(h => h.FoodID == successFood.FoodID);
        Check(entry.JumlahTerpakai == 2 && entry.StatusPenggunaan == "Digunakan", "A: isi riwayat sesuai permintaan");

        var insufficientFood = await AddFoodAsync(repository, a.UserID, "Skenario B", 2);
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(a.UserID, insufficientFood.FoodID, 5, "Digunakan")), "B: stok tidak cukup ditolak");
        await CheckStateAsync(repository, a.UserID, insufficientFood.FoodID, 2, 0, "B: stok 2 dan tanpa riwayat setelah penolakan");

        foreach (var invalidAmount in new[] { 0, -1 })
        {
            var rejected = false;
            try { await repository.RecordUsageAsync(a.UserID, insufficientFood.FoodID, invalidAmount, "Digunakan"); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, $"C: jumlah {invalidAmount} ditolak");
            await CheckStateAsync(repository, a.UserID, insufficientFood.FoodID, 2, 0, "C: tidak ada perubahan database");
        }
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(b.UserID, insufficientFood.FoodID, 1, "Digunakan")), "kepemilikan: penggunaan lintas akun ditolak");
        await CheckStateAsync(repository, a.UserID, insufficientFood.FoodID, 2, 0, "kepemilikan: stok dan riwayat tetap");
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(a.UserID, 0, 1, "Digunakan")), "bahan tidak ditemukan ditolak");

        await repository.RecordUsageAsync(a.UserID, insufficientFood.FoodID, 1, "Dibuang");
        await CheckStateAsync(repository, a.UserID, insufficientFood.FoodID, 1, 1, "pembuangan mengurangi stok dan mencatat riwayat");
        Check((await repository.GetUsageAsync(a.UserID)).Single(h => h.FoodID == insufficientFood.FoodID).StatusPenggunaan == "Dibuang", "status pembuangan tersimpan");
        await repository.ArchiveFoodAsync(a.UserID, insufficientFood.FoodID);
        Check(await ThrowsAsync(() => repository.RecordUsageAsync(a.UserID, insufficientFood.FoodID, 1, "Digunakan")), "bahan arsip ditolak");
        Check((await repository.GetUsageAsync(a.UserID)).Any(h => h.FoodID == insufficientFood.FoodID), "pengarsipan mempertahankan riwayat");

        var concurrentFood = await AddFoodAsync(repository, a.UserID, "Permintaan bersamaan", 2);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await repository.RecordUsageAsync(a.UserID, concurrentFood.FoodID, 2, "Digunakan"); return true; }
            catch (InvalidOperationException) { return false; }
        }));
        Check(attempts.Count(success => success) == 1, "dua permintaan bersamaan: hanya satu berhasil");
        await CheckStateAsync(repository, a.UserID, concurrentFood.FoodID, 0, 1, "permintaan bersamaan: stok tidak negatif, satu riwayat");
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
else
{
    Console.WriteLine("SKIP: tes A-D PostgreSQL belum dijalankan; atur FOODSAVE_TEST_CONNECTION_STRING ke database khusus pengujian.");
    if (args.Contains("--require-database")) Environment.ExitCode = 1;
}

static async Task<FoodItem> AddFoodAsync(FoodSaveRepository repository, int userId, string name, int quantity)
{
    await repository.SaveFoodAsync(userId, new FoodItem
    {
        NamaMakanan = name, Kategori = "Pengujian", Jumlah = quantity,
        TanggalPembelian = DateTime.Today, TanggalKedaluwarsa = DateTime.Today.AddDays(7)
    });
    return (await repository.GetFoodsAsync(userId)).Single(f => f.NamaMakanan == name);
}

static async Task CheckStateAsync(FoodSaveRepository repository, int userId, int foodId, int stock, int historyCount, string name)
{
    var item = (await repository.GetFoodsAsync(userId)).Single(f => f.FoodID == foodId);
    var history = (await repository.GetUsageAsync(userId)).Where(h => h.FoodID == foodId).ToList();
    Check(item.Jumlah == stock && history.Count == historyCount, name);
}

static async Task<bool> ThrowsAsync(Func<Task> action)
{
    try { await action(); return false; }
    catch (InvalidOperationException) { return true; }
}
