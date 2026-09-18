using NPMS.Core.DTOs;
using NPMS.Core.Models;
using NPMS.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NPMS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly IProductService _service;
        private readonly LoginResponse _currentUser;
        private List<ProductListDto> _products = new();
        private ProductDetailDto? _selectedProduct;
        private Timer? _searchDebounce;

        public MainWindow(IProductService service, LoginResponse currentUser)
        {
            InitializeComponent();
            _service = service;
            _currentUser = currentUser;
            ApplyRoleUI();
            LoadProducts();
            Closed += (s, e) => _searchDebounce?.Dispose();
        }

        // ─── ROLE UI ──────────────────────────────────────────────────────────

        private void ApplyRoleUI()
        {
            TxtUserName.Text = _currentUser.Username;
            TxtUserRole.Text = _currentUser.RoleName;

            // Add Product button — R&D Team only
            BtnAddProductBorder.Visibility = _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;

            // Store nav — Super Admin (read) + Store Manager (crud)
            BtnNavStore.IsEnabled = _currentUser.CanAccessStore;
            BtnNavStore.Opacity   = _currentUser.CanAccessStore ? 1.0 : 0.35;

            // Account Settings nav — Super Admin only
            BtnNavSettings.IsEnabled = _currentUser.CanAccessSettings;
            BtnNavSettings.Opacity   = _currentUser.CanAccessSettings ? 1.0 : 0.35;
        }

        // ─── PRODUCT LIST ─────────────────────────────────────────────────────

        private void LoadProducts(string query = "", string status = "", string factory = "", string partType = "", string family = "")
        {
            _products = _service.SearchProducts(query, status, factory);
            if (!string.IsNullOrWhiteSpace(partType) && partType != "All Part Numbers")
                _products = _products.Where(p => p.ProductType == partType).ToList();
            if (!string.IsNullOrWhiteSpace(family) && family != "All Product Families")
                _products = _products.Where(p => p.ProductFamily == family).ToList();
            GridProducts.ItemsSource = _products;
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch.Text == "Search Part Number or Product Name...")
            {
                TxtSearch.Text = "";
                TxtSearch.Foreground = Brushes.Black;
            }
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = "Search Part Number or Product Name...";
                TxtSearch.Foreground = Brushes.Gray;
            }
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;

            // Debounce: wait 300ms after last keystroke before querying
            _searchDebounce?.Dispose();
            _searchDebounce = new Timer(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var q = TxtSearch.Text == "Search Part Number or Product Name..." ? "" : TxtSearch.Text;
                    LoadProducts(q, GetComboVal(CmbFilterStatus), GetComboVal(CmbFilterFactory),
                        GetComboVal(CmbFilterPartType), GetComboVal(CmbFilterFamily));
                });
            }, null, 300, Timeout.Infinite);
        }

        private string GetComboVal(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem item)
            {
                var val = item.Content.ToString() ?? "";
                if (val is "Status" or "Factory" or "All Statuses" or "All Factories" or "All Part Numbers" or "All Product Families")
                    return "";
                return val;
            }
            return "";
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var q = TxtSearch.Text == "Search Part Number or Product Name..." ? "" : TxtSearch.Text;
            LoadProducts(q, GetComboVal(CmbFilterStatus), GetComboVal(CmbFilterFactory), GetComboVal(CmbFilterPartType), GetComboVal(CmbFilterFamily));
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "Search Part Number or Product Name...";
            TxtSearch.Foreground = Brushes.Gray;
            CmbFilterPartType.SelectedIndex = 0;
            CmbFilterFamily.SelectedIndex = 0;
            CmbFilterStatus.SelectedIndex = 0;
            CmbFilterFactory.SelectedIndex = 0;
            LoadProducts();
        }

        private void GridProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridProducts.SelectedItem is not ProductListDto item) return;

            // Ignore clicks on empty area or column headers
            var originalSource = e.OriginalSource as System.Windows.DependencyObject;
            while (originalSource != null)
            {
                if (originalSource is DataGridRow) break;
                if (originalSource is System.Windows.Controls.Primitives.DataGridColumnHeader) return;
                originalSource = System.Windows.Media.VisualTreeHelper.GetParent(originalSource);
            }
            if (originalSource == null) return;

            ShowProductDetail(item.ProductId);
        }

        // ─── PRODUCT DETAIL ───────────────────────────────────────────────────

        private void ShowProductDetail(int productId)
        {
            _selectedProduct = _service.GetProductById(productId);
            if (_selectedProduct == null) return;

            // Header
            BreadcrumbProduct.Text = _selectedProduct.PartNumber;
            DetailTitle.Text = _selectedProduct.ProductName.ToUpper();

            // Core fields
            TxtDetailPn.Text = _selectedProduct.PartNumber;
            TxtDetailName.Text = _selectedProduct.ProductName;
            TxtDetailFamily.Text = _selectedProduct.ProductFamily;
            TxtDetailGroup.Text = _selectedProduct.ProductGroup;
            TxtDetailType.Text = _selectedProduct.ProductType;
            TxtDetailRevision.Text = _selectedProduct.CurrentRevision;
            TxtDetailStatus.Text = _selectedProduct.Status;
            TxtDetailStatus.Foreground = _selectedProduct.Status switch
            {
                ProductStatus.Released      => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                ProductStatus.PendingReview => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                ProductStatus.Obsolete      => new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                ProductStatus.Discontinued  => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                ProductStatus.InDevelopment => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                _                           => new SolidColorBrush(Color.FromRgb(107, 114, 128))
            };            TxtDetailDesc.Text = string.IsNullOrWhiteSpace(_selectedProduct.Description) ? "—" : _selectedProduct.Description;
            TxtDetailCreatedBy.Text = _selectedProduct.CreatedBy;
            TxtDetailUpdatedAt.Text = _selectedProduct.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

            // Product image
            LoadProductImage(_selectedProduct.ImagePath);

            // Manufacturing
            if (_selectedProduct.ManufacturingInfo != null)
            {
                TxtDetailFactory.Text = _selectedProduct.ManufacturingInfo.Factory;
                TxtDetailLine.Text = _selectedProduct.ManufacturingInfo.ProductionLine;
                TxtDetailProdType.Text = _selectedProduct.ManufacturingInfo.ProductionType;
                TxtDetailMfgNotes.Text = _selectedProduct.ManufacturingInfo.ManufacturingNotes;
            }

            // Specification
            if (_selectedProduct.Specification != null)
            {
                SpecLength.Text = _selectedProduct.Specification.Length;
                SpecLenTol.Text = _selectedProduct.Specification.LengthTolerance;
                SpecWidth.Text = _selectedProduct.Specification.Width;
                SpecWidTol.Text = _selectedProduct.Specification.WidthTolerance;
                SpecHeight.Text = _selectedProduct.Specification.Height;
                SpecHgtTol.Text = _selectedProduct.Specification.HeightTolerance;
                SpecInductance.Text = _selectedProduct.Specification.Inductance;
                SpecIndTol.Text = _selectedProduct.Specification.InductanceTolerance;
                SpecIsaat.Text = _selectedProduct.Specification.Isaat;
                SpecIsaatTol.Text = _selectedProduct.Specification.IsaatTolerance;
                SpecDcr.Text = _selectedProduct.Specification.Dcr;
                SpecDcrTol.Text = _selectedProduct.Specification.DcrTolerance;
            }

            // Process tabs
            BuildDetailProcessTabs(_selectedProduct.Processes);

            // Documents — set Tag for delete button visibility
            DetailDocList.Tag = _currentUser.CanEditProduct ? Visibility.Visible : Visibility.Collapsed;
            DetailDocList.ItemsSource = _selectedProduct.Documents;

            // Materials
            DetailMaterialList.ItemsSource = _selectedProduct.Materials;

            // Role-based edit/delete/upload buttons — R&D Team only
            if (_currentUser.CanEditProduct)
            {
                BtnEditProductBorder.Visibility   = Visibility.Visible;
                BtnDeleteProductBorder.Visibility = Visibility.Visible;
                BtnUploadDocBorder.Visibility     = Visibility.Visible;
                BadgeAccess.Visibility            = Visibility.Collapsed;
            }
            else
            {
                BtnEditProductBorder.Visibility   = Visibility.Collapsed;
                BtnDeleteProductBorder.Visibility = Visibility.Collapsed;
                BtnUploadDocBorder.Visibility     = Visibility.Collapsed;
                BadgeAccess.Visibility            = Visibility.Visible;
            }

            ViewList.Visibility   = Visibility.Collapsed;
            ViewDetail.Visibility = Visibility.Visible;
        }

        private void LoadProductImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
            {
                ImgProduct.Visibility = Visibility.Collapsed;
                TxtNoImage.Visibility = Visibility.Visible;
                return;
            }
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.EndInit();
                ImgProduct.Source = bmp;
                ImgProduct.Visibility = Visibility.Visible;
                TxtNoImage.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ImgProduct.Visibility = Visibility.Collapsed;
                TxtNoImage.Visibility = Visibility.Visible;
            }
        }

        // ─── PROCESS TABS ─────────────────────────────────────────────────────

        private void BuildDetailProcessTabs(List<ProcessStepDto> processes)
        {
            DetailProcessTabBar.Children.Clear();
            DetailProcessPanel.Visibility = Visibility.Collapsed;
            if (processes == null || processes.Count == 0) return;

            for (int i = 0; i < processes.Count; i++)
            {
                int idx = i;
                var rb = new RadioButton
                {
                    Content = processes[i].ProcessName,
                    Style = (Style)FindResource("ProcessTabStyle"),
                    GroupName = "DetailProcessTabs"
                };
                rb.Checked += (s, e) => ShowDetailProcess(processes[idx]);
                DetailProcessTabBar.Children.Add(rb);
            }

            if (DetailProcessTabBar.Children[0] is RadioButton first)
                first.IsChecked = true;
        }

        private void ShowDetailProcess(ProcessStepDto step)
        {
            DetailProcessName.Text = step.ProcessName;
            DetailMachine.Text = string.IsNullOrWhiteSpace(step.MachineName) ? "—" : step.MachineName;
            DetailTooling.Text = string.IsNullOrWhiteSpace(step.ToolingName) ? "—" : step.ToolingName;
            DetailProcessDesc.Text = string.IsNullOrWhiteSpace(step.ProcessDescription) ? "—" : step.ProcessDescription;
            DetailParamList.ItemsSource = step.Parameters;
            DetailProcessPanel.Visibility = Visibility.Visible;
        }

        // ─── DOCUMENT ACTIONS ─────────────────────────────────────────────────

        private void BtnOpenDoc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not DocumentDto doc) return;
            try
            {
                if (System.IO.File.Exists(doc.FilePath))
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(doc.FilePath) { UseShellExecute = true });
                else
                    MessageBox.Show($"File tidak ditemukan:\n{doc.FilePath}", "Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private void BtnDownloadDoc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not DocumentDto doc) return;
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = doc.FileName, Title = "Save Document" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                if (System.IO.File.Exists(doc.FilePath))
                    System.IO.File.Copy(doc.FilePath, dlg.FileName, true);
                else
                    MessageBox.Show("File sumber tidak ditemukan.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private void BtnUploadDoc_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null) return;

            var fileDlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Document",
                Filter = "All Files|*.*|PDF|*.pdf|Word|*.docx|Excel|*.xlsx"
            };
            if (fileDlg.ShowDialog() != true) return;

            var defaultName = System.IO.Path.GetFileNameWithoutExtension(fileDlg.FileName);
            var metaDlg = new UploadDocumentDialog(defaultName) { Owner = this };
            if (metaDlg.ShowDialog() != true) return;

            try
            {
                var bytes = System.IO.File.ReadAllBytes(fileDlg.FileName);
                _service.AddDocument(_selectedProduct.ProductId, metaDlg.DocumentName,
                    metaDlg.DocumentType, metaDlg.Revision,
                    System.IO.Path.GetFileName(fileDlg.FileName), bytes, _currentUser.Username);
                ShowProductDetail(_selectedProduct.ProductId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal upload: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteDocDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not DocumentDto doc) return;
            var confirm = MessageBox.Show($"Hapus dokumen '{doc.DocumentName}'?", "Konfirmasi",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            _service.DeleteDocument(doc.DocumentId);
            ShowProductDetail(_selectedProduct!.ProductId);
        }

        // ─── PRODUCT ACTIONS ──────────────────────────────────────────────────

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var form = new ProductFormWindow(_service, _currentUser.Username) { Owner = this };
            if (form.ShowDialog() == true)
            {
                LoadProducts();
                if (form.SavedProduct != null)
                    ShowProductDetail(form.SavedProduct.ProductId);
            }
        }

        private void BtnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null) return;
            var form = new ProductFormWindow(_service, _currentUser.Username, _selectedProduct) { Owner = this };
            if (form.ShowDialog() == true && form.SavedProduct != null)
            {
                LoadProducts();
                ShowProductDetail(form.SavedProduct.ProductId);
            }
        }

        private void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null) return;
            var confirm = MessageBox.Show(
                $"Hapus produk '{_selectedProduct.ProductName}' ({_selectedProduct.PartNumber})?\n\nTindakan ini tidak dapat dibatalkan.",
                "Hapus Produk", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            _service.DeleteProduct(_selectedProduct.ProductId);
            LoadProducts();
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
        }

        // ─── NAVIGATION ───────────────────────────────────────────────────────

        private void BtnBackToList_Click(object sender, RoutedEventArgs e)
        {
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
            LoadProducts();
        }

        private void NavStore_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessStore)
            {
                MessageBox.Show("Anda tidak memiliki akses ke menu Store.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MessageBox.Show("Store — Fitur sedang dalam pengembangan.",
                "Store", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessSettings)
            {
                MessageBox.Show("Anda tidak memiliki akses ke Account Settings.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var win = new AccountSettingsWindow(_service, _currentUser) { Owner = this };
            win.ShowDialog();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Logout dari sistem?", "Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            ((App)Application.Current).RestartToLogin();
        }
    }
}
