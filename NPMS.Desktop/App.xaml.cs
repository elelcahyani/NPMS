using Microsoft.EntityFrameworkCore;
using NPMS.Core.Data;
using NPMS.Core.Services;
using System.IO;
using System.Windows;

namespace NPMS.Desktop
{
    public partial class App : Application
    {
        private NpmsDbContext? _db;
        private ProductService? _service;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InitDb();
            ShowLogin();
        }

        private void InitDb()
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "npms_desktop.db");
            var options = new DbContextOptionsBuilder<NpmsDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            _db = new NpmsDbContext(options);
            NpmsDbContext.SeedDatabase(_db);
            _service = new ProductService(_db);
        }

        private void ShowLogin()
        {
            var loginWindow = new LoginWindow(_service!);
            var result = loginWindow.ShowDialog();

            if (result != true || loginWindow.LoggedInUser == null)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow(_service!, loginWindow.LoggedInUser);
            mainWindow.Show();
        }

        public void RestartToLogin()
        {
            // Close MainWindow first
            foreach (Window w in Windows)
                if (w is MainWindow) { w.Close(); break; }

            // HIGH #10: Dispose old db context and service before creating new ones
            // This prevents connection/memory leaks across multiple login sessions
            _db?.Dispose();
            _db = null;
            _service = null;

            InitDb();
            ShowLogin();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _db?.Dispose();
            base.OnExit(e);
        }

        public NpmsDbContext GetDb() => _db!;
    }
}
