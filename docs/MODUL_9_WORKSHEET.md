# Laboratorium Informatika
# Junior Project — Teknik Basis Data dan Pemrograman Berbasis Objek
# Worksheet Modul 9: Pilar dan Method pada OOP

**Nama proyek:** FoodSave

**Repository:** https://github.com/nastarynaz/foodsave

| Anggota kelompok | NIM |
| --- | --- |
| Javier Yazid Janadi | 24/545752/TK/60737 |
| Muhammad Khoirunas | 24/533373/TK/59083 |
| Faiz Gymnastiar Wibawa | 24/537851/TK/59634 |

## 1. Method yang menerapkan konsep OOP

Pada Modul 8, FoodSave sudah menerapkan **encapsulation** untuk proses pencatatan penggunaan bahan (`FoodSaveRepository.RecordUsageAsync()`). Pada Modul 9 kelompok menerapkan **inheritance** dan **polymorphism** pada proses lain dalam rancangan solusi, yaitu fitur MVP **Pengingat Kedaluwarsa** di Dashboard.

Sebelumnya, status kedaluwarsa hanya berupa string dari `ExpiryStatus.For()`, dan Dashboard menyaring bahan dengan membandingkan string tersebut. Sekarang setiap tingkat urgensi dimodelkan sebagai kelas turunan yang memiliki status, prioritas, dan pesan pengingatnya sendiri.

### 1.1 Struktur kelas

```text
ExpiryReminder (abstract)
├── ExpiredReminder (sealed)  → Status "Kedaluwarsa",        Priority 0
├── TodayReminder   (sealed)  → Status "Hari ini",           Priority 1
├── SoonReminder    (sealed)  → Status "Segera kedaluwarsa", Priority 2
└── SafeReminder    (sealed)  → Status "Aman",               Priority 3, NeedsAttention = false
```

File: `Models/ExpiryReminder.cs`

```csharp
public abstract class ExpiryReminder
{
    protected ExpiryReminder(FoodItem item, int daysLeft)
    {
        Item = item;
        DaysLeft = daysLeft;
    }

    public FoodItem Item { get; }
    public int DaysLeft { get; }

    public abstract string Status { get; }
    public abstract int Priority { get; }
    public virtual bool NeedsAttention => true;
    public virtual string Message =>
        $"{NamaMakanan} ({UnitText}) masih aman sampai {TanggalKedaluwarsa:dd MMM yyyy}.";

    protected string UnitText => $"{Jumlah} unit";

    public static ExpiryReminder Create(FoodItem item, DateTime today)
    {
        var days = (item.TanggalKedaluwarsa.Date - today.Date).Days;
        if (days < 0) return new ExpiredReminder(item, days);
        if (days == 0) return new TodayReminder(item, days);
        if (days <= ExpiryStatus.SoonThresholdDays) return new SoonReminder(item, days);
        return new SafeReminder(item, days);
    }
}

public sealed class ExpiredReminder : ExpiryReminder
{
    internal ExpiredReminder(FoodItem item, int daysLeft) : base(item, daysLeft) { }

    public override string Status => "Kedaluwarsa";
    public override int Priority => 0;
    public override string Message =>
        $"{NamaMakanan} ({UnitText}) sudah lewat {-DaysLeft} hari. Periksa kondisinya; catat sebagai Dibuang bila tidak layak.";
}
```

`TodayReminder` dan `SoonReminder` mengikuti pola yang sama dengan pesan berbeda. `SafeReminder` menimpa `NeedsAttention` menjadi `false` dan **tidak** menimpa `Message`, sehingga memakai implementasi virtual dari kelas dasar.

### 1.2 Method proses: `BuildReminders`

File: `Services/ExpiryReminderService.cs`

```csharp
public static List<ExpiryReminder> BuildReminders(IEnumerable<FoodItem> foods, DateTime today) =>
    foods.Where(HasStock)
        .Select(food => ExpiryReminder.Create(food, today))
        .Where(reminder => reminder.NeedsAttention)
        .OrderBy(reminder => reminder.Priority)
        .ThenBy(reminder => reminder.DaysLeft)
        .ThenBy(reminder => reminder.NamaMakanan, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

private static bool HasStock(FoodItem food) => food.Jumlah > 0;
```

Alur method:

1. Bahan yang stoknya 0 diabaikan (`HasStock`).
2. Setiap bahan diubah menjadi objek `ExpiryReminder` melalui factory `Create()`. Objek yang dibuat sebenarnya adalah salah satu kelas turunan.
3. Bahan yang tidak perlu diperhatikan (`NeedsAttention == false`, yaitu `SafeReminder`) disaring.
4. Daftar diurutkan berdasarkan `Priority`, lalu sisa hari, lalu nama bahan.

### 1.3 Penerapan pilar OOP

| Pilar | Penerapan di FoodSave |
| --- | --- |
| **Inheritance** | Empat kelas pengingat diturunkan dari kelas dasar `ExpiryReminder` dan memakai ulang `Item`, `DaysLeft`, properti binding, serta helper `UnitText`. Constructor turunan memanggil constructor basis dengan `: base(item, daysLeft)`. |
| **Polymorphism** | `BuildReminders()` dan Dashboard hanya mengenal tipe `ExpiryReminder`. Nilai `Status`, `Priority`, `NeedsAttention`, dan `Message` yang dipakai ditentukan oleh kelas turunan saat runtime (method `abstract`/`virtual` yang di-`override`). |
| **Abstract class** | `ExpiryReminder` tidak dapat dibuat langsung; `Status` dan `Priority` wajib diimplementasikan oleh turunan. |
| **Sealed class** | Kelas turunan dinyatakan `sealed` agar tidak diturunkan lagi, sesuai skenario sealed class pada modul. |
| **Encapsulation** | Pemilihan kelas turunan disembunyikan di `Create()`; constructor turunan bersifat `internal`, dan aturan stok di `BuildReminders()` disimpan di method `private`. |

## 2. Penerapan access modifier

FoodSave menerapkan keempat access modifier yang dibahas di Lab 9.2:

| Access modifier | Lokasi | Alasan |
| --- | --- | --- |
| `public` | `ExpiryReminder.Create()`, `ExpiryReminderService.BuildReminders()`, properti `Status`/`Message` | Diakses UI (`MainWindow`) dan binding DataGrid. |
| `private` | `ExpiryReminderService.HasStock()`; `FoodSaveRepository._connectionString` dan `OpenAsync()` (Modul 8) | Detail internal yang tidak perlu diketahui pemanggil. |
| `protected` | Constructor `ExpiryReminder(...)` dan properti `UnitText` | Hanya kelas turunan yang boleh membuat objek dasar dan memakai helper format jumlah. |
| `internal` | Constructor `ExpiredReminder`, `TodayReminder`, `SoonReminder`, `SafeReminder`; konstanta `ExpiryStatus.SoonThresholdDays` | Objek pengingat hanya dibuat di dalam assembly FoodSave melalui `Create()`; batas 3 hari dipakai bersama `ExpiryStatus` dan `ExpiryReminder` tanpa dibuka ke assembly lain. |

Konstanta `SoonThresholdDays` juga memastikan `ExpiryStatus.For()` (dipakai tab Stok) dan `ExpiryReminder` (dipakai Dashboard) memakai batas hari yang sama.

## 3. Integrasi dengan aplikasi

Pada `Views/MainWindow.xaml.cs`, daftar **Prioritas digunakan** di Dashboard sekarang dibentuk oleh method baru:

```csharp
// sebelumnya:
// var priority = active.Where(f => f.Status != "Aman").OrderBy(f => f.TanggalKedaluwarsa).ToList();
var priority = ExpiryReminderService.BuildReminders(active, DateTime.Today);
PriorityGrid.ItemsSource = priority;
```

Pada `Views/MainWindow.xaml`, tabel prioritas mendapat kolom baru **Pengingat** yang menampilkan `Message` dari masing-masing kelas turunan. Warna baris tetap mengikuti `Status`. Kolom lain (`NamaMakanan`, `Jumlah`, `TanggalKedaluwarsa`) tetap dapat di-binding karena `ExpiryReminder` meneruskan properti tersebut dari `FoodItem`.

Tidak ada perubahan skema database, repository, atau alur login/stok.

## 4. Pengujian

Pemeriksaan baru ditambahkan pada runner `FoodSave.Tests/Program.cs` (berlabel `M9:`):

| Pemeriksaan | Harapan |
| --- | --- |
| Bahan lewat tanggal / hari ini / 3 hari lagi / 4 hari lagi | Menghasilkan `ExpiredReminder` / `TodayReminder` / `SoonReminder` / `SafeReminder` |
| Status turunan untuk −5 sampai +10 hari | Sama dengan `ExpiryStatus.For()` |
| Pesan `ExpiredReminder` stok 2, lewat 2 hari | Memuat "sudah lewat 2 hari" dan "2 unit" (override) |
| `SafeReminder` | `NeedsAttention == false` dan memakai pesan dasar "masih aman" |
| `BuildReminders` dengan Roti (+2), Beras (+30), Tahu (0), Susu (−1), Keju (stok 0) | Urutan Susu → Tahu → Roti; Beras dan Keju tidak muncul |

Perintah (dari folder `foodsave`):

```bash
dotnet build FoodSave.csproj
dotnet run --project FoodSave.Tests/FoodSave.Tests.csproj
```

**Hasil aktual:** _belum dijalankan — isi setelah build dan runner dijalankan (tempel ringkasan output `OK: M9: ...`)._

## 5. Lampiran bukti yang perlu disertakan

- [ ] Screenshot `Models/ExpiryReminder.cs`: kelas abstrak, `override`, `sealed`, constructor `protected`/`internal`.
- [ ] Screenshot `Services/ExpiryReminderService.cs`: method `BuildReminders()` dan method `private HasStock()`.
- [ ] Screenshot terminal: build berhasil dan output `OK: M9: ...`.
- [ ] Screenshot Dashboard pada Windows: tabel **Prioritas digunakan** dengan kolom **Pengingat** untuk bahan kedaluwarsa, hari ini, dan segera kedaluwarsa (gunakan akun demonstrasi).
