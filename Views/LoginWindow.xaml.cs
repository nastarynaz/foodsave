using System.Windows;
using FoodSave.Services;

namespace FoodSave.Views;

public partial class LoginWindow : Window
{
    private FoodSaveRepository? _repository;
    private bool _register;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                _repository = new FoodSaveRepository();
                await _repository.InitializeAsync();
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Database belum siap: " + ex.Message;
                SubmitButton.IsEnabled = false;
            }
        };
    }

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        _register = !_register;
        NamePanel.Visibility = _register ? Visibility.Visible : Visibility.Collapsed;
        ModeTitle.Text = _register ? "Buat akun" : "Masuk ke akun";
        SubmitButton.Content = _register ? "Daftar" : "Masuk";
        SwitchButton.Content = _register ? "Sudah punya akun? Masuk" : "Belum punya akun? Daftar";
        ErrorText.Text = "";
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (_repository == null) return;
        SubmitButton.IsEnabled = false;
        ErrorText.Text = "";
        try
        {
            var user = _register
                ? await _repository.RegisterAsync(NameBox.Text, EmailBox.Text, PasswordInput.Password)
                : await _repository.LoginAsync(EmailBox.Text, PasswordInput.Password);
            if (user == null) { ErrorText.Text = "Email atau kata sandi salah."; return; }
            var main = new MainWindow(_repository, user);
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }
}
