using System.Globalization;
using System.Windows;
using NPMS.Core.Services;
using System.Windows.Input;

namespace NPMS.Desktop
{
    public partial class UsageRequestDialog : Window
    {
        private readonly bool _isReturn;
        private readonly double _maximumQuantity;
        private readonly IProductService _service;
        private readonly string _itemType;
        private readonly int _inventoryId;
        private readonly int _requestId;
        private readonly string _username;

        public double Quantity { get; private set; }
        public string Reason { get; private set; } = string.Empty;

        public UsageRequestDialog(
            IProductService service,
            string itemDescription,
            double maximumQuantity,
            string itemType,
            int inventoryId,
            string username,
            bool isReturn = false,
            int requestId = 0)
        {
            InitializeComponent();
            _service = service;
            _isReturn = isReturn;
            _maximumQuantity = maximumQuantity;
            _itemType = itemType;
            _inventoryId = inventoryId;
            _requestId = requestId;
            _username = username;
            TxtDescription.Text = itemDescription;
            PreviewKeyDown += UsageRequestDialog_PreviewKeyDown;

            if (isReturn)
            {
                Title = $"Report {itemType} Return";
                TxtTitle.Text = "Report Remaining Quantity";
                TxtQuantityLabel.Text = "Quantity returned (enter 0 if fully used)";
                ReasonLabel.Visibility = Visibility.Collapsed;
                TxtReason.Visibility = Visibility.Collapsed;
                TxtCapacityWarning.Visibility = Visibility.Collapsed;
                BtnSubmit.Content = "Complete";
            }
            else
            {
                Title = $"Request {itemType} Usage";
                TxtTitle.Text = $"Request {itemType} Usage";
                TxtDescription.Text += $"\nAvailable: {maximumQuantity:N2}";
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseQuantity(TxtQuantity.Text, out var quantity))
            {
                ShowError("Masukkan jumlah dalam format angka yang valid.");
                return;
            }

            if (_isReturn)
            {
                if (quantity < 0 || quantity > _maximumQuantity)
                {
                    ShowError($"Jumlah sisa harus antara 0 dan {_maximumQuantity:N2}.");
                    return;
                }
            }
            else
            {
                if (quantity <= 0 || quantity > _maximumQuantity)
                {
                    ShowError(quantity > _maximumQuantity
                        ? $"Requested quantity exceeds available stock capacity ({_maximumQuantity:N2})."
                        : "Requested quantity must be greater than 0.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(TxtReason.Text))
                {
                    ShowError("Keterangan pemakaian wajib diisi.");
                    return;
                }
                Reason = TxtReason.Text.Trim();
            }

            try
            {
                BtnSubmit.IsEnabled = false;
                if (_isReturn)
                    _service.CompleteUsageRequest(_requestId, quantity, _username);
                else
                    _service.CreateUsageRequest(_itemType, _inventoryId, quantity, Reason, _username);
                Quantity = quantity;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError($"Request gagal disimpan: {ex.Message}");
            }
            finally
            {
                BtnSubmit.IsEnabled = true;
            }
        }

        private void TxtQuantity_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isReturn || TxtCapacityWarning == null) return;

            TxtCapacityWarning.Visibility =
                TryParseQuantity(TxtQuantity.Text, out var quantity) && quantity > _maximumQuantity
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            TxtError.Visibility = Visibility.Collapsed;
        }

        private void UsageRequestDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !ReferenceEquals(Keyboard.FocusedElement, TxtReason))
            {
                BtnSubmit_Click(BtnSubmit, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private static bool TryParseQuantity(string text, out double quantity) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out quantity) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out quantity);

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
            Activate();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
