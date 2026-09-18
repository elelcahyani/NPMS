using NPMS.Core.Services;
using System.Windows;

namespace NPMS.Desktop
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly IProductService _service;
        private readonly int _userId;

        public ChangePasswordWindow(IProductService service, int userId)
        {
            InitializeComponent();
            _service = service;
            _userId = userId;
            TxtCurrentPwd.Focus();
            // Enter key moves focus forward through fields
            TxtCurrentPwd.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) TxtNewPwd.Focus(); };
            TxtNewPwd.KeyDown    += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) TxtConfirmPwd.Focus(); };
            TxtConfirmPwd.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnSave_Click(s, e); };
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var current = TxtCurrentPwd.Password;
            var newPwd = TxtNewPwd.Password;
            var confirm = TxtConfirmPwd.Password;

            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(newPwd))
            {
                ShowError("Semua field harus diisi.");
                return;
            }
            if (newPwd.Length < 8)
            {
                ShowError("Password baru minimal 8 karakter.");
                return;
            }
            if (newPwd != confirm)
            {
                ShowError("Konfirmasi password tidak cocok.");
                return;
            }

            var success = _service.ChangePassword(_userId, current, newPwd);
            if (!success)
            {
                ShowError("Password saat ini tidak benar.");
                TxtCurrentPwd.Clear();
                TxtCurrentPwd.Focus();
                return;
            }

            MessageBox.Show("Password berhasil diubah.", "Sukses",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
