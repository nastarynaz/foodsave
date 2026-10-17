using System.Windows;
using System.Windows.Controls;
using FoodSave.Models;

namespace FoodSave.Views;

public partial class UsageDialog : Window
{
    private readonly int _stock;
    public int Amount { get; private set; }
    public string Status { get; private set; } = "";

    public UsageDialog(FoodItem food)
    {
        InitializeComponent();
        _stock = food.Jumlah;
        Heading.Text = food.NamaMakanan + " • stok " + food.Jumlah;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AmountBox.Text, out var amount) || amount <= 0 || amount > _stock)
        {
            ErrorText.Text = "Jumlah harus antara 1 dan " + _stock + ".";
            return;
        }
        Amount = amount;
        Status = ((ComboBoxItem)StatusBox.SelectedItem).Content.ToString() ?? "Digunakan";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
