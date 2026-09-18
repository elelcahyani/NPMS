using Microsoft.Win32;
using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NPMS.Desktop
{
    public partial class ProductFormWindow : Window
    {
        private readonly IProductService _service;
        private readonly string _currentUser;
        private readonly int? _editProductId;

        // Process steps list — each step has its own parameter collection
        private ObservableCollection<ProcessStepDto> _steps = new();
        private ObservableCollection<ProcessParameterDto> _currentParams = new();
        private int _activeStepIndex = -1;

        // Documents added during this session
        private ObservableCollection<DocumentDto> _docs = new();

        // Materials
        private ObservableCollection<MaterialViewModel> _materials = new();

        // Image path
        private string _imagePath = "";
        // Track whether user has made any changes
        private bool _isDirty = false;

        public ProductDetailDto? SavedProduct { get; private set; }

        public ProductFormWindow(IProductService service, string currentUser)
        {
            InitializeComponent();
            _service = service;
            _currentUser = currentUser;
            _editProductId = null;
            TxtBreadcrumb.Text = "Add New Product";
            Title = "Add New Product — NPMS";
            DocList.ItemsSource = _docs;
            ParamList.ItemsSource = _currentParams;
            MaterialList.ItemsSource = _materials;
            ProcessDetailPanel.Visibility = Visibility.Collapsed;
            SubscribeDirtyTracking();
        }

        // Track which existing document IDs were removed during edit
        private readonly HashSet<int> _deletedDocIds = new();

        public ProductFormWindow(IProductService service, string currentUser, ProductDetailDto existing)
        {
            InitializeComponent();
            _service = service;
            _currentUser = currentUser;
            _editProductId = existing.ProductId;
            TxtBreadcrumb.Text = $"Edit Product — {existing.PartNumber}";
            Title = $"Edit Product — NPMS";
            DocList.ItemsSource = _docs;
            ParamList.ItemsSource = _currentParams;
            MaterialList.ItemsSource = _materials;
            ProcessDetailPanel.Visibility = Visibility.Collapsed;
            PopulateForm(existing);
            // Reset dirty after populating — user hasn't changed anything yet
            _isDirty = false;
            SubscribeDirtyTracking();
        }

        private void SubscribeDirtyTracking()
        {
            // Subscribe all relevant TextBoxes and ComboBoxes to mark form dirty
            foreach (var tb in FindVisualChildren<TextBox>(this))
                tb.TextChanged += (s, e) => _isDirty = true;
            foreach (var cb in FindVisualChildren<ComboBox>(this))
                cb.SelectionChanged += (s, e) => _isDirty = true;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent)
            where T : System.Windows.DependencyObject
        {
            if (parent == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var inner in FindVisualChildren<T>(child))
                    yield return inner;
            }
        }

        private void PopulateForm(ProductDetailDto p)
        {
            TxtPartNumber.Text = p.PartNumber;
            TxtProductName.Text = p.ProductName;
            TxtProductFamily.Text = p.ProductFamily;
            TxtProductGroup.Text = p.ProductGroup;
            SetCombo(CmbProductType, p.ProductType);
            SetCombo(CmbStatus, p.Status);
            TxtRevision.Text = p.CurrentRevision;
            TxtDescription.Text = p.Description;
            _imagePath = p.ImagePath;

            // Show existing image if available
            if (!string.IsNullOrWhiteSpace(_imagePath) && System.IO.File.Exists(_imagePath))
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(_imagePath, UriKind.Absolute);
                    bmp.EndInit();
                    ImgPreview.Source = bmp;
                    ImgPreview.Visibility = Visibility.Visible;
                    ImgPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch { }
            }

            if (p.Specification != null)
            {
                SpecLength.Text = StripUnit(p.Specification.Length, "mm");
                SpecLenTol.Text = StripUnit(p.Specification.LengthTolerance, "mm", "%");
                SpecWidth.Text = StripUnit(p.Specification.Width, "mm");
                SpecWidTol.Text = StripUnit(p.Specification.WidthTolerance, "mm", "%");
                SpecHeight.Text = StripUnit(p.Specification.Height, "mm");
                SpecHgtTol.Text = StripUnit(p.Specification.HeightTolerance, "mm", "%");
                SpecInductance.Text = StripUnit(p.Specification.Inductance, "µH", "uH", "mH", "H");
                SpecIndTol.Text = StripUnit(p.Specification.InductanceTolerance, "%", "µH", "uH");
                SpecIsaat.Text = StripUnit(p.Specification.Isaat, "A", "mA");
                SpecIsaatTol.Text = StripUnit(p.Specification.IsaatTolerance, "%", "A");
                SpecDcr.Text = StripUnit(p.Specification.Dcr, "Ω", "mΩ", "Ohm");
                SpecDcrTol.Text = StripUnit(p.Specification.DcrTolerance, "%", "Ω");
            }

            if (p.ManufacturingInfo != null)
            {
                MfgFactory.Text = p.ManufacturingInfo.Factory;
                MfgLine.Text = p.ManufacturingInfo.ProductionLine;
                MfgProdType.Text = p.ManufacturingInfo.ProductionType;
                MfgNotes.Text = p.ManufacturingInfo.ManufacturingNotes;
            }

            foreach (var step in p.Processes.OrderBy(x => x.ProcessOrder))
            {
                _steps.Add(new ProcessStepDto
                {
                    ProcessId = step.ProcessId,
                    ProcessOrder = step.ProcessOrder,
                    ProcessName = step.ProcessName,
                    MachineName = step.MachineName,
                    ToolingName = step.ToolingName,
                    ProcessDescription = step.ProcessDescription,
                    Parameters = new List<ProcessParameterDto>(step.Parameters)
                });
            }
            RebuildProcessTabs();
            if (_steps.Count > 0) SelectStep(0);

            foreach (var doc in p.Documents)
                _docs.Add(doc);

            foreach (var mat in p.Materials)
                _materials.Add(new MaterialViewModel
                {
                    PartNumber = mat.PartNumber,
                    PartName = mat.PartName
                });
        }

        // ─── PROCESS TABS ────────────────────────────────────────────────────

        private void RebuildProcessTabs()
        {
            ProcessTabBar.Children.Clear();
            for (int i = 0; i < _steps.Count; i++)
            {
                int idx = i;
                var rb = new RadioButton
                {
                    Content = _steps[i].ProcessName,
                    Style = (Style)FindResource("TabBtn"),
                    GroupName = "ProcessTabs",
                    IsChecked = (i == _activeStepIndex)
                };
                rb.Checked += (s, e) => SelectStep(idx);
                ProcessTabBar.Children.Add(rb);
            }

            // Update move/delete button states
            bool hasActive = _activeStepIndex >= 0 && _steps.Count > 0;
            BtnMoveUp.IsEnabled = hasActive && _activeStepIndex > 0;
            BtnMoveDown.IsEnabled = hasActive && _activeStepIndex < _steps.Count - 1;
            BtnDeleteProcess.IsEnabled = hasActive;
            BtnMoveUp.Opacity = BtnMoveUp.IsEnabled ? 1.0 : 0.35;
            BtnMoveDown.Opacity = BtnMoveDown.IsEnabled ? 1.0 : 0.35;
            BtnDeleteProcess.Opacity = BtnDeleteProcess.IsEnabled ? 1.0 : 0.35;
        }

        private void SelectStep(int index)
        {
            // Save current step data first
            if (_activeStepIndex >= 0 && _activeStepIndex < _steps.Count)
                SaveCurrentStepToModel();

            _activeStepIndex = index;
            var step = _steps[index];

            TxtProcessName.Text = step.ProcessName;
            TxtMachine.Text = step.MachineName;
            TxtTooling.Text = step.ToolingName;
            TxtProcessDesc.Text = step.ProcessDescription;

            _currentParams.Clear();
            foreach (var p in step.Parameters ?? new List<ProcessParameterDto>())
                _currentParams.Add(p);

            ProcessDetailPanel.Visibility = Visibility.Visible;

            // Sync radio button check state
            for (int i = 0; i < ProcessTabBar.Children.Count; i++)
            {
                if (ProcessTabBar.Children[i] is RadioButton rb)
                    rb.IsChecked = (i == index);
            }
        }

        private void SaveCurrentStepToModel()
        {
            if (_activeStepIndex < 0 || _activeStepIndex >= _steps.Count) return;
            var step = _steps[_activeStepIndex];
            step.ProcessName = TxtProcessName.Text.Trim();
            step.MachineName = TxtMachine.Text.Trim();
            step.ToolingName = TxtTooling.Text.Trim();
            step.ProcessDescription = TxtProcessDesc.Text.Trim();
            step.Parameters = new List<ProcessParameterDto>(_currentParams);
            // Refresh tab label
            RebuildProcessTabs();
        }

        private void BtnAddProcess_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStepIndex >= 0) SaveCurrentStepToModel();
            _isDirty = true;
            _steps.Add(new ProcessStepDto
            {
                ProcessOrder = _steps.Count + 1,
                ProcessName = $"Step {_steps.Count + 1}",
                MachineName = "",
                ToolingName = "",
                ProcessDescription = "",
                Parameters = new List<ProcessParameterDto>()
            });
            RebuildProcessTabs();
            SelectStep(_steps.Count - 1);
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStepIndex <= 0) return;
            if (_activeStepIndex >= 0) SaveCurrentStepToModel();

            var step = _steps[_activeStepIndex];
            _steps.RemoveAt(_activeStepIndex);
            _steps.Insert(_activeStepIndex - 1, step);

            // Recalculate ProcessOrder
            for (int i = 0; i < _steps.Count; i++)
                _steps[i].ProcessOrder = i + 1;

            int newIndex = _activeStepIndex - 1;
            _activeStepIndex = -1;
            RebuildProcessTabs();
            SelectStep(newIndex);
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStepIndex < 0 || _activeStepIndex >= _steps.Count - 1) return;
            if (_activeStepIndex >= 0) SaveCurrentStepToModel();

            var step = _steps[_activeStepIndex];
            _steps.RemoveAt(_activeStepIndex);
            _steps.Insert(_activeStepIndex + 1, step);

            // Recalculate ProcessOrder
            for (int i = 0; i < _steps.Count; i++)
                _steps[i].ProcessOrder = i + 1;

            int newIndex = _activeStepIndex + 1;
            _activeStepIndex = -1;
            RebuildProcessTabs();
            SelectStep(newIndex);
        }

        private void BtnDeleteProcess_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStepIndex < 0 || _steps.Count == 0) return;

            var stepName = _steps[_activeStepIndex].ProcessName;
            var confirm = MessageBox.Show($"Hapus process '{stepName}'?", "Konfirmasi",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            _steps.RemoveAt(_activeStepIndex);

            // Recalculate ProcessOrder
            for (int i = 0; i < _steps.Count; i++)
                _steps[i].ProcessOrder = i + 1;

            _activeStepIndex = -1;
            _currentParams.Clear();
            ProcessDetailPanel.Visibility = Visibility.Collapsed;
            RebuildProcessTabs();

            if (_steps.Count > 0)
                SelectStep(0);
        }

        private void BtnAddParameter_Click(object sender, RoutedEventArgs e)
        {
            _currentParams.Add(new ProcessParameterDto
            {
                ParameterName = "Parameter",
                ParameterValue = "",
                Unit = ""
            });
        }

        private void BtnRemoveParam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ProcessParameterDto param)
                _currentParams.Remove(param);
        }

        // ─── DOCUMENTS ───────────────────────────────────────────────────────

        private void BtnUploadDoc_Click(object sender, RoutedEventArgs e)
        {
            var fileDlg = new OpenFileDialog
            {
                Title = "Select Document",
                Filter = "All Files|*.*|PDF|*.pdf|Word|*.docx|Excel|*.xlsx"
            };
            if (fileDlg.ShowDialog() != true) return;

            var defaultName = Path.GetFileNameWithoutExtension(fileDlg.FileName);
            var metaDlg = new UploadDocumentDialog(defaultName) { Owner = this };
            if (metaDlg.ShowDialog() != true) return;

            _isDirty = true;
            _docs.Add(new DocumentDto
            {
                DocumentName = metaDlg.DocumentName,
                DocumentType = metaDlg.DocumentType,
                Revision = metaDlg.Revision,
                FileName = Path.GetFileName(fileDlg.FileName),
                FilePath = fileDlg.FileName,
                FileSize = new FileInfo(fileDlg.FileName).Length,
                UploadedBy = _currentUser,
                UploadedAt = DateTime.UtcNow
            });
        }

        private void BtnDeleteDoc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DocumentDto doc)
            {
                if (doc.DocumentId > 0)
                    _deletedDocIds.Add(doc.DocumentId);
                _isDirty = true;
                _docs.Remove(doc);
            }
        }

        // ─── IMAGE ───────────────────────────────────────────────────────────

        private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Product Image",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dlg.ShowDialog() != true) return;
            _imagePath = dlg.FileName;
            _isDirty = true;
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(_imagePath, UriKind.Absolute);
                bmp.EndInit();
                ImgPreview.Source = bmp;
                ImgPreview.Visibility = Visibility.Visible;
                ImgPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private void BtnAddMaterial_Click(object sender, RoutedEventArgs e)
        {
            _isDirty = true;
            _materials.Add(new MaterialViewModel { PartNumber = "", PartName = "" });
        }

        private void BtnRemoveMaterial_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MaterialViewModel mat)
            {
                _isDirty = true;
                _materials.Remove(mat);
            }
        }

        private void BtnAddSpec_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset semua field spesifikasi? Data yang diisi akan terhapus.",
                "Konfirmasi Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            SpecLength.Text = "";
            SpecLenTol.Text = "";
            SpecWidth.Text = "";
            SpecWidTol.Text = "";
            SpecHeight.Text = "";
            SpecHgtTol.Text = "";
            SpecInductance.Text = "";
            SpecIndTol.Text = "";
            SpecIsaat.Text = "";
            SpecIsaatTol.Text = "";
            SpecDcr.Text = "";
            SpecDcrTol.Text = "";
        }

        // ─── SPEC UNIT HELPERS ───────────────────────────────────────────────

        // Strip known units so form shows only the numeric value
        private static string StripUnit(string value, params string[] units)
        {
            var v = value.Trim();
            foreach (var u in units)
            {
                if (v.EndsWith(u, StringComparison.OrdinalIgnoreCase))
                    return v[..^u.Length].Trim();
            }
            return v;
        }

        // Append unit only if not already present
        private static string AppendUnit(string value, string unit)
        {
            var v = value.Trim();
            if (string.IsNullOrWhiteSpace(v)) return v;
            if (v.EndsWith(unit, StringComparison.OrdinalIgnoreCase)) return v;
            return $"{v} {unit}";
        }

        private static string AppendTolUnit(string value, string unit)
        {
            var v = value.Trim();
            if (string.IsNullOrWhiteSpace(v)) return v;
            // tolerance could be % or same unit
            if (v.EndsWith("%") || v.EndsWith(unit, StringComparison.OrdinalIgnoreCase)) return v;
            return $"{v} {unit}";
        }

        private void SetCombo(ComboBox cmb, string value)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Content?.ToString() == value)
                { cmb.SelectedItem = item; return; }
            }
        }

        private string GetCombo(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem item)
            {
                var val = item.Content?.ToString() ?? "";
                if (val.StartsWith("Enter ")) return "";
                return val;
            }
            return "";
        }

        // ─── CANCEL / SAVE ───────────────────────────────────────────────────

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show(
                    "Ada perubahan yang belum disimpan. Yakin ingin keluar?",
                    "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            DialogResult = false;
            Close();
        }

        private void BtnSaveDraft_Click(object sender, RoutedEventArgs e)
        {
            SetCombo(CmbStatus, "Draft");
            if (string.IsNullOrEmpty(GetCombo(CmbStatus)))
            {
                if (CmbStatus.Items.Count > 1) CmbStatus.SelectedIndex = 1;
            }
            DoSave();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e) => DoSave();

        private void DoSave()
        {
            HideError();

            if (string.IsNullOrWhiteSpace(TxtPartNumber.Text))
            { ShowError("Part Number harus diisi."); return; }

            if (string.IsNullOrWhiteSpace(TxtProductName.Text))
            { ShowError("Product Name harus diisi."); return; }

            // Save active step before building DTO
            if (_activeStepIndex >= 0) SaveCurrentStepToModel();

            var status = GetCombo(CmbStatus);
            if (string.IsNullOrEmpty(status)) status = "Draft";

            var productType = GetCombo(CmbProductType);
            if (string.IsNullOrEmpty(productType)) productType = "Inductor";

            var dto = new ProductSaveDto
            {
                PartNumber = TxtPartNumber.Text.Trim(),
                ProductName = TxtProductName.Text.Trim(),
                ProductFamily = TxtProductFamily.Text.Trim(),
                ProductGroup = TxtProductGroup.Text.Trim(),
                ProductType = productType,
                Status = status,
                CurrentRevision = string.IsNullOrWhiteSpace(TxtRevision.Text) ? "Revision A" : TxtRevision.Text.Trim(),
                Description = TxtDescription.Text.Trim(),
                ImagePath = _imagePath,
                Specification = new ProductSpecificationDto
                {
                    Length      = AppendUnit(SpecLength.Text, "mm"),
                    LengthTolerance  = AppendTolUnit(SpecLenTol.Text, "mm"),
                    Width       = AppendUnit(SpecWidth.Text, "mm"),
                    WidthTolerance   = AppendTolUnit(SpecWidTol.Text, "mm"),
                    Height      = AppendUnit(SpecHeight.Text, "mm"),
                    HeightTolerance  = AppendTolUnit(SpecHgtTol.Text, "mm"),
                    Inductance  = AppendUnit(SpecInductance.Text, "µH"),
                    InductanceTolerance = AppendTolUnit(SpecIndTol.Text, "%"),
                    Isaat       = AppendUnit(SpecIsaat.Text, "A"),
                    IsaatTolerance   = AppendTolUnit(SpecIsaatTol.Text, "%"),
                    Dcr         = AppendUnit(SpecDcr.Text, "Ω"),
                    DcrTolerance     = AppendTolUnit(SpecDcrTol.Text, "%"),
                },
                ManufacturingInfo = new ManufacturingInfoDto
                {
                    Factory = MfgFactory.Text.Trim(),
                    ProductionLine = MfgLine.Text.Trim(),
                    ProductionType = MfgProdType.Text.Trim(),
                    EquipmentGroup = "",
                    ProcessOwner = _currentUser,
                    ManufacturingNotes = MfgNotes.Text.Trim()
                },
                Processes = _steps.ToList(),
                Materials = _materials.Select(m => new ProductMaterialDto
                {
                    PartNumber = m.PartNumber,
                    PartName = m.PartName
                }).ToList()
            };

            // Disable save buttons to prevent double-submit
            BtnSave.IsEnabled = false;
            BtnSaveDraft.IsEnabled = false;
            BtnSave.Opacity = 0.6;
            BtnSaveDraft.Opacity = 0.6;

            try
            {
                if (_editProductId.HasValue)
                    SavedProduct = _service.UpdateProduct(_editProductId.Value, dto, _currentUser);
                else
                    SavedProduct = _service.CreateProduct(dto, _currentUser);

                // Delete documents removed during edit
                foreach (var docId in _deletedDocIds)
                    _service.DeleteDocument(docId);

                // Upload new documents
                if (SavedProduct != null)
                {
                    var existingIds = SavedProduct.Documents.Select(d => d.DocumentId).ToHashSet();
                    foreach (var doc in _docs)
                    {
                        if (doc.DocumentId > 0 && existingIds.Contains(doc.DocumentId)) continue;
                        if (!File.Exists(doc.FilePath)) continue;
                        try
                        {
                            var bytes = File.ReadAllBytes(doc.FilePath);
                            _service.AddDocument(SavedProduct.ProductId, doc.DocumentName,
                                doc.DocumentType, doc.Revision, doc.FileName, bytes, _currentUser);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Gagal upload dokumen '{doc.DocumentName}': {ex.Message}",
                                "Upload Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Gagal menyimpan: {ex.Message}");
                // Re-enable buttons so user can fix and retry
                BtnSave.IsEnabled = true;
                BtnSaveDraft.IsEnabled = true;
                BtnSave.Opacity = 1.0;
                BtnSaveDraft.Opacity = 1.0;
            }
        }

        private void ShowError(string msg)
        {
            TxtValidationError.Text = msg;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        private void HideError() => ErrorBorder.Visibility = Visibility.Collapsed;
    }

    public class MaterialViewModel : INotifyPropertyChanged
    {
        private string _partNumber = "";
        private string _partName = "";

        public string PartNumber
        {
            get => _partNumber;
            set { _partNumber = value; OnPropertyChanged(nameof(PartNumber)); }
        }

        public string PartName
        {
            get => _partName;
            set { _partName = value; OnPropertyChanged(nameof(PartName)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
