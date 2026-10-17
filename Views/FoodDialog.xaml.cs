using System.Windows;
using System.Windows.Controls;
using FoodSave.Models;

namespace FoodSave.Views;

public partial class FoodDialog : Window
{
    private readonly int _foodId;
    public FoodItem? Result { get; private set; }

    public FoodDialog(FoodItem? source = null)
    {
        InitializeComponent();
        _foodId = source?.FoodID ?? 0;
        Heading.Text = source == null ? "Tambah bahan" : "Ubah bahan";
        NameBox.Text = source?.NamaMakanan ?? "";
        CategoryBox.Text = source?.Kategori ?? "";
        QuantityBox.Text = source?.Jumlah.ToString() ?? "1";
        PurchasePicker.SelectedDate = source?.TanggalPembelian ?? DateTime.Today;
        ExpiryPicker.SelectedDate = source?.TanggalKedaluwarsa ?? DateTime.Today.AddDays(7);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var category = CategoryBox.SelectedItem is ComboBoxItem selected ? selected.Content?.ToString() ?? "" : CategoryBox.Text;
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(category) ||
            !int.TryParse(QuantityBox.Text, out var amount) || amount <= 0 ||
            PurchasePicker.SelectedDate is not { } purchase || ExpiryPicker.SelectedDate is not { } expiry || expiry.Date < purchase.Date)
        {
            ErrorText.Text = "Isi nama, kategori, jumlah positif, dan tanggal yang benar.";
            return;
        }
        Result = new FoodItem { FoodID = _foodId, NamaMakanan = NameBox.Text, Kategori = category,
            Jumlah = amount, TanggalPembelian = purchase, TanggalKedaluwarsa = expiry };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
