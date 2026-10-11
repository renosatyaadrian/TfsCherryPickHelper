using System.Windows;
using TfsCherryPickHelper.Services;

namespace TfsCherryPickHelper;

public partial class LoginWindow : Window
{
    /// <summary>
    /// Invoked after credentials are saved successfully.
    /// </summary>
    public event EventHandler? CredentialsSaved;

    public LoginWindow()
    {
        InitializeComponent();

        var settings = ConfigService.Load();
        UsernameBox.Text = settings.Username;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(this, "Username and Password are required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = ConfigService.Load();
        settings.Username = username;
        settings.Password = password;
        ConfigService.Save(settings);

        CredentialsSaved?.Invoke(this, EventArgs.Empty);

        DialogResult = true;
        Close();
    }
}