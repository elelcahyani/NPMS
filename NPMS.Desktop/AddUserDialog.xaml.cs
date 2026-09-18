using NPMS.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class AddUserDialog : Window
    {
        private readonly List<Role> _roles;
        public string NewUsername { get; private set; } = "";
        public string NewPassword { get; private set; } = "";
        public int SelectedRoleId { get; private set; }

        public AddUserDialog(List<Role> roles)
        {
            InitializeComponent();
            _roles = roles;
            foreach (var r in roles)
                CmbRole.Items.Add(new ComboBoxItem { Content = r.RoleName, Tag = r.RoleId });
            if (CmbRole.Items.Count > 0) CmbRole.SelectedIndex = 0;
            TxtUsername.Focus();
            TxtUsername.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnAdd_Click(s, e); };
            TxtPassword.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) BtnAdd_Click(s, e); };
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            // Validation: username
            if (string.IsNullOrWhiteSpace(username))
            { ShowError("Username harus diisi."); return; }
            if (username.Length < 3)
            { ShowError("Username minimal 3 karakter."); return; }
            if (username.Length > 50)
            { ShowError("Username maksimal 50 karakter."); return; }
            // Only allow alphanumeric + underscore + hyphen
            if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_\-]+$"))
            { ShowError("Username hanya boleh mengandung huruf, angka, underscore, atau tanda hubung."); return; }

            // Validation: password
            if (password.Length < 8)
            { ShowError("Password minimal 8 karakter."); return; }

            if (CmbRole.SelectedItem is not ComboBoxItem selected)
            { ShowError("Pilih role terlebih dahulu."); return; }

            NewUsername = username;
            NewPassword = password;
            SelectedRoleId = (int)selected.Tag;
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
