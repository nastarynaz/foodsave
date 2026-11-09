# Laboratorium Informatika
# Junior Project — Teknik Basis Data dan Pemrograman Berbasis Objek
# Worksheet Modul 8: PostgreSQL to C-Sharp

**Nama proyek:** FoodSave

**Repository:** https://github.com/nastarynaz/foodsave

| Anggota kelompok | NIM |
| --- | --- |
| Javier Yazid Janadi | 24/545752/TK/60737 |
| Muhammad Khoirunas | 24/533373/TK/59083 |
| Faiz Gymnastiar Wibawa | 24/537851/TK/59634 |

## 1. Method yang menerapkan konsep OOP

FoodSave menerapkan **encapsulation** pada kelas `FoodSaveRepository` untuk proses pencatatan penggunaan atau pembuangan bahan makanan. Proses ini sesuai dengan rancangan aplikasi: stok berkurang ketika bahan digunakan atau dibuang, kemudian aktivitas tersebut dicatat sebagai riwayat.

Implementasi berada di `Services/FoodSaveRepository.cs`, melalui method:

```csharp
public async Task RecordUsageAsync(int userId, int foodId, int amount, string status)
{
    InventoryRules.ValidateUsage(amount, status);
    await using var connection = await OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await using (var read = new NpgsqlCommand("SELECT jumlah FROM food_items WHERE food_id=@food AND user_id=@user AND NOT is_archived FOR UPDATE", connection, transaction))
    {
        read.Parameters.AddWithValue("food", foodId);
        read.Parameters.AddWithValue("user", userId);
        var stock = await read.ExecuteScalarAsync();
        if (stock is not int available)
            throw new InvalidOperationException("Bahan tidak ditemukan atau tidak dapat diakses.");
        if (available < amount)
            throw new InvalidOperationException("Stok tidak cukup.");
    }
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
```

Penerapan encapsulation ditunjukkan oleh:

- Field `private readonly string _connectionString` menyimpan konfigurasi koneksi di dalam kelas.
- Method privat `OpenAsync()` menyembunyikan detail pembukaan koneksi.
- Method publik `RecordUsageAsync()` menjadi akses terkontrol untuk menjalankan proses bisnis. Pemanggil tidak mengatur SQL atau transaksi sendiri.
- Validasi jumlah, pemeriksaan kepemilikan, pengurangan stok, dan pencatatan riwayat dilakukan dalam satu proses yang konsisten.

Method menerima `userId` sebagai ID pengguna yang sedang login, `foodId` sebagai ID bahan, `amount` sebagai jumlah penggunaan, serta `status` berupa `Digunakan` atau `Dibuang`. Jumlah nol/negatif ditolak oleh `InventoryRules.ValidateUsage()`. Bahan yang bukan milik pengguna, sudah diarsipkan, atau tidak memiliki stok cukup juga ditolak.

UI memanggil method tersebut melalui tombol **Gunakan / Buang** pada tab **Stok**. Pemanggilan yang sudah terintegrasi pada `Views/MainWindow.xaml.cs` adalah:

```csharp
await _repository.RecordUsageAsync(_user.UserID, food.FoodID, dialog.Amount, dialog.Status);
await RefreshAsync();
```

## 2. Menghubungkan aplikasi dengan PostgreSQL

FoodSave menggunakan **Npgsql 8.0.9** pada C#/.NET 8 untuk mengakses PostgreSQL. Connection string dibaca dari environment variable agar password tidak ditulis langsung dalam source code:

```csharp
private readonly string _connectionString;

public FoodSaveRepository()
{
    _connectionString = Environment.GetEnvironmentVariable("FOODSAVE_CONNECTION_STRING")
        ?? throw new InvalidOperationException("Atur FOODSAVE_CONNECTION_STRING sebelum menjalankan FoodSave.");
}
```

Method privat `OpenAsync()` membuat `NpgsqlConnection` menggunakan konfigurasi tersebut dan memanggil `OpenAsync()` pada koneksi. Proses penggunaan bahan menjalankan operasi berikut pada skema PostgreSQL yang sudah ada:

| Operasi | Pelaksanaan |
| --- | --- |
| READ | Membaca `food_items.jumlah` berdasarkan `food_id`, `user_id`, dan status arsip. `FOR UPDATE` mengunci baris selama transaksi. |
| UPDATE | Mengurangi `food_items.jumlah` sesuai jumlah penggunaan. |
| CREATE | Menambahkan `food_id`, `jumlah_terpakai`, dan `status_penggunaan` ke `usage_history`. Kolom `tanggal_penggunaan` diisi oleh default `now()` pada database. |
| TRANSACTION | Menyimpan perubahan stok dan riwayat bersama melalui `CommitAsync()`. Transaksi yang ditutup tanpa commit akan di-rollback. |

SQL menggunakan parameter, seperti `@food`, `@user`, `@amount`, dan `@status`. Oleh karena itu, input pengguna tidak digabungkan langsung ke teks query. Konfigurasi Npgsql dapat diarahkan ke PostgreSQL lokal atau Supabase PostgreSQL menggunakan connection string dan pengaturan SSL sesuai server.

## 3. Hasil pengujian

Pengujian implementasi telah dilakukan pada macOS ARM64 dengan .NET SDK 8.0.427 dan PostgreSQL 16 dalam container khusus pengujian. Hasilnya: build berhasil dengan **0 warning dan 0 error**, serta **35 pemeriksaan berhasil**. Ringkasan skenario utama:

| Skenario | Hasil aktual |
| --- | --- |
| Stok 10, digunakan 2 | Stok tersisa 8; terbentuk satu riwayat penggunaan dengan jumlah 2. |
| Stok 2, permintaan 5 | Ditolak; stok tetap 2 dan tidak terbentuk riwayat. |
| Jumlah penggunaan 0 atau negatif | Ditolak; stok dan riwayat tidak berubah. |
| Koneksi PostgreSQL | Koneksi berhasil dan seluruh empat tabel FoodSave dapat di-query. |
| Dua permintaan penggunaan 2 dengan stok awal 2 | Hanya satu berhasil; stok akhir 0 dan satu riwayat tersimpan. |

Runner pengujian dijalankan dari folder `foodsave`, setelah `FOODSAVE_TEST_CONNECTION_STRING` diarahkan ke database khusus pengujian:

```bash
dotnet build FoodSave.csproj
dotnet run --project FoodSave.Tests/FoodSave.Tests.csproj -- --require-database
```

Data pengujian memakai akun sementara dan dibersihkan setelah tes. Hasil koneksi PostgreSQL lokal telah diuji; koneksi Supabase belum diuji karena konfigurasi belum tersedia. UI menggunakan WPF dan harus dijalankan pada Windows. Build target Windows dan tes service dapat dilakukan dari macOS, tetapi keberhasilan build tidak berarti UI WPF dapat berjalan native di macOS.

## 4. Lampiran bukti yang perlu disertakan

- [ ] Screenshot kode field privat dan method publik sebagai bukti encapsulation.
- [ ] Screenshot kode query berparameter dan transaksi.
- [ ] Screenshot terminal build berhasil dan hasil pengujian PostgreSQL.
- [ ] Screenshot UI pada Windows: stok awal 10, penggunaan 2, dan stok akhir 8.
- [ ] Screenshot riwayat penggunaan yang memperlihatkan jumlah 2 pada akun demonstrasi yang sama.

Screenshot UI belum dibuat karena lingkungan pengembangan saat ini macOS. Gunakan akun demonstrasi dan jangan menampilkan password atau connection string. Petunjuk konfigurasi dan laporan teknis lengkap tersedia pada [MODUL_8_IMPLEMENTATION.md](MODUL_8_IMPLEMENTATION.md).
