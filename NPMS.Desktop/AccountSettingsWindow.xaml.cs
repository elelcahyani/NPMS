using NPMS.Core.Data;
using NPMS.Core.DTOs;
using NPMS.Core.Models;
using NPMS.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class AccountSettingsWindow : Window
    {
        private readonly IProductService _service;
        private readonly LoginResponse _currentUser;
        private readonly NpmsDbContext _db;
        private ObservableCollection<UserViewModel> _users = new();

        public AccountSettingsWindow(IProductService service, LoginResponse currentUser)
        {
            InitializeComponent();
            _service = service;
            _currentUser = currentUser;

            // Get DB context via App
            _db = ((App)Application.Current).GetDb();

            // Only Super Admin can manage users
            if (!currentUser.CanManageUsers)
                UserManagementPanel.Visibility = Visibility.Collapsed;

            LoadUsers();
        }

        private void LoadUsers()
        {
            _users.Clear();
            var users = _db.Users.ToList();
            var roles = _db.Roles.ToList();
            foreach (var u in users)
            {
                var role = roles.FirstOrDefault(r => r.RoleId == u.RoleId);
                _users.Add(new UserViewModel
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    RoleName = role?.RoleName ?? "Unknown",
                    IsActive = u.IsActive
                });
            }
            UserList.ItemsSource = _users;
        }

        private void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var roles = _db.Roles.ToList();
            var dlg = new AddUserDialog(roles) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            var newUser = new User
            {
                Username = dlg.NewUsername,
                PasswordHash = ProductService.HashPassword(dlg.NewPassword),
                RoleId = dlg.SelectedRoleId,
                IsActive = true
            };
            _db.Users.Add(newUser);
            _db.SaveChanges();
            LoadUsers();
        }

        private void BtnChangeUserPwd_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not UserViewModel vm) return;
            var dlg = new ChangePasswordWindow(_service, vm.UserId) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnEditUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not UserViewModel vm) return;

            var roles = _db.Roles.ToList();
            var dlg = new EditUserDialog(vm, roles) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            if (vm.UserId == _currentUser.UserId && !dlg.IsAccountActive)
            {
                MessageBox.Show("You cannot deactivate your own account.", "Not Allowed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var duplicate = _db.Users.Any(u =>
                u.UserId != vm.UserId &&
                u.Username.ToLower() == dlg.EditedUsername.ToLower());
            if (duplicate)
            {
                MessageBox.Show("Username tersebut sudah digunakan.", "Invalid Username",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = _db.Users.FirstOrDefault(u => u.UserId == vm.UserId);
            if (user == null) return;

            user.Username = dlg.EditedUsername;
            user.RoleId = dlg.SelectedRoleId;
            user.IsActive = dlg.IsAccountActive;
            _db.SaveChanges();
            LoadUsers();
        }

        private void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not UserViewModel vm) return;
            if (vm.UserId == _currentUser.UserId)
            {
                MessageBox.Show("You cannot delete your own account.", "Not Allowed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var confirm = MessageBox.Show($"Delete user '{vm.Username}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var user = _db.Users.FirstOrDefault(u => u.UserId == vm.UserId);
            if (user != null)
            {
                _db.Users.Remove(user);
                _db.SaveChanges();
                LoadUsers();
            }
        }
    }

    public class UserViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string RoleName { get; set; } = "";
        public bool IsActive { get; set; }
        public string StatusText => IsActive ? "Active" : "Inactive";
        public System.Windows.Media.Brush StatusColor => IsActive
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
    }
}
