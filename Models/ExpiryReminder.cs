namespace FoodSave.Models;

/// <summary>
/// Pengingat kedaluwarsa untuk satu bahan makanan (Modul 9).
/// Kelas dasar abstrak: setiap tingkat urgensi adalah kelas turunan yang
/// menimpa (override) status, prioritas, dan pesan pengingatnya sendiri.
/// </summary>
public abstract class ExpiryReminder
{
    // protected: constructor hanya dapat dipanggil oleh kelas turunan.
    protected ExpiryReminder(FoodItem item, int daysLeft)
    {
        Item = item;
        DaysLeft = daysLeft;
    }

    public FoodItem Item { get; }

    /// <summary>Selisih hari menuju tanggal kedaluwarsa; negatif berarti sudah lewat.</summary>
    public int DaysLeft { get; }

    // Diteruskan dari Item agar DataGrid dapat melakukan binding langsung.
    public string NamaMakanan => Item.NamaMakanan;
    public int Jumlah => Item.Jumlah;
    public DateTime TanggalKedaluwarsa => Item.TanggalKedaluwarsa;

    /// <summary>Label status yang ditampilkan di UI (abstract: wajib diisi turunan).</summary>
    public abstract string Status { get; }

    /// <summary>Urutan prioritas; 0 paling mendesak (abstract: wajib diisi turunan).</summary>
    public abstract int Priority { get; }

    /// <summary>Apakah bahan perlu muncul di daftar prioritas (virtual: boleh ditimpa).</summary>
    public virtual bool NeedsAttention => true;

    /// <summary>Pesan pengingat bawaan (virtual: turunan boleh menimpa).</summary>
    public virtual string Message =>
        $"{NamaMakanan} ({UnitText}) masih aman sampai {TanggalKedaluwarsa:dd MMM yyyy}.";

    // protected: helper yang hanya dipakai kelas ini dan turunannya.
    protected string UnitText => $"{Jumlah} unit";

    /// <summary>
    /// Factory: memilih kelas turunan yang tepat berdasarkan sisa hari.
    /// Pemanggil cukup memegang tipe dasar <see cref="ExpiryReminder"/> (polymorphism).
    /// </summary>
    public static ExpiryReminder Create(FoodItem item, DateTime today)
    {
        var days = (item.TanggalKedaluwarsa.Date - today.Date).Days;
        if (days < 0) return new ExpiredReminder(item, days);
        if (days == 0) return new TodayReminder(item, days);
        if (days <= ExpiryStatus.SoonThresholdDays) return new SoonReminder(item, days);
        return new SafeReminder(item, days);
    }
}

// sealed: tingkat urgensi ini tidak boleh diturunkan lagi.
// internal constructor: objek hanya dibuat di dalam assembly FoodSave (melalui Create).

public sealed class ExpiredReminder : ExpiryReminder
{
    internal ExpiredReminder(FoodItem item, int daysLeft) : base(item, daysLeft) { }

    public override string Status => "Kedaluwarsa";
    public override int Priority => 0;
    public override string Message =>
        $"{NamaMakanan} ({UnitText}) sudah lewat {-DaysLeft} hari. Periksa kondisinya; catat sebagai Dibuang bila tidak layak.";
}

public sealed class TodayReminder : ExpiryReminder
{
    internal TodayReminder(FoodItem item, int daysLeft) : base(item, daysLeft) { }

    public override string Status => "Hari ini";
    public override int Priority => 1;
    public override string Message =>
        $"{NamaMakanan} ({UnitText}) kedaluwarsa hari ini. Gunakan hari ini juga.";
}

public sealed class SoonReminder : ExpiryReminder
{
    internal SoonReminder(FoodItem item, int daysLeft) : base(item, daysLeft) { }

    public override string Status => "Segera kedaluwarsa";
    public override int Priority => 2;
    public override string Message =>
        $"{NamaMakanan} ({UnitText}) kedaluwarsa dalam {DaysLeft} hari. Cari resep yang memakai bahan ini.";
}

public sealed class SafeReminder : ExpiryReminder
{
    internal SafeReminder(FoodItem item, int daysLeft) : base(item, daysLeft) { }

    public override string Status => "Aman";
    public override int Priority => 3;
    public override bool NeedsAttention => false;
    // Message tidak di-override: memakai implementasi virtual dari kelas dasar.
}
