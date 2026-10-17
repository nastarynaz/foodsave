using FoodSave.Models;
using Npgsql;
using NpgsqlTypes;

namespace FoodSave.Services;

public sealed class FoodSaveRepository
{
    private readonly string _connectionString;

    public FoodSaveRepository()
    {
        _connectionString = Environment.GetEnvironmentVariable("FOODSAVE_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Atur FOODSAVE_CONNECTION_STRING sebelum menjalankan FoodSave.");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task InitializeAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
        var sql = await File.ReadAllTextAsync(path);
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<User> RegisterAsync(string name, string email, string password)
    {
        name = name.Trim(); email = email.Trim().ToLowerInvariant();
        if (name.Length is < 2 or > 120 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email || email.Length > 254 || password.Length < 8)
            throw new ArgumentException("Isi nama, email valid, dan kata sandi minimal 8 karakter.");
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO users(nama,email,password_hash) VALUES(@name,@email,@hash) RETURNING user_id", connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("hash", PasswordService.Hash(password));
        try
        {
            var id = (int)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Registrasi gagal."));
            return new User { UserID = id, Nama = name, Email = email };
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ArgumentException("Email sudah terdaftar.");
        }
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT user_id,nama,email,password_hash FROM users WHERE email=@email", connection);
        command.Parameters.AddWithValue("email", email.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || !PasswordService.Verify(password, reader.GetString(3))) return null;
        return new User { UserID = reader.GetInt32(0), Nama = reader.GetString(1), Email = reader.GetString(2) };
    }

    public async Task<List<FoodItem>> GetFoodsAsync(int userId)
    {
        var result = new List<FoodItem>();
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT food_id,user_id,nama_makanan,kategori,jumlah,tanggal_pembelian,tanggal_kedaluwarsa FROM food_items WHERE user_id=@user AND NOT is_archived ORDER BY tanggal_kedaluwarsa,nama_makanan", connection);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new FoodItem
        {
            FoodID = reader.GetInt32(0), UserID = reader.GetInt32(1), NamaMakanan = reader.GetString(2),
            Kategori = reader.GetString(3), Jumlah = reader.GetInt32(4),
            TanggalPembelian = reader.GetDateTime(5), TanggalKedaluwarsa = reader.GetDateTime(6)
        });
        return result;
    }

    public async Task SaveFoodAsync(int userId, FoodItem item)
    {
        item.NamaMakanan = item.NamaMakanan.Trim(); item.Kategori = item.Kategori.Trim();
        InventoryRules.Validate(item);
        await using var connection = await OpenAsync();
        var sql = item.FoodID == 0
            ? "INSERT INTO food_items(user_id,nama_makanan,kategori,jumlah,tanggal_pembelian,tanggal_kedaluwarsa) VALUES(@user,@name,@category,@quantity,@purchase,@expiry)"
            : "UPDATE food_items SET nama_makanan=@name,kategori=@category,jumlah=@quantity,tanggal_pembelian=@purchase,tanggal_kedaluwarsa=@expiry WHERE food_id=@food AND user_id=@user AND NOT is_archived";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("name", item.NamaMakanan);
        command.Parameters.AddWithValue("category", item.Kategori);
        command.Parameters.AddWithValue("quantity", item.Jumlah);
        command.Parameters.AddWithValue("purchase", NpgsqlDbType.Date, item.TanggalPembelian.Date);
        command.Parameters.AddWithValue("expiry", NpgsqlDbType.Date, item.TanggalKedaluwarsa.Date);
        if (item.FoodID != 0) command.Parameters.AddWithValue("food", item.FoodID);
        if (await command.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Bahan tidak ditemukan atau tidak dapat diubah.");
    }

    public async Task ArchiveFoodAsync(int userId, int foodId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE food_items SET is_archived=true WHERE food_id=@food AND user_id=@user AND NOT is_archived", connection);
        command.Parameters.AddWithValue("food", foodId);
        command.Parameters.AddWithValue("user", userId);
        if (await command.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Bahan tidak ditemukan.");
    }

    public async Task RecordUsageAsync(int userId, int foodId, int amount, string status)
    {
        InventoryRules.ValidateUsage(amount, status);
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var update = new NpgsqlCommand("UPDATE food_items SET jumlah=jumlah-@amount WHERE food_id=@food AND user_id=@user AND NOT is_archived AND jumlah>=@amount", connection, transaction);
        update.Parameters.AddWithValue("amount", amount);
        update.Parameters.AddWithValue("food", foodId);
        update.Parameters.AddWithValue("user", userId);
        if (await update.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Stok tidak cukup atau bahan tidak ditemukan.");
        await using var insert = new NpgsqlCommand("INSERT INTO usage_history(food_id,jumlah_terpakai,status_penggunaan) VALUES(@food,@amount,@status)", connection, transaction);
        insert.Parameters.AddWithValue("food", foodId);
        insert.Parameters.AddWithValue("amount", amount);
        insert.Parameters.AddWithValue("status", status);
        await insert.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    public async Task<List<UsageHistory>> GetUsageAsync(int userId)
    {
        var result = new List<UsageHistory>();
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT h.history_id,h.food_id,f.nama_makanan,h.tanggal_penggunaan,h.jumlah_terpakai,h.status_penggunaan FROM usage_history h JOIN food_items f ON f.food_id=h.food_id WHERE f.user_id=@user ORDER BY h.tanggal_penggunaan DESC", connection);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new UsageHistory
        {
            HistoryID = reader.GetInt32(0), FoodID = reader.GetInt32(1), NamaMakanan = reader.GetString(2),
            TanggalPenggunaan = reader.GetDateTime(3).ToLocalTime(), JumlahTerpakai = reader.GetInt32(4), StatusPenggunaan = reader.GetString(5)
        });
        return result;
    }

    public async Task RecordRecipeAccessAsync(int userId, Recipe recipe)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO recipe_history(user_id,meal_api_id,nama_resep) VALUES(@user,@id,@name)", connection);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("id", recipe.ApiID);
        command.Parameters.AddWithValue("name", recipe.NamaResep);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<RecipeHistory>> GetRecipeHistoryAsync(int userId)
    {
        var result = new List<RecipeHistory>();
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand("SELECT recipe_id,user_id,meal_api_id,nama_resep,tanggal_akses FROM recipe_history WHERE user_id=@user ORDER BY tanggal_akses DESC LIMIT 100", connection);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new RecipeHistory
        {
            RecipeID = reader.GetInt32(0), UserID = reader.GetInt32(1), MealApiID = reader.GetString(2),
            NamaResep = reader.GetString(3), TanggalAkses = reader.GetDateTime(4).ToLocalTime()
        });
        return result;
    }
}
