using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System.Globalization;
using System.Windows.Controls;
using System.Windows;

namespace NPMS.Desktop
{
    public partial class ToolingFormDialog : Window
    {
        private readonly ToolingDto? _editing;
        public ToolingSaveDto? SavedTooling { get; private set; }

        public ToolingFormDialog(IProductService service, ToolingDto? editing = null)
        {
            InitializeComponent();
            CmbProductPartNumber.ItemsSource = service.SearchProducts(null, null, null);
            DpEntryDate.SelectedDate = DateTime.Today;
            _editing = editing;
            if (editing == null) return;
            Title = "Edit Tooling Inventory";
            TxtTitle.Text = "Edit Tooling Inventory";
            BtnSave.Content = "Update Tooling";
            TxtPartNumber.Text = editing.ToolingCode;
            TxtToolingName.Text = editing.ToolingName;
            DpEntryDate.SelectedDate = editing.EntryDate;
            TxtItemCode.Text = editing.ItemCode;
            TxtQuantity.Text = editing.Qty.ToString(CultureInfo.CurrentCulture);
            TxtUoM.Text = editing.UoM;
            TxtLot.Text = editing.Lot;
            TxtLocation.Text = editing.Location;
            TxtPackage.Text = editing.Package;
            TxtRemarks.Text = editing.Remarks;
            CmbProductPartNumber.SelectedValue = editing.ProductPartNumber;
            TxtProductFamily.Text = editing.ProductFamily;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPartNumber.Text) ||
                string.IsNullOrWhiteSpace(TxtToolingName.Text) ||
                string.IsNullOrWhiteSpace(TxtLocation.Text) ||
            !DpEntryDate.SelectedDate.HasValue ||
            string.IsNullOrWhiteSpace(CmbProductPartNumber.SelectedValue?.ToString()))
            {
            ShowError("Part Number, tooling name, entry date, location, and product are required.");
                return;
            }
            if (!double.TryParse(TxtQuantity.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var quantity) ||
                quantity < 0 || double.IsNaN(quantity) || double.IsInfinity(quantity))
            {
                ShowError("Quantity must be a valid non-negative number.");
                return;
            }

            SavedTooling = new ToolingSaveDto
            {
                ToolingId = _editing?.ToolingId ?? 0,
                ToolingCode = TxtPartNumber.Text.Trim(),
                ToolingName = TxtToolingName.Text.Trim(),
                EntryDate = DpEntryDate.SelectedDate.Value,
                ItemCode = TxtItemCode.Text.Trim(),
                Qty = quantity,
                UoM = string.IsNullOrWhiteSpace(TxtUoM.Text) ? "PCS" : TxtUoM.Text.Trim(),
                Lot = TxtLot.Text.Trim(),
                Location = TxtLocation.Text.Trim(),
                Package = TxtPackage.Text.Trim(),
                Remarks = TxtRemarks.Text.Trim(),
                ProductPartNumber = CmbProductPartNumber.SelectedValue?.ToString() ?? string.Empty,
                ProductFamily = TxtProductFamily.Text
            };
            DialogResult = true;
        }

        private void CmbProductPartNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProductPartNumber.SelectedItem is ProductListDto product)
                TxtProductFamily.Text = product.ProductFamily;
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
