using System;
using System.Windows;
using System.Windows.Data;

namespace CaptureScreen
{
    /// <summary>
    /// 反转 <see cref="Visibility"/> 值
    /// </summary>
    [ValueConversion(typeof(Visibility), typeof(Visibility))]
    public class InverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(Visibility))
                throw new InvalidOperationException("值类型必须为 Visibility 类型");

            return (Visibility)value == Visibility.Hidden || (Visibility)value == Visibility.Collapsed ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
