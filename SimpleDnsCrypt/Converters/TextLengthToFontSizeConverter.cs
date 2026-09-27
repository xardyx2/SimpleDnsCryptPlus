using System;
using System.Globalization;
using System.Windows.Data;

namespace SimpleDnsCrypt.Converters
{
    /// <summary>
    ///     Text length to font size converter.
    /// </summary>
    public class TextLengthToFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (string)value;
            return text.Length switch
            {
                <= 10 => 14,
                > 10 and < 15 => 14,
                _ => 10,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}