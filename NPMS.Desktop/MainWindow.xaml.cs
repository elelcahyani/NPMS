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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NPMS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly IProductService _service;
        private readonly LoginResponse _currentUser;
        private List<ProductListDto> _products = new();
        private ProductDetailDto? _selectedProduct;
        private Timer? _searchDebounce;
        private List<StoreMaterialDto> _storeMaterials = new();
        private List<ToolingDto> _toolings = new();
        private List<UsageRequestDto> _usageRequests = new();
        private Timer? _materialSearchDebounce;
        private readonly DispatcherTimer _usageNoticeTimer = new();
        private string _usagePeriod = "Day";
        private string _toolingUsagePeriod = "Day";

        public MainWindow(IProductService service, LoginResponse currentUser)
        {
            InitializeComponent();
            _service = service;
            _currentUser = currentUser;
            ApplyRoleUI();
            PopulatePartNumberFilter();
            LoadProducts();
            _usageNoticeTimer.Interval = TimeSpan.FromSeconds(30);
            _usageNoticeTimer.Tick += (s, e) => UpdatePendingUsageNotice();
            _usageNoticeTimer.Start();
            UpdatePendingUsageNotice();
            Closed += (s, e) =>
            {
                _searchDebounce?.Dispose();
                _materialSearchDebounce?.Dispose();
                _usageNoticeTimer.Stop();
            };
        }

        private static string DisplaySpecificationValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value;

        // ─── ROLE UI ──────────────────────────────────────────────────────────

        private void ApplyRoleUI()
        {
            TxtUserName.Text = _currentUser.Username;
            TxtUserRole.Text = _currentUser.RoleName;

            // Add Product button — R&D Team only
            BtnAddProductBorder.Visibility = _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;

            // Add Material button — Store Manager & Super Admin
            BtnAddMaterialBorder.Visibility = _currentUser.CanEditStore
                ? Visibility.Visible : Visibility.Collapsed;
            StoreMaterialActionsColumn.Visibility = _currentUser.CanEditStore
                ? Visibility.Visible : Visibility.Collapsed;
            StoreMaterialRequestColumn.Visibility = _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;
            BtnAddToolingBorder.Visibility = _currentUser.CanEditStore
                ? Visibility.Visible : Visibility.Collapsed;
            ToolingActionsColumn.Visibility = _currentUser.CanEditStore
                ? Visibility.Visible : Visibility.Collapsed;
            ToolingRequestColumn.Visibility = _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;
            UsageRequestActionsColumn.Visibility = _currentUser.CanEditStore || _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;
            ProductActionGroup.Visibility = _currentUser.CanEditProduct
                ? Visibility.Visible : Visibility.Collapsed;

            // Store nav — Super Admin (read) + Store Manager (crud)
            BtnNavStore.IsEnabled = _currentUser.CanAccessStore;
            BtnNavStore.Opacity   = _currentUser.CanAccessStore ? 1.0 : 0.35;

            // Account Settings nav — Super Admin only
            BtnNavSettings.IsEnabled = _currentUser.CanAccessSettings;
            BtnNavSettings.Opacity   = _currentUser.CanAccessSettings ? 1.0 : 0.35;
        }

        // ─── PRODUCT LIST ─────────────────────────────────────────────────────

        public void PopulatePartNumberFilter()
        {
            var currentSelected = (CmbFilterPartType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var allProducts = _service.SearchProducts(null, null, null);
            var partNumbers = allProducts
                .Select(p => p.PartNumber)
                .Where(pn => !string.IsNullOrWhiteSpace(pn))
                .Distinct()
                .OrderBy(pn => pn)
                .ToList();

            CmbFilterPartType.SelectionChanged -= Filter_SelectionChanged;
            CmbFilterPartType.Items.Clear();
            var defaultItem = new ComboBoxItem { Content = "All Part Numbers", IsSelected = true };
            CmbFilterPartType.Items.Add(defaultItem);

            foreach (var pn in partNumbers)
            {
                var item = new ComboBoxItem { Content = pn };
                if (pn == currentSelected)
                {
                    defaultItem.IsSelected = false;
                    item.IsSelected = true;
                }
                CmbFilterPartType.Items.Add(item);
            }
            CmbFilterPartType.SelectionChanged += Filter_SelectionChanged;
        }

        private void LoadProducts(string query = "", string status = "", string factory = "", string partNumber = "", string family = "")
        {
            _products = _service.SearchProducts(query, status, factory);
            if (!string.IsNullOrWhiteSpace(partNumber) && partNumber != "All Part Numbers")
                _products = _products.Where(p => p.PartNumber.Equals(partNumber, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(family) && family != "All Product Families")
                _products = _products.Where(p => p.ProductFamily == family).ToList();
            
            GridProducts.ItemsSource = _products;
            
            if (EmptyStateMessage != null)
            {
                EmptyStateMessage.Visibility = _products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch.Text == "Search Part Number or Product Name...")
            {
                TxtSearch.Text = "";
                TxtSearch.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = "Search Part Number or Product Name...";
                TxtSearch.Foreground = new SolidColorBrush(Color.FromRgb(216, 180, 254));
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
                if (val is "Status" or "Factory" or "All" or "All dates" or "Status" or
                    "All Products" or "All Lots" or "All Part Numbers" or "All Product Families" or
                    "All Statuses" or "All Factories")
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
            TxtSearch.Foreground = new SolidColorBrush(Color.FromRgb(216, 180, 254));
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
            var specification = _selectedProduct.Specification;
            SpecLength.Text = DisplaySpecificationValue(specification?.Length);
            SpecLenTol.Text = DisplaySpecificationValue(specification?.LengthTolerance);
            SpecWidth.Text = DisplaySpecificationValue(specification?.Width);
            SpecWidTol.Text = DisplaySpecificationValue(specification?.WidthTolerance);
            SpecHeight.Text = DisplaySpecificationValue(specification?.Height);
            SpecHgtTol.Text = DisplaySpecificationValue(specification?.HeightTolerance);
            SpecInductance.Text = DisplaySpecificationValue(specification?.Inductance);
            SpecIndTol.Text = DisplaySpecificationValue(specification?.InductanceTolerance);
            SpecIsaat.Text = DisplaySpecificationValue(specification?.Isaat);
            SpecIsaatTol.Text = DisplaySpecificationValue(specification?.IsaatTolerance);
            SpecDcr.Text = DisplaySpecificationValue(specification?.Dcr);
            SpecDcrTol.Text = DisplaySpecificationValue(specification?.DcrTolerance);

            // Process tabs
            BuildDetailProcessTabs(_selectedProduct.Processes);

            // Documents — set Tag for delete button visibility
            DetailDocList.Tag = _currentUser.CanEditProduct ? Visibility.Visible : Visibility.Collapsed;
            DetailDocList.ItemsSource = _selectedProduct.Documents;

            // Role-based edit/delete/upload buttons — R&D Team only
            BadgeAccess.Visibility = _currentUser.CanEditProduct ? Visibility.Collapsed : Visibility.Visible;

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
            var metaDlg = new UploadDocumentDialog(defaultName,
                _selectedProduct.Processes.Select(process => process.ProcessName)) { Owner = this };
            if (metaDlg.ShowDialog() != true) return;

            try
            {
                var bytes = System.IO.File.ReadAllBytes(fileDlg.FileName);
                _service.AddDocument(_selectedProduct.ProductId, metaDlg.DocumentName,
                    metaDlg.DocumentType, metaDlg.Revision,
                    System.IO.Path.GetFileName(fileDlg.FileName), bytes, _currentUser.Username, metaDlg.ProcessName);
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
                PopulatePartNumberFilter();
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
                PopulatePartNumberFilter();
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

            try
            {
                _service.DeleteProduct(_selectedProduct.ProductId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menghapus produk: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            PopulatePartNumberFilter();
            LoadProducts();
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
        }

        // ─── NAVIGATION ───────────────────────────────────────────────────────

        private void BtnBackToList_Click(object sender, RoutedEventArgs e)
        {
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewMaterial.Visibility = Visibility.Collapsed;
            ViewTooling.Visibility = Visibility.Collapsed;
            ViewUsageHistory.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewMaterial.Visibility = Visibility.Collapsed;
            ViewTooling.Visibility = Visibility.Collapsed;
            ViewUsageHistory.Visibility = Visibility.Collapsed;
            ViewList.Visibility = Visibility.Visible;
            LoadProducts();
        }

        private void NavStoreMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessStore)
            {
                MessageBox.Show("Anda tidak memiliki akses ke menu Store Material.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ViewList.Visibility = Visibility.Collapsed;
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewTooling.Visibility = Visibility.Collapsed;
            ViewUsageHistory.Visibility = Visibility.Collapsed;
            ViewMaterial.Visibility = Visibility.Visible;

            try
            {
                // Load data once — share between filter population and grid
                var allMaterials = _service.GetStoreMaterials();
                _storeMaterials = allMaterials;
                PopulateMaterialFilters(allMaterials);
                ApplyStoreMaterialsToGrid(allMaterials);
                LoadUsageRequests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat data material: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavStore_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessStore)
            {
                MessageBox.Show("Anda tidak memiliki akses ke menu Store.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            NavStoreMaterial_Click(sender, e);
        }

        private void NavStoreTooling_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessStore)
            {
                MessageBox.Show("Anda tidak memiliki akses ke menu Store Tooling.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ViewList.Visibility = Visibility.Collapsed;
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewMaterial.Visibility = Visibility.Collapsed;
            ViewUsageHistory.Visibility = Visibility.Collapsed;
            ViewTooling.Visibility = Visibility.Visible;
            try
            {
                LoadToolingsForCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat data tooling: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavUsageHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanAccessStore)
            {
                MessageBox.Show("Anda tidak memiliki akses ke riwayat pemakaian.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ViewList.Visibility = Visibility.Collapsed;
            ViewDetail.Visibility = Visibility.Collapsed;
            ViewMaterial.Visibility = Visibility.Collapsed;
            ViewTooling.Visibility = Visibility.Collapsed;
            ViewUsageHistory.Visibility = Visibility.Visible;
            RefreshUsageHistory();
        }

        // ─── MATERIAL INVENTORY LOGIC ──────────────────────────────────────────

        private void PopulateMaterialFilters(List<StoreMaterialDto> allMaterials)
        {
            // Part numbers
            var currentPartNumber = (CmbFilterMaterialPartNumber.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var partNumbers = allMaterials
                .Select(m => m.PartNumber)
                .Where(partNumber => !string.IsNullOrWhiteSpace(partNumber))
                .Distinct()
                .OrderBy(partNumber => partNumber)
                .ToList();

            CmbFilterMaterialPartNumber.SelectionChanged -= MaterialFilter_SelectionChanged;
            CmbFilterMaterialPartNumber.Items.Clear();
            var defaultPartNumber = new ComboBoxItem { Content = "All Part Numbers", IsSelected = true };
            CmbFilterMaterialPartNumber.Items.Add(defaultPartNumber);

            foreach (var partNumber in partNumbers)
            {
                var item = new ComboBoxItem { Content = partNumber };
                if (partNumber == currentPartNumber)
                {
                    defaultPartNumber.IsSelected = false;
                    item.IsSelected = true;
                }
                CmbFilterMaterialPartNumber.Items.Add(item);
            }
            CmbFilterMaterialPartNumber.SelectionChanged += MaterialFilter_SelectionChanged;

            // Lots
            var currentLot = (CmbFilterMaterialLot.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var lots = allMaterials
                .Select(m => m.Lot)
                .Where(lot => !string.IsNullOrWhiteSpace(lot))
                .Distinct()
                .OrderBy(lot => lot)
                .ToList();

            CmbFilterMaterialLot.SelectionChanged -= MaterialFilter_SelectionChanged;
            CmbFilterMaterialLot.Items.Clear();
            var defaultLot = new ComboBoxItem { Content = "All Lots", IsSelected = true };
            CmbFilterMaterialLot.Items.Add(defaultLot);

            foreach (var lot in lots)
            {
                var item = new ComboBoxItem { Content = lot };
                if (lot == currentLot)
                {
                    defaultLot.IsSelected = false;
                    item.IsSelected = true;
                }
                CmbFilterMaterialLot.Items.Add(item);
            }
            CmbFilterMaterialLot.SelectionChanged += MaterialFilter_SelectionChanged;

            PopulateMaterialTextFilter(CmbFilterMaterialProduct,
                "All Products", allMaterials.Select(material => material.ProductPartNumber));
            PopulateMaterialTextFilter(CmbFilterMaterialFamily,
                "All Product Families", allMaterials.Select(material => material.ProductFamily));
        }

        private void PopulateMaterialTextFilter(ComboBox combo, string defaultText, IEnumerable<string> values)
        {
            var selected = GetComboVal(combo);
            combo.SelectionChanged -= MaterialFilter_SelectionChanged;
            combo.Items.Clear();
            var defaultItem = new ComboBoxItem { Content = defaultText, IsSelected = true };
            combo.Items.Add(defaultItem);
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct().OrderBy(value => value))
            {
                var item = new ComboBoxItem { Content = value };
                if (string.Equals(value, selected, StringComparison.OrdinalIgnoreCase))
                {
                    defaultItem.IsSelected = false;
                    item.IsSelected = true;
                }
                combo.Items.Add(item);
            }
            combo.SelectionChanged += MaterialFilter_SelectionChanged;
        }

        private void LoadStoreMaterials(string query = "", string partNumber = "", string lot = "")
        {
            try
            {
                var q = TxtSearchMaterialInput.Text == "Cari Part No, Description, Lot, Location..." ? "" : TxtSearchMaterialInput.Text;
                if (!string.IsNullOrEmpty(query)) q = query;

                _storeMaterials = _service.GetStoreMaterials(q, partNumber, lot,
                    GetComboVal(CmbFilterMaterialProduct), GetComboVal(CmbFilterMaterialFamily));
                UpdateAvailableStockQuantities();
                ApplyStoreMaterialsToGrid(_storeMaterials);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat material: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyStoreMaterialsToGrid(List<StoreMaterialDto> list)
        {
            GridStoreMaterials.ItemsSource = list;
            UpdateMaterialAvailableQuantities(list);

            if (MaterialEmptyStateMessage != null)
                MaterialEmptyStateMessage.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            TxtStatTotalItems.Text = list.Count.ToString("N0");
            TxtStatTotalQty.Text = list.Sum(m => m.Qty).ToString("N0");
        }

        private void MaterialFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var partNumber = GetComboVal(CmbFilterMaterialPartNumber);
            var lot = GetComboVal(CmbFilterMaterialLot);
            var q = TxtSearchMaterialInput.Text;
            LoadStoreMaterials(q, partNumber, lot);
        }

        private void BtnClearMaterialFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchMaterialInput.Text = "";
            CmbFilterMaterialPartNumber.SelectedIndex = 0;
            CmbFilterMaterialLot.SelectedIndex = 0;
            CmbFilterMaterialProduct.SelectedIndex = 0;
            CmbFilterMaterialFamily.SelectedIndex = 0;
            LoadStoreMaterials();
        }


        private void TxtSearchMaterial_KeyUp(object sender, KeyEventArgs e)
        {
            _materialSearchDebounce?.Dispose();
            _materialSearchDebounce = new Timer(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    var q = TxtSearchMaterialInput.Text;
                    var partNumber = GetComboVal(CmbFilterMaterialPartNumber);
                    var lot = GetComboVal(CmbFilterMaterialLot);
                    LoadStoreMaterials(q, partNumber, lot);
                });
            }, null, 300, Timeout.Infinite);
        }

        private void BtnAddMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore) return;
            var dlg = new StoreMaterialFormDialog(_service, _currentUser.Username) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                RefreshStoreMaterialView();
            }
        }

        private void BtnEditStoreMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore) return;
            if (sender is not Button btn || btn.DataContext is not StoreMaterialDto item) return;

            var dlg = new StoreMaterialFormDialog(_service, _currentUser.Username, item) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                RefreshStoreMaterialView();
            }
        }

        private void GridStoreMaterials_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridStoreMaterials.SelectedItem is not StoreMaterialDto item) return;

            var originalSource = e.OriginalSource as DependencyObject;
            while (originalSource != null)
            {
                if (originalSource is Button) return;
                if (originalSource is DataGridRow) break;
                if (originalSource is System.Windows.Controls.Primitives.DataGridColumnHeader) return;
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }
            if (originalSource == null) return;

            if (_currentUser.CanEditProduct)
            {
                BtnRequestMaterialUsage_Click(new Button { DataContext = item }, e);
                return;
            }
            if (!_currentUser.CanEditStore) return;

            var dlg = new StoreMaterialFormDialog(_service, _currentUser.Username, item) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                RefreshStoreMaterialView();
            }
        }

        private void BtnDeleteStoreMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore) return;
            if (sender is not Button btn || btn.DataContext is not StoreMaterialDto item) return;

            var confirm = MessageBox.Show(
                $"Hapus material '{item.PartNumber}' - {item.ItemDescription} (Lot: {item.Lot})?\n\nTindakan ini tidak dapat dibatalkan.",
                "Hapus Material Inventory", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _service.DeleteStoreMaterial(item.MaterialId);
                RefreshStoreMaterialView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menghapus material: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshStoreMaterialView()
        {
            try
            {
                var allMaterials = _service.GetStoreMaterials();
                _storeMaterials = allMaterials;
                PopulateMaterialFilters(allMaterials);
                LoadUsageRequests();
                ApplyStoreMaterialsToGrid(allMaterials);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal refresh data: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUsageRequests()
        {
            _usageRequests = _service.GetUsageRequests();
            UpdateAvailableStockQuantities();
            GridStoreMaterials?.Items.Refresh();
            GridToolings?.Items.Refresh();
            var today = DateTime.Today;
            var todayUsage = _usageRequests
                .Where(request => request.ItemType == "Material" &&
                                  request.Status == "Completed" &&
                                  request.ReturnedAt.HasValue &&
                                  ToLocalDateTime(request.ReturnedAt.Value).Date == today)
                .GroupBy(request => request.UoM)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Sum(request => request.UsedQty ?? 0):N2} {group.Key}")
                .ToList();
            TxtStatUsageToday.Text = todayUsage.Count == 0 ? "0" : string.Join(Environment.NewLine, todayUsage);
            DrawUsageChart();
            UpdateToolingUsageStats();
            UpdatePendingUsageNotice();
        }

        private void UpdateAvailableStockQuantities()
        {
            UpdateMaterialAvailableQuantities(_storeMaterials);
            foreach (var tooling in _toolings)
                tooling.AvailableQty = Math.Max(0, tooling.Qty - GetPendingReservedQuantity("Tooling", tooling.ToolingId));
        }

        private void UpdateMaterialAvailableQuantities(IEnumerable<StoreMaterialDto> materials)
        {
            foreach (var material in materials)
                material.AvailableQty = Math.Max(0,
                    material.Qty - GetPendingReservedQuantity("Material", material.MaterialId));
        }

        private double GetPendingReservedQuantity(string itemType, int inventoryId) =>
            _usageRequests.Where(request => request.ItemType == itemType &&
                                            request.InventoryId == inventoryId &&
                                            request.Status == "Pending")
                .Sum(request => request.RequestedQty);

        private void UpdateToolingUsageStats()
        {
            TxtStatTotalToolingItems.Text = _toolings.Count.ToString("N0");
            TxtStatTotalToolingQty.Text = _toolings.Sum(tooling => tooling.Qty).ToString("N0");
            var today = DateTime.Today;
            var todayUsage = _usageRequests
                .Where(request => request.ItemType == "Tooling" &&
                                  request.Status == "Completed" &&
                                  request.ReturnedAt.HasValue &&
                                  ToLocalDateTime(request.ReturnedAt.Value).Date == today)
                .GroupBy(request => request.UoM)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Sum(request => request.UsedQty ?? 0):N2} {group.Key}")
                .ToList();
            TxtStatToolingUsageToday.Text = todayUsage.Count == 0 ? "0" : string.Join(Environment.NewLine, todayUsage);
            DrawToolingUsageChart();
        }

        private void RefreshUsageHistory()
        {
            try
            {
                LoadUsageRequests();
                GridUsageRequests.Tag = _currentUser.CanEditStore
                    ? "Store"
                    : _currentUser.CanEditProduct ? "Rd" : string.Empty;
                var selectedType = GetComboVal(CmbHistoryType);
                IEnumerable<UsageRequestDto> filtered = _usageRequests;
                if (selectedType == "Material" || selectedType == "Tooling")
                    filtered = filtered.Where(request => request.ItemType == selectedType);

                var range = GetComboVal(CmbHistoryDateRange);
                var today = DateTime.Today;
                if (range == "Today")
                    filtered = filtered.Where(request => ToLocalDateTime(request.RequestedAt).Date == today);
                else if (range == "Yesterday")
                {
                    var yesterday = today.AddDays(-1);
                    filtered = filtered.Where(request => ToLocalDateTime(request.RequestedAt).Date == yesterday);
                }
                else if (range == "Custom range" && DpHistoryFrom.SelectedDate.HasValue &&
                         DpHistoryTo.SelectedDate.HasValue)
                {
                    var from = DpHistoryFrom.SelectedDate.Value.Date;
                    var to = DpHistoryTo.SelectedDate.Value.Date.AddDays(1);
                    filtered = filtered.Where(request =>
                        ToLocalDateTime(request.RequestedAt) >= from &&
                        ToLocalDateTime(request.RequestedAt) < to);
                }

                var results = filtered.ToList();
                GridUsageRequests.ItemsSource = results;
                UsageHistoryEmptyState.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                var pending = _usageRequests.Count(request => request.Status == "Pending");
                TxtPendingUsageNotice.Text = pending == 0
                    ? "No pending usage requests."
                    : $"{pending} usage request(s) are waiting for Store confirmation.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat riwayat pemakaian: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsageHistoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || CmbHistoryDateRange == null) return;
            var isCustomRange = GetComboVal(CmbHistoryDateRange) == "Custom range";
            DpHistoryFrom.Visibility = isCustomRange ? Visibility.Visible : Visibility.Collapsed;
            DpHistoryTo.Visibility = isCustomRange ? Visibility.Visible : Visibility.Collapsed;
            TxtHistoryDateSeparator.Visibility = isCustomRange ? Visibility.Visible : Visibility.Collapsed;
            if (isCustomRange)
            {
                DpHistoryFrom.SelectedDate ??= DateTime.Today;
                DpHistoryTo.SelectedDate ??= DateTime.Today;
            }
            RefreshUsageHistory();
        }

        private void UsageHistoryDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && GetComboVal(CmbHistoryDateRange) == "Custom range" &&
                DpHistoryFrom.SelectedDate.HasValue && DpHistoryTo.SelectedDate.HasValue)
                RefreshUsageHistory();
        }

        private void BtnClearUsageHistoryFilters_Click(object sender, RoutedEventArgs e)
        {
            CmbHistoryType.SelectedIndex = 0;
            CmbHistoryDateRange.SelectedIndex = 0;
            DpHistoryFrom.SelectedDate = null;
            DpHistoryTo.SelectedDate = null;
            DpHistoryFrom.Visibility = Visibility.Collapsed;
            DpHistoryTo.Visibility = Visibility.Collapsed;
            TxtHistoryDateSeparator.Visibility = Visibility.Collapsed;
            RefreshUsageHistory();
        }

        private void UpdatePendingUsageNotice()
        {
            try
            {
                var isStoreManager = _currentUser.CanEditStore;
                var count = isStoreManager
                    ? _service.GetPendingUsageRequestCount()
                    : _currentUser.CanEditProduct ? _service.GetIssuedUsageRequestCount() : 0;
                var notification = isStoreManager
                    ? "request(s) waiting for Store confirmation"
                    : "request(s) waiting for R&D return report";
                var showBadge = count > 0 && (isStoreManager || _currentUser.CanEditProduct);
                PendingStoreBadge.Visibility = showBadge ? Visibility.Visible : Visibility.Collapsed;
                TxtPendingStoreBadge.Text = count.ToString();
                PendingStoreBadge.ToolTip = $"{count} {notification}";
                PendingUsageBadge.Visibility = showBadge ? Visibility.Visible : Visibility.Collapsed;
                TxtPendingUsageBadge.Text = count.ToString();
                PendingUsageBadge.ToolTip = $"{count} {notification}";
                if (ViewUsageHistory.Visibility == Visibility.Visible)
                {
                    TxtPendingUsageNotice.Text = count == 0
                        ? "No usage requests are waiting for your action."
                        : $"{count} usage {notification}.";
                }
            }
            catch (Exception ex)
            {
                _usageNoticeTimer.Stop();
                MessageBox.Show($"Gagal memeriksa notifikasi request pemakaian: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsagePeriod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string period) return;
            _usagePeriod = period;
            var buttons = new[] { BtnUsageDay, BtnUsageWeek, BtnUsageMonth };
            foreach (var item in buttons)
            {
                var isSelected = string.Equals(item.Tag?.ToString(), period, StringComparison.Ordinal);
                item.Background = isSelected ? new SolidColorBrush(Color.FromRgb(243, 232, 255)) : Brushes.Transparent;
                item.Foreground = isSelected ? new SolidColorBrush(Color.FromRgb(109, 40, 217)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
            DrawUsageChart();
        }

        private void ToolingUsagePeriod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string period) return;
            _toolingUsagePeriod = period;
            var buttons = new[] { BtnToolingUsageDay, BtnToolingUsageWeek, BtnToolingUsageMonth };
            foreach (var item in buttons)
            {
                var isSelected = string.Equals(item.Tag?.ToString(), period, StringComparison.Ordinal);
                item.Background = isSelected ? new SolidColorBrush(Color.FromRgb(243, 232, 255)) : Brushes.Transparent;
                item.Foreground = isSelected
                    ? new SolidColorBrush(Color.FromRgb(109, 40, 217))
                    : new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
            DrawToolingUsageChart();
        }

        private void UsageChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawUsageChart();

        private void ToolingUsageChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) =>
            DrawToolingUsageChart();

        private void DrawUsageChart() => DrawUsageChart(UsageChartCanvas, "Material", _usagePeriod);

        private void DrawToolingUsageChart() =>
            DrawUsageChart(ToolingUsageChartCanvas, "Tooling", _toolingUsagePeriod);

        private void DrawUsageChart(Canvas chartCanvas, string itemType, string period)
        {
            if (chartCanvas == null) return;
            chartCanvas.Children.Clear();
            var width = chartCanvas.ActualWidth;
            var height = chartCanvas.ActualHeight;
            if (width < 120 || height < 55) return;

            var now = DateTime.Now;
            DateTime start;
            int bucketCount;
            TimeSpan bucketWidth;
            string[] labels;
            if (period == "Week")
            {
                start = now.Date.AddDays(-6);
                bucketCount = 7;
                bucketWidth = TimeSpan.FromDays(1);
                labels = Enumerable.Range(0, bucketCount).Select(i => start.AddDays(i).ToString("ddd")).ToArray();
            }
            else if (period == "Month")
            {
                start = now.Date.AddDays(-29);
                bucketCount = 6;
                bucketWidth = TimeSpan.FromDays(5);
                labels = Enumerable.Range(0, bucketCount)
                    .Select(i => start.AddDays(i * 5).ToString("dd MMM")).ToArray();
            }
            else
            {
                start = now.Date;
                bucketCount = 6;
                bucketWidth = TimeSpan.FromHours(4);
                labels = Enumerable.Range(0, bucketCount)
                    .Select(i => start.AddHours(i * 4).ToString("HH:mm")).ToArray();
            }
            var end = start + TimeSpan.FromTicks(bucketWidth.Ticks * bucketCount);
            var records = _usageRequests.Where(request =>
                    request.ItemType == itemType &&
                    request.Status == "Completed" &&
                    request.ReturnedAt.HasValue &&
                    ToLocalDateTime(request.ReturnedAt.Value) >= start &&
                    ToLocalDateTime(request.ReturnedAt.Value) < end)
                .ToList();
            var topItems = records.GroupBy(request => request.PartNumber)
                .Select(group => new { PartNumber = group.Key, Total = group.Sum(item => item.UsedQty ?? 0) })
                .OrderByDescending(group => group.Total)
                .Take(4)
                .ToList();
            if (topItems.Count == 0)
            {
                AddChartText(chartCanvas, $"No completed {itemType.ToLowerInvariant()} usage in this period",
                    12, Math.Max(5, height / 2 - 8), "#94A3B8");
                return;
            }

            const double left = 42;
            const double right = 10;
            const double top = 18;
            const double bottom = 22;
            var plotWidth = Math.Max(1, width - left - right);
            var plotHeight = Math.Max(1, height - top - bottom);
            var valuesBySeries = topItems.Select(item =>
                Enumerable.Range(0, bucketCount).Select(bucket =>
                {
                    var bucketStart = start + TimeSpan.FromTicks(bucketWidth.Ticks * bucket);
                    var bucketEnd = bucketStart + bucketWidth;
                    return records.Where(request => request.PartNumber == item.PartNumber &&
                            ToLocalDateTime(request.ReturnedAt!.Value) >= bucketStart &&
                            ToLocalDateTime(request.ReturnedAt.Value) < bucketEnd)
                        .Sum(request => request.UsedQty ?? 0);
                }).ToArray()).ToList();
            var maxValue = Math.Max(1, valuesBySeries.SelectMany(values => values).DefaultIfEmpty(0).Max());
            for (var line = 0; line <= 2; line++)
            {
                var y = top + plotHeight * line / 2;
                chartCanvas.Children.Add(new Line
                {
                    X1 = left,
                    X2 = width - right,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    StrokeThickness = 1
                });
                AddChartText(chartCanvas, (maxValue * (1 - line / 2d)).ToString("N0"),
                    0, y - 7, "#94A3B8");
            }

            var colors = new[]
            {
                Color.FromRgb(109, 40, 217),
                Color.FromRgb(13, 148, 136),
                Color.FromRgb(217, 119, 6),
                Color.FromRgb(37, 99, 235)
            };
            for (var index = 0; index < topItems.Count; index++)
            {
                var points = new PointCollection();
                for (var bucket = 0; bucket < bucketCount; bucket++)
                {
                    var x = left + (bucketCount == 1 ? 0 : plotWidth * bucket / (bucketCount - 1));
                    var y = top + plotHeight * (1 - valuesBySeries[index][bucket] / maxValue);
                    points.Add(new Point(x, y));
                }
                chartCanvas.Children.Add(new Polyline
                {
                    Points = points,
                    Stroke = new SolidColorBrush(colors[index]),
                    StrokeThickness = 2
                });
                foreach (var point in points)
                {
                    var marker = new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = new SolidColorBrush(colors[index])
                    };
                    Canvas.SetLeft(marker, point.X - 2.5);
                    Canvas.SetTop(marker, point.Y - 2.5);
                    chartCanvas.Children.Add(marker);
                }
                var legendX = left + index * Math.Min(130, (plotWidth - 75) / Math.Max(1, topItems.Count));
                chartCanvas.Children.Add(new Line
                {
                    X1 = legendX,
                    X2 = legendX + 12,
                    Y1 = 7,
                    Y2 = 7,
                    Stroke = new SolidColorBrush(colors[index]),
                    StrokeThickness = 2
                });
                AddChartText(chartCanvas, topItems[index].PartNumber, legendX + 16, 0, "#475569", 110);
            }
            for (var index = 0; index < labels.Length; index++)
            {
                var x = left + (labels.Length == 1 ? 0 : plotWidth * index / (labels.Length - 1));
                AddChartText(chartCanvas, labels[index], x - 18, height - 18, "#64748B", 48);
            }
        }

        private void AddChartText(Canvas chartCanvas, string text, double left, double top, string color,
            double width = 38)
        {
            var label = new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = 9,
                Foreground = (Brush)new BrushConverter().ConvertFromString(color)!,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, top);
            chartCanvas.Children.Add(label);
        }

        private static DateTime ToLocalDateTime(DateTime value) =>
            value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
                : value.ToLocalTime();

        private void BtnRequestMaterialUsage_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditProduct)
            {
                MessageBox.Show("Hanya akun R&D yang dapat mengajukan pemakaian material.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ResolveInventoryRow<StoreMaterialDto>(sender, GridStoreMaterials) is not { } material)
            {
                MessageBox.Show("Pilih baris material yang ingin diminta, lalu tekan Request.",
                    "Material Tidak Dipilih", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new UsageRequestDialog(_service,
                $"{material.PartNumber} · {material.ItemDescription} · Lot {material.Lot} · {material.UoM}",
                material.AvailableQty, "Material", material.MaterialId, _currentUser.Username) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                LoadUsageRequests();
                UpdatePendingUsageNotice();
                MessageBox.Show("Usage request has been sent to the Store team.",
                    "Request Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request tersimpan, tetapi tampilan gagal diperbarui: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRequestToolingUsage_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditProduct)
            {
                MessageBox.Show("Hanya akun R&D yang dapat mengajukan pemakaian tooling.",
                    "Akses Ditolak", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ResolveInventoryRow<ToolingDto>(sender, GridToolings) is not { } tooling)
            {
                MessageBox.Show("Pilih baris tooling yang ingin diminta, lalu tekan Request.",
                    "Tooling Tidak Dipilih", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new UsageRequestDialog(_service,
                $"{tooling.ToolingCode} · {tooling.ToolingName} · {tooling.UoM}",
                tooling.AvailableQty, "Tooling", tooling.ToolingId, _currentUser.Username) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                LoadUsageRequests();
                UpdatePendingUsageNotice();
                MessageBox.Show("Tooling request has been sent to the Store team.",
                    "Request Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request tersimpan, tetapi tampilan gagal diperbarui: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static T? ResolveInventoryRow<T>(object sender, DataGrid grid) where T : class
        {
            if (sender is Button button && button.DataContext is T item)
                return item;
            if (grid.SelectedItem is T selectedItem)
                return selectedItem;
            if (sender is DependencyObject source)
            {
                while (source != null && source is not DataGridRow)
                    source = VisualTreeHelper.GetParent(source);
                if (source is DataGridRow row && row.Item is T rowItem)
                    return rowItem;
            }
            return null;
        }

        private void BtnConfirmUsageRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore ||
                sender is not Button button || button.DataContext is not UsageRequestDto request) return;
            var confirm = MessageBox.Show(
                $"Confirm receipt of request for {request.RequestedQty:N2} {request.UoM} of {request.PartNumber}?\n\nThe requested quantity will be deducted from stock.",
                "Confirm Usage Request", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            try
            {
                _service.ConfirmUsageRequest(request.RequestId, _currentUser.Username);
                RefreshUsageHistory();
                RefreshStoreMaterialView();
                LoadToolingsForCurrentView();
                MessageBox.Show("Request confirmed and stock issued.",
                    "Request Confirmed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal mengonfirmasi request: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCompleteUsageRequest_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditProduct ||
                sender is not Button button || button.DataContext is not UsageRequestDto request) return;
            var dialog = new UsageRequestDialog(_service,
                $"{request.PartNumber} · {request.ItemDescription} · Requested {request.RequestedQty:N2} {request.UoM}",
                request.RequestedQty, request.ItemType, request.InventoryId, _currentUser.Username,
                isReturn: true, requestId: request.RequestId) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                RefreshUsageHistory();
                RefreshStoreMaterialView();
                LoadToolingsForCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menyelesaikan request: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddTooling_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore) return;
            var dialog = new ToolingFormDialog(_service) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SavedTooling == null) return;
            try
            {
                _service.CreateTooling(dialog.SavedTooling);
                LoadToolingsForCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menambahkan tooling: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditTooling_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore ||
                sender is not Button button || button.DataContext is not ToolingDto tooling) return;
            var dialog = new ToolingFormDialog(_service, tooling) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.SavedTooling == null) return;
            try
            {
                _service.UpdateTooling(tooling.ToolingId, dialog.SavedTooling);
                LoadToolingsForCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memperbarui tooling: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteTooling_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentUser.CanEditStore ||
                sender is not Button button || button.DataContext is not ToolingDto tooling) return;
            if (MessageBox.Show($"Delete tooling '{tooling.ToolingCode}' - {tooling.ToolingName}?",
                    "Delete Tooling", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                _service.DeleteTooling(tooling.ToolingId);
                LoadToolingsForCurrentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menghapus tooling: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridToolings_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            while (originalSource != null)
            {
                if (originalSource is Button) return;
                if (originalSource is DataGridRow) break;
                if (originalSource is System.Windows.Controls.Primitives.DataGridColumnHeader) return;
                originalSource = VisualTreeHelper.GetParent(originalSource);
            }
            if (originalSource == null) return;

            if (GridToolings.SelectedItem is not ToolingDto tooling) return;
            if (_currentUser.CanEditStore)
            {
                BtnEditTooling_Click(new Button { DataContext = tooling }, e);
                return;
            }
            if (_currentUser.CanEditProduct)
                BtnRequestToolingUsage_Click(new Button { DataContext = tooling }, e);
        }

        private void LoadToolingsForCurrentView()
        {
            _toolings = _service.GetToolings();
            _usageRequests = _service.GetUsageRequests();
            UpdateAvailableStockQuantities();
            UpdateToolingUsageStats();
            PopulateToolingFilters();
            ApplyToolingFilters();
        }

        private void PopulateToolingFilters()
        {
            PopulateToolingFilter(CmbFilterToolingPartNumber, "All Part Numbers",
                _toolings.Select(tooling => tooling.ToolingCode));
            PopulateToolingFilter(CmbFilterToolingLot, "All Lots",
                _toolings.Select(tooling => tooling.Lot));
            PopulateToolingFilter(CmbFilterToolingProduct, "All Products",
                _toolings.Select(tooling => tooling.ProductPartNumber));
            PopulateToolingFilter(CmbFilterToolingFamily, "All Product Families",
                _toolings.Select(tooling => tooling.ProductFamily));
        }

        private void PopulateToolingFilter(ComboBox combo, string defaultText, IEnumerable<string> values)
        {
            var selected = GetComboVal(combo);
            combo.SelectionChanged -= ToolingFilter_SelectionChanged;
            combo.Items.Clear();
            var defaultItem = new ComboBoxItem { Content = defaultText, IsSelected = true };
            combo.Items.Add(defaultItem);
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct().OrderBy(value => value))
            {
                var item = new ComboBoxItem { Content = value };
                if (string.Equals(value, selected, StringComparison.OrdinalIgnoreCase))
                {
                    defaultItem.IsSelected = false;
                    item.IsSelected = true;
                }
                combo.Items.Add(item);
            }
            combo.SelectionChanged += ToolingFilter_SelectionChanged;
        }

        private void ToolingFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyToolingFilters();
        }

        private void ToolingSearch_KeyUp(object sender, KeyEventArgs e) => ApplyToolingFilters();

        private void BtnClearToolingFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchToolingInput.Text = "";
            CmbFilterToolingPartNumber.SelectedIndex = 0;
            CmbFilterToolingLot.SelectedIndex = 0;
            CmbFilterToolingProduct.SelectedIndex = 0;
            CmbFilterToolingFamily.SelectedIndex = 0;
            ApplyToolingFilters();
        }

        private void ApplyToolingFilters()
        {
            var query = TxtSearchToolingInput.Text.Trim();
            var partNumber = GetComboVal(CmbFilterToolingPartNumber);
            var lot = GetComboVal(CmbFilterToolingLot);
            var productPartNumber = GetComboVal(CmbFilterToolingProduct);
            var productFamily = GetComboVal(CmbFilterToolingFamily);
            var filtered = _toolings.Where(tooling =>
                (string.IsNullOrWhiteSpace(query) ||
                 tooling.ToolingCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 tooling.ToolingName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 tooling.ItemCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 tooling.Lot.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 tooling.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 tooling.Remarks.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(partNumber) ||
                 string.Equals(tooling.ToolingCode, partNumber, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(lot) ||
                 string.Equals(tooling.Lot, lot, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(productPartNumber) ||
                 string.Equals(tooling.ProductPartNumber, productPartNumber, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(productFamily) ||
                 string.Equals(tooling.ProductFamily, productFamily, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            GridToolings.ItemsSource = filtered;
            ToolingEmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private void NavYourAccount_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Your Account is currently under development.",
                "Your Account", MessageBoxButton.OK, MessageBoxImage.Information);
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
