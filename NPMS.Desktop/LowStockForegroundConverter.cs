using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NPMS.Desktop
{
    public sealed class LowStockForegroundConverter : IValueConverter
    {
        private static readonly Brush LowStockBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        private static readonly Brush NormalStockBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136));

        static LowStockForegroundConverter()
        {
            LowStockBrush.Freeze();
            NormalStockBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double quantity && quantity < 100 ? LowStockBrush : NormalStockBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
