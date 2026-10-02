using System;
using System.Globalization;
using System.Windows.Data;

namespace NPMS.Desktop
{
    public sealed class StockQuantityDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double quantity)
                return value?.ToString() ?? string.Empty;

            return quantity <= 0 ? "Habis" : quantity.ToString("N0", culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
