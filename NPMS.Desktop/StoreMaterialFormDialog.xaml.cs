using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class StoreMaterialFormDialog : Window
    {
        private readonly IProductService _service;
        private readonly string _username;
        private readonly StoreMaterialDto? _editingMaterial;

        public StoreMaterialDto? SavedMaterial { get; private set; }

        public StoreMaterialFormDialog(IProductService service, string username, StoreMaterialDto? materialToEdit = null)
        {
            InitializeComponent();
            _service = service;
            _username = username;
            _editingMaterial = materialToEdit;

            CmbProductPartNumber.ItemsSource = _service.SearchProducts(null, null, null);
            DpEntryDate.SelectedDate = DateTime.Now;

            if (_editingMaterial != null)
            {
                TxtTitle.Text = "Edit Material Inventory";
                BtnSave.Content = "Update Material";
                PopulateFormFields(_editingMaterial);
            }

        }

        private void CmbProductPartNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProductPartNumber.SelectedItem is ProductListDto product)
                TxtProductFamily.Text = product.ProductFamily;
        }

        private void PopulateFormFields(StoreMaterialDto dto)
        {
            TxtPartNumber.Text = dto.PartNumber;
            CmbProductPartNumber.SelectedValue = dto.ProductPartNumber;
            TxtProductFamily.Text = dto.ProductFamily;
            DpEntryDate.SelectedDate = dto.EntryDate;
            TxtItemDescription.Text = dto.ItemDescription;
            TxtItemCode.Text = dto.ItemCode;
            TxtLot.Text = dto.Lot;
            TxtLocation.Text = dto.Location;
            TxtQty.Text = dto.Qty.ToString();
            TxtPackage.Text = dto.Package;
            TxtRemarks.Text = dto.Remarks;

            // Set UoM
            foreach (ComboBoxItem item in CmbUoM.Items)
            {
                if (item.Content?.ToString()?.Equals(dto.UoM, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CmbUoM.SelectedItem = item;
                    break;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtError.Text = "";

            // Validation
            var partNumber = TxtPartNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                ShowError("Part Number wajib diisi.");
                return;
            }

            if (string.IsNullOrWhiteSpace(CmbProductPartNumber.SelectedValue?.ToString()))
            {
                ShowError("PN Product wajib dipilih.");
                return;
            }

            if (!DpEntryDate.SelectedDate.HasValue)
            {
                ShowError("Tanggal Masuk wajib diisi.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtItemDescription.Text))
            {
                ShowError("Item Description wajib diisi.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtLot.Text))
            {
                ShowError("Lot wajib diisi.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtLocation.Text))
            {
                ShowError("Storage Location wajib diisi.");
                return;
            }

            if (!double.TryParse(TxtQty.Text, out double qty) || qty <= 0)
            {
                ShowError("Quantity (Qty) harus berupa angka positif.");
                return;
            }

            var selectedUom = (CmbUoM.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PCS";

            var dto = new StoreMaterialSaveDto
            {
                MaterialId = _editingMaterial?.MaterialId ?? 0,
                PartNumber = partNumber,
                EntryDate = DpEntryDate.SelectedDate.Value,
                ItemDescription = TxtItemDescription.Text.Trim(),
                ItemCode = TxtItemCode.Text.Trim(),
                UoM = selectedUom,
                ProductPartNumber = CmbProductPartNumber.SelectedValue?.ToString() ?? string.Empty,
                ProductFamily = TxtProductFamily.Text,
                Lot = TxtLot.Text.Trim(),
                Location = TxtLocation.Text.Trim(),
                Qty = qty,
                Package = TxtPackage.Text.Trim(),
                Remarks = TxtRemarks.Text.Trim()
            };

            try
            {
                if (_editingMaterial == null)
                {
                    SavedMaterial = _service.CreateStoreMaterial(dto, _username);
                }
                else
                {
                    SavedMaterial = _service.UpdateStoreMaterial(_editingMaterial.MaterialId, dto, _username);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Gagal menyimpan material: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
