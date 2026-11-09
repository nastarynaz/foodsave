# Implementasi Modul 8 — PostgreSQL to C-Sharp

## 1. Ringkasan proyek

FoodSave adalah aplikasi pengelolaan persediaan makanan untuk mahasiswa dan rumah tangga. Proses bisnis yang dipilih untuk Modul 8 adalah pencatatan penggunaan atau pembuangan bahan. Proses ini menerapkan encapsulation dan menjalankan operasi PostgreSQL nyata melalui Npgsql.

## 2. Hasil audit arsitektur

- Proyek utama: `FoodSave.csproj`, target `net8.0-windows`, WPF, Npgsql versi 8.0.9. Tidak ada berkas `.sln`.
- Entry point: `App.xaml` dengan `StartupUri="Views/LoginWindow.xaml"`.
- `Models/`: `User`, `FoodItem`, `UsageHistory`, `Recipe`, dan `RecipeHistory`.
- `Views/`: login/registrasi, jendela utama, dialog bahan, dan dialog penggunaan. Tampilan memakai XAML dengan event handler pada code-behind; belum menerapkan MVVM atau container dependency injection.
- `Services/FoodSaveRepository.cs`: autentikasi, CRUD stok, pengarsipan, transaksi penggunaan, dan riwayat resep.
- `InventoryRules`: validasi jumlah, status, dan data bahan. `PasswordService`: PBKDF2-SHA256 dengan salt acak. `TheMealDBApiService`: rekomendasi resep.
- `Database/schema.sql`: skema PostgreSQL yang sudah ada. `InitializeAsync()` menjalankan `CREATE TABLE/INDEX IF NOT EXISTS`; tidak melakukan reset atau migrasi tabel lama. Skema ini tidak diubah untuk Modul 8.
- `FoodSave.Tests`: executable console `net8.0` yang menautkan model dan service asli. Ini bukan proyek xUnit/MSTest; menjalankan `dotnet test` saja tidak menjalankan pemeriksaan bisnis tersebut.

Arsitektur yang dipertahankan adalah UI → repository/service → Npgsql → PostgreSQL. Repository sudah memiliki proses penggunaan stok, sehingga implementasi diperluas di tempat yang sama tanpa membuat `StockService` dengan fungsi duplikat.

## 3. Konsep OOP: encapsulation

Kelas `FoodSaveRepository` menyembunyikan connection string di field `private readonly string _connectionString`. Method `OpenAsync()` juga privat. UI mengakses proses melalui method publik, bukan menjalankan SQL atau mengelola transaksi sendiri.

Method yang digunakan untuk tugas:

```csharp
public async Task RecordUsageAsync(int userId, int foodId, int amount, string status)
```

Pemanggil memberikan ID pengguna yang sedang login, ID bahan, jumlah, dan status `Digunakan` atau `Dibuang`. Validasi, pemeriksaan akses, penguncian stok, perubahan jumlah, dan pencatatan riwayat diselesaikan di dalam method. Dengan demikian, UI tidak perlu mengetahui urutan operasi database yang menjaga konsistensi stok.

Nilai `userId` berasal dari objek pengguna hasil login pada aplikasi desktop ini. Pemeriksaan kepemilikan bukan pengganti autentikasi server jika kelak aplikasi diubah menjadi layanan web.

## 4. Alur method

1. `InventoryRules.ValidateUsage(amount, status)` menolak jumlah nol/negatif dan status di luar pilihan sebelum membuka koneksi.
2. `OpenAsync()` membuka koneksi PostgreSQL secara asinkron. Koneksi dibuang jika pembukaan gagal.
3. `BeginTransactionAsync()` memulai transaksi.
4. `SELECT jumlah ... WHERE food_id=@food AND user_id=@user AND NOT is_archived FOR UPDATE` membaca sekaligus mengunci baris milik pengguna. ID yang tidak ditemukan, bukan milik pengguna, atau sudah diarsipkan ditolak dengan pesan akses yang sama.
5. Jika stok lebih kecil dari permintaan, method melempar `InvalidOperationException`.
6. `UPDATE` mengurangi stok. Filter kepemilikan, pengarsipan, dan kecukupan stok tetap disertakan sebagai penjagaan pada query perubahan.
7. `INSERT` menambahkan riwayat penggunaan atau pembuangan.
8. `CommitAsync()` menyimpan kedua perubahan bersama-sama.

Semua command perubahan memakai koneksi dan transaksi yang sama. `await using` membuang transaksi; transaksi yang belum di-commit di-rollback jika validasi stok atau command gagal. Tidak ada exception yang ditelan sebagai keberhasilan. UI menangkap error melalui alur penanganan error yang sudah ada.

Penguncian `FOR UPDATE` membuat permintaan penggunaan bersamaan terhadap baris yang sama menunggu giliran. Setelah memperoleh kunci, permintaan berikutnya memeriksa stok terkini.

## 5. Koneksi PostgreSQL

Konfigurasi aplikasi tetap berasal dari `FOODSAVE_CONNECTION_STRING`. Tes hanya memakai `FOODSAVE_TEST_CONNECTION_STRING` dan mengarahkannya ke repository di dalam proses pengujian. Tes tidak mengambil connection string aplikasi sebagai cadangan.

Format lokal, dengan placeholder yang harus diganti sesuai konfigurasi sendiri:

```text
Host=<host-lokal>;Port=<port>;Database=<database>;Username=<pengguna>;Password=<password>;SSL Mode=Disable
```

Format PostgreSQL remote/Supabase:

```text
Host=<host-dari-konfigurasi>;Port=<port-dari-konfigurasi>;Database=<database>;Username=<pengguna>;Password=<password>;SSL Mode=VerifyFull
```

Gunakan host, port, username, dan password database dari konfigurasi proyek Supabase yang sebenarnya. Bila diperlukan, tambahkan `Root Certificate=<path-sertifikat-CA>` sesuai penyedia. `VerifyFull` memverifikasi sertifikat dan nama host. `Require` mengenkripsi koneksi tetapi pada Npgsql 8 tidak memverifikasi identitas server. Tidak diperlukan anon key maupun service-role key untuk koneksi Npgsql.

Tidak ada kredensial yang ditulis ke source code. File `.env` dan `.env.*` diabaikan Git; aplikasi tidak otomatis memuat `.env`. Isi environment variable pada terminal yang menjalankan aplikasi. Jangan menampilkan connection string pada screenshot.

## 6. Skema aktual dan operasi database

Seluruh kolom di bawah mengikuti `Database/schema.sql`; semua kolom bersifat `NOT NULL`.

| Tabel | Kolom dan tipe |
| --- | --- |
| `users` | `user_id integer` identity PK, `nama varchar(120)`, `email varchar(254)` UNIQUE, `password_hash text` |
| `food_items` | `food_id integer` identity PK, `user_id integer` FK users, `nama_makanan varchar(150)`, `kategori varchar(80)`, `jumlah integer`, `tanggal_pembelian date`, `tanggal_kedaluwarsa date`, `is_archived boolean` default false |
| `usage_history` | `history_id integer` identity PK, `food_id integer` FK food_items, `tanggal_penggunaan timestamptz` default now(), `jumlah_terpakai integer`, `status_penggunaan varchar(12)` |
| `recipe_history` | `recipe_id integer` identity PK, `user_id integer` FK users, `meal_api_id varchar(30)`, `nama_resep varchar(200)`, `tanggal_akses timestamptz` default now() |

Database membatasi `food_items.jumlah >= 0`, tanggal kedaluwarsa tidak sebelum pembelian, `jumlah_terpakai > 0`, dan status riwayat hanya `Digunakan`/`Dibuang`. Riwayat penggunaan merujuk bahan dengan `ON DELETE RESTRICT`; fitur hapus stok memakai pengarsipan agar riwayat tetap tersedia.

| Operasi | Implementasi proses penggunaan |
| --- | --- |
| READ | `SELECT jumlah FROM food_items ... FOR UPDATE`, memeriksa bahan aktif milik pengguna |
| UPDATE | `UPDATE food_items SET jumlah=jumlah-@amount ...` |
| CREATE | `INSERT INTO usage_history(food_id,jumlah_terpakai,status_penggunaan) ...` |
| TRANSACTION | Commit setelah UPDATE dan INSERT berhasil; rollback saat transaksi ditutup tanpa commit |

Nilai pengguna dikirim melalui parameter `@user`, `@food`, `@amount`, dan `@status`. Tidak ada interpolasi input pengguna ke SQL.

## 7. Integrasi aplikasi

Pada tab **Stok**, pengguna memilih bahan lalu menekan **Gunakan / Buang**. `UsageDialog` meminta jumlah dan status. `MainWindow.UseFood_Click` memanggil:

```csharp
await _repository.RecordUsageAsync(_user.UserID, food.FoodID, dialog.Amount, dialog.Status);
```

Setelah berhasil, `RefreshAsync()` memuat ulang stok, dashboard, serta riwayat/statistik. Validasi dialog membantu interaksi pengguna; repository tetap memvalidasi permintaan meskipun dipanggil tanpa dialog.

## 8. Pengujian dan hasil aktual

Lingkungan validasi: macOS ARM64, .NET SDK 8.0.427. Build awal gagal dengan CS0103 untuk `Path`/`File`; penambahan `using System.IO` memperbaikinya. Pengujian database dilaksanakan pada 9 November 2026 menggunakan PostgreSQL 16 (`postgres:16-alpine`) dalam container sementara, database `foodsave_modul8_test`, dan port localhost yang dialokasikan Docker. Password dibuat acak dan hanya diteruskan melalui environment proses. Seluruh 35 pemeriksaan berhasil dengan exit code 0; container dihentikan dan data sementara dibuang setelah pengujian.

| Pemeriksaan | Hasil aktual |
| --- | --- |
| `dotnet build FoodSave.csproj --nologo` setelah perbaikan | Berhasil, 0 warning dan 0 error |
| Tes non-database | 11 pemeriksaan berhasil |
| A: stok 10, penggunaan 2 | Berhasil: sisa 8, satu riwayat dengan jumlah 2 dan status Digunakan |
| B: stok 2, penggunaan 5 | Berhasil: ditolak, stok tetap 2, riwayat kosong |
| C: jumlah 0 dan -1 | Berhasil: keduanya ditolak, stok tetap 2, riwayat kosong |
| D: koneksi dan query tabel | Berhasil: OpenAsync dan SELECT kolom pada seluruh empat tabel |
| Dua permintaan penggunaan 2 dengan stok awal 2 | Berhasil: tepat satu sukses, stok akhir 0, satu riwayat |
| Isolasi akun, ID tidak ditemukan, pembuangan, pengarsipan | Seluruh pemeriksaan berhasil |
| UI WPF pada Windows | Belum diuji; lingkungan saat ini macOS |
| Koneksi Supabase/SSL remote | Belum diuji; konfigurasi pengguna belum tersedia |

Tes tambahan memeriksa isolasi antar akun, ID bahan yang tidak ditemukan, pembuangan, bahan arsip, retensi riwayat, dan dua permintaan stok bersamaan. Tes menggunakan dua akun sementara dengan email unik; pembersihan dibatasi ke akun tersebut beserta datanya. Jalankan hanya pada database khusus pengujian. Tidak ada `DROP`, `TRUNCATE`, atau penghapusan data akun lain.

Jika connection string pengujian kosong, runner menampilkan `SKIP`. Opsi `--require-database` membuat kondisi tersebut menghasilkan exit code 1 agar tes database yang terlewat tidak dianggap sebagai bukti keberhasilan.

Ringkasan output eksekusi database:

```text
OK: D: koneksi PostgreSQL dan query seluruh tabel berhasil
OK: A: 10 - 2 = 8, satu riwayat
OK: A: isi riwayat sesuai permintaan
OK: B: stok tidak cukup ditolak
OK: B: stok 2 dan tanpa riwayat setelah penolakan
OK: C: jumlah 0 ditolak
OK: C: tidak ada perubahan database
OK: C: jumlah -1 ditolak
OK: C: tidak ada perubahan database
OK: dua permintaan bersamaan: hanya satu berhasil
OK: permintaan bersamaan: stok tidak negatif, satu riwayat
```

Penolakan sebelum perubahan dan konkurensi telah diuji. Kegagalan database yang sengaja dipaksakan di antara UPDATE dan INSERT belum diuji tersendiri; perilaku rollback pada kondisi tersebut menggunakan lifecycle transaksi Npgsql.

## 9. Menjalankan dari VS Code

Jalankan perintah dari folder `foodsave`.

```bash
dotnet --info
dotnet restore FoodSave.csproj
dotnet build FoodSave.csproj
dotnet run --project FoodSave.Tests/FoodSave.Tests.csproj
```

Untuk tes PostgreSQL, siapkan database khusus pengujian dengan skema kosong atau skema FoodSave yang kompatibel. Akun database pengujian perlu izin membuat tabel/index dan melakukan CRUD. Masukkan connection string tanpa mencatat password pada riwayat perintah, menggunakan terminal zsh macOS:

```zsh
read -rs 'FOODSAVE_TEST_CONNECTION_STRING?Connection string database uji: '
export FOODSAVE_TEST_CONNECTION_STRING
echo
dotnet run --project FoodSave.Tests/FoodSave.Tests.csproj -- --require-database
unset FOODSAVE_TEST_CONNECTION_STRING
```

`InitializeAsync()` memakai skema yang sudah ada untuk membuat tabel yang belum tersedia. Ini tidak memperbaiki otomatis tabel lama yang strukturnya berbeda. Pada database yang sudah digunakan, cocokkan skema sebelum menjalankan aplikasi dan jangan melakukan reset data.

WPF hanya dapat dijalankan di Windows. `EnableWindowsTargeting=true` memungkinkan build target Windows dari macOS; hasil build bukan executable UI native macOS. Model, repository, dan runner tes `net8.0` dapat dijalankan di macOS. Migrasi UI tidak diperlukan untuk memenuhi Modul 8. Jika kelak diperlukan UI native macOS, usulan Avalonia harus disetujui terpisah karena memerlukan porting XAML, kontrol, dialog, resource aplikasi, dan pengujian ulang UI.

Untuk UI pada Windows dengan .NET 8 SDK dan PostgreSQL yang sudah disiapkan, masukkan konfigurasi melalui PowerShell:

```powershell
$secureConnection = Read-Host 'Connection string PostgreSQL' -AsSecureString
$env:FOODSAVE_CONNECTION_STRING = [System.Net.NetworkCredential]::new('', $secureConnection).Password
dotnet run --project FoodSave.csproj
Remove-Item Env:FOODSAVE_CONNECTION_STRING
```

## 10. Checklist screenshot worksheet

- [ ] Struktur proyek dan target framework pada `.csproj`.
- [ ] Field privat `_connectionString`, method privat `OpenAsync()`, dan method publik `RecordUsageAsync()` sebagai bukti encapsulation.
- [ ] Kode SELECT, UPDATE, INSERT berparameter dan transaksi pada method tersebut.
- [ ] Build berhasil dengan 0 error.
- [ ] Output tes A: 10 − 2 = 8 beserta satu riwayat yang sesuai.
- [ ] Output tes B dan C: penolakan serta stok/riwayat yang tidak berubah.
- [ ] Output tes D: koneksi PostgreSQL dan query seluruh tabel berhasil.
- [ ] Pada Windows: stok awal 10 → dialog penggunaan 2 → stok akhir 8 → riwayat penggunaan 2. Gunakan akun demonstrasi khusus.
- [ ] Pada alat database: baris `food_items` dan `usage_history` milik akun demonstrasi yang sama. Jangan menampilkan password atau connection string. Data tes otomatis sudah dibersihkan setelah runner selesai, jadi gunakan catatan demonstrasi terpisah untuk screenshot tabel.

## 11. Perubahan dan batas validasi

- `Services/FoodSaveRepository.cs`: pembacaan stok dengan row lock, perbaikan impor IO, dan disposal koneksi ketika gagal dibuka.
- `FoodSave.Tests/Program.cs`: skenario A–D, validasi status/akses, pengarsipan, pembuangan, dan konkurensi; pelaporan SKIP dan opsi database wajib.
- `.gitignore`: perlindungan file `.env`.
- `README.md`: tautan laporan Modul 8 dan instruksi validasi.
- `docs/MODUL_8_IMPLEMENTATION.md`: laporan ini.

Tidak ada perubahan framework, tabel, namespace, signature method publik, atau alur UI. Validasi service tidak membuktikan seluruh interaksi UI WPF, API resep, atau konfigurasi Supabase telah berjalan.

Referensi: [Microsoft — EnableWindowsTargeting](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100), [Npgsql — keamanan koneksi](https://www.npgsql.org/doc/security.html), [Npgsql 8 — perubahan SSL](https://www.npgsql.org/doc/release-notes/8.0.html).
