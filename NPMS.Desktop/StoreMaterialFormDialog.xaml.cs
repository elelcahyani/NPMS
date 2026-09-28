using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NPMS.Desktop
{
    public partial class StoreMaterialFormDialog : Window
    {
        private readonly IProductService _service;
        private readonly string _username;
        private readonly StoreMaterialDto? _editingMaterial;
        private List<ProductMaterialDto> _productMaterials = new();

        public StoreMaterialDto? SavedMaterial { get; private set; }

        public StoreMaterialFormDialog(IProductService service, string username, StoreMaterialDto? materialToEdit = null)
        {
            InitializeComponent();
            _service = service;
            _username = username;
            _editingMaterial = materialToEdit;

            DpEntryDate.SelectedDate = DateTime.Now;
            LoadPartNumbers();

            if (_editingMaterial != null)
            {
                TxtTitle.Text = "Edit Material Inventory";
                BtnSave.Content = "Update Material";
                PopulateFormFields(_editingMaterial);
            }
        }

        private void LoadPartNumbers()
        {
            _productMaterials = _service.GetAllProductMaterials();

            CmbPartNumber.Items.Clear();

            if (_productMaterials.Count == 0)
            {
                var emptyItem = new ComboBoxItem
                {
                    Content = "-- Belum ada Material Part Number di Product Dashboard --",
                    IsEnabled = false
                };
                CmbPartNumber.Items.Add(emptyItem);
                CmbPartNumber.SelectedIndex = 0;
                return;
            }

            foreach (var pm in _productMaterials)
            {
                var item = new ComboBoxItem
                {
                    Content = pm.PartNumber,
                    Tag = pm.PartName
                };
                CmbPartNumber.Items.Add(item);
            }
        }

        private void CmbPartNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPartNumber.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                string partName = selectedItem.Tag.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(TxtItemDescription.Text) && !string.IsNullOrWhiteSpace(partName))
                {
                    TxtItemDescription.Text = partName;
                }
            }
        }

        private void PopulateFormFields(StoreMaterialDto dto)
        {
            // Set Part Number
            foreach (ComboBoxItem item in CmbPartNumber.Items)
            {
                if (item.Content?.ToString() == dto.PartNumber)
                {
                    CmbPartNumber.SelectedItem = item;
                    break;
                }
            }

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
            var selectedPartNumber = (CmbPartNumber.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(selectedPartNumber) || selectedPartNumber.StartsWith("--"))
            {
                ShowError("Silakan pilih Part Number material dari Product Dashboard.");
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
                PartNumber = selectedPartNumber,
                EntryDate = DpEntryDate.SelectedDate.Value,
                ItemDescription = TxtItemDescription.Text.Trim(),
                ItemCode = TxtItemCode.Text.Trim(),
                UoM = selectedUom,
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
