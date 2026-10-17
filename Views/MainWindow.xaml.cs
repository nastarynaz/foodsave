using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FoodSave.Models;
using FoodSave.Services;

namespace FoodSave.Views;

public partial class MainWindow : Window
{
    private readonly FoodSaveRepository _repository;
    private readonly TheMealDBApiService _recipes = new();
    private readonly User _user;
    private List<FoodItem> _foods = new();
    private bool _ready;

    public MainWindow(FoodSaveRepository repository, User user)
    {
        InitializeComponent();
        _repository = repository;
        _user = user;
        GreetingText.Text = "Halo, " + user.Nama;
        ProfileName.Text = user.Nama;
        ProfileEmail.Text = user.Email;
        Loaded += async (_, _) =>
        {
            _ready = true;
            await RefreshAsync();
        };
    }

    private async Task RefreshAsync()
    {
        try
        {
            _foods = await _repository.GetFoodsAsync(_user.UserID);
            var active = _foods.Where(f => f.Jumlah > 0).ToList();
            StockCountText.Text = active.Count.ToString();
            ExpiringCountText.Text = active.Count(f => f.Status is "Hari ini" or "Segera kedaluwarsa").ToString();
            ExpiredCountText.Text = active.Count(f => f.Status == "Kedaluwarsa").ToString();
            var priority = active.Where(f => f.Status != "Aman").OrderBy(f => f.TanggalKedaluwarsa).ToList();
            PriorityGrid.ItemsSource = priority;
            PriorityEmptyText.Visibility = priority.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RebuildCategories();
            ApplyFilter();

            var usage = await _repository.GetUsageAsync(_user.UserID);
            UsageGrid.ItemsSource = usage;
            var used = usage.Where(h => h.StatusPenggunaan == "Digunakan").Sum(h => h.JumlahTerpakai);
            var wasted = usage.Where(h => h.StatusPenggunaan == "Dibuang").Sum(h => h.JumlahTerpakai);
            StatisticsText.Text = $"Digunakan: {used} unit     •     Dibuang: {wasted} unit";
            RecipeHistoryGrid.ItemsSource = await _repository.GetRecipeHistoryAsync(_user.UserID);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RebuildCategories()
    {
        var selected = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        CategoryFilter.Items.Clear();
        CategoryFilter.Items.Add(new ComboBoxItem { Content = "Semua kategori" });
        foreach (var category in _foods.Select(f => f.Kategori).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c))
            CategoryFilter.Items.Add(new ComboBoxItem { Content = category });
        CategoryFilter.SelectedItem = CategoryFilter.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == selected) ?? CategoryFilter.Items[0];
    }

    private void ApplyFilter()
    {
        if (!_ready) return;
        var search = SearchBox.Text.Trim();
        var category = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var status = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filtered = _foods.Where(f => f.NamaMakanan.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            && (category is null or "Semua kategori" || f.Kategori == category)
            && (status is null or "Semua status" || f.Status == status)).ToList();
        FoodsGrid.ItemsSource = filtered;
        StockEmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Changed(object sender, EventArgs e) => ApplyFilter();

    private async void AddFood_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FoodDialog { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        try { await _repository.SaveFoodAsync(_user.UserID, dialog.Result); await RefreshAsync(); MainTabs.SelectedIndex = 1; }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void EditFood_Click(object sender, RoutedEventArgs e)
    {
        if (FoodsGrid.SelectedItem is not FoodItem food) { SelectFoodMessage(); return; }
        var dialog = new FoodDialog(food) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result == null) return;
        try { await _repository.SaveFoodAsync(_user.UserID, dialog.Result); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void UseFood_Click(object sender, RoutedEventArgs e)
    {
        if (FoodsGrid.SelectedItem is not FoodItem food) { SelectFoodMessage(); return; }
        if (food.Jumlah == 0) { MessageBox.Show(this, "Stok bahan ini sudah habis."); return; }
        var dialog = new UsageDialog(food) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { await _repository.RecordUsageAsync(_user.UserID, food.FoodID, dialog.Amount, dialog.Status); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void DeleteFood_Click(object sender, RoutedEventArgs e)
    {
        if (FoodsGrid.SelectedItem is not FoodItem food) { SelectFoodMessage(); return; }
        if (MessageBox.Show(this, $"Hapus {food.NamaMakanan} dari daftar stok? Riwayatnya tetap tersimpan.", "Konfirmasi hapus", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _repository.ArchiveFoodAsync(_user.UserID, food.FoodID); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void GoRecipes_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 2;

    private async void LoadRecipes_Click(object sender, RoutedEventArgs e)
    {
        RecipeMessage.Text = "Mencari resep…";
        RecipesGrid.ItemsSource = null;
        try
        {
            var recipes = await _recipes.FetchRecipesAsync(_foods);
            RecipesGrid.ItemsSource = recipes;
            RecipeMessage.Text = recipes.Count == 0
                ? "Belum ada resep yang cocok. Coba tambah bahan atau gunakan nama bahan dalam bahasa Inggris."
                : $"{recipes.Count} resep ditemukan. Pilih resep untuk melihat bahan dan langkahnya.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            RecipeMessage.Text = "Gagal mengambil resep. Periksa koneksi internet dan coba lagi.";
        }
    }

    private async void RecipesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecipesGrid.SelectedItem is not Recipe recipe) return;
        RecipeTitle.Text = recipe.NamaResep;
        MatchedText.Text = "Ada di stok: " + (recipe.MatchedIngredients.Count == 0 ? "belum ada yang terdeteksi" : string.Join(", ", recipe.MatchedIngredients));
        MissingText.Text = "Bahan tambahan: " + (recipe.MissingIngredients.Count == 0 ? "tidak ada yang terdeteksi" : string.Join(", ", recipe.MissingIngredients));
        InstructionsText.Text = recipe.Instructions;
        RecipeImage.Source = Uri.TryCreate(recipe.ImageURL, UriKind.Absolute, out var uri) ? new BitmapImage(uri) : null;
        try { await _repository.RecordRecipeAccessAsync(_user.UserID, recipe); RecipeHistoryGrid.ItemsSource = await _repository.GetRecipeHistoryAsync(_user.UserID); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.Source != MainTabs) return;
        if (MainTabs.SelectedIndex == 3) await RefreshAsync();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void SelectFoodMessage() => MessageBox.Show(this, "Pilih bahan pada tabel stok terlebih dahulu.");
    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "FoodSave", MessageBoxButton.OK, MessageBoxImage.Warning);
}
