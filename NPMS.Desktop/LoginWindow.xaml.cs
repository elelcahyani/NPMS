using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System.Windows;
using System.Windows.Input;

namespace NPMS.Desktop
{
    public partial class LoginWindow : Window
    {
        private readonly IProductService _service;
        public LoginResponse? LoggedInUser { get; private set; }

        public LoginWindow(IProductService service)
        {
            InitializeComponent();
            _service = service;
            TxtUsername.Focus();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryLogin();
        }

        private void TryLogin()
        {
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Username dan password harus diisi.");
                return;
            }

            var result = _service.Authenticate(username, password);
            if (!result.Success)
            {
                ShowError(result.Message);
                TxtPassword.Clear();
                return;
            }

            LoggedInUser = result;
            DialogResult = true;
            Close();
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
