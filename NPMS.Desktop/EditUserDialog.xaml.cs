using NPMS.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class EditUserDialog : Window
    {
        public string EditedUsername { get; private set; } = "";
        public int SelectedRoleId { get; private set; }
        public bool IsAccountActive { get; private set; }
        public bool ChangePasswordRequested { get; private set; }

        public EditUserDialog(UserViewModel user, List<Role> roles)
        {
            InitializeComponent();
            TxtUsername.Text = user.Username;
            ChkActive.IsChecked = user.IsActive;
            foreach (var role in roles)
                CmbRole.Items.Add(new ComboBoxItem { Content = role.RoleName, Tag = role.RoleId });
            CmbRole.SelectedItem = CmbRole.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Content == user.RoleName);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var username = TxtUsername.Text.Trim();
            if (username.Length < 3 || username.Length > 50 ||
                !Regex.IsMatch(username, @"^[a-zA-Z0-9_\-]+$"))
            {
                ShowError("Username harus 3-50 karakter dan hanya boleh berisi huruf, angka, underscore, atau tanda hubung.");
                return;
            }
            if (CmbRole.SelectedItem is not ComboBoxItem role)
            {
                ShowError("Pilih role terlebih dahulu.");
                return;
            }

            EditedUsername = username;
            SelectedRoleId = (int)role.Tag;
            IsAccountActive = ChkActive.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            ChangePasswordRequested = true;
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
