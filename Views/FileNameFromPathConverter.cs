using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace MyBook.Views
{
    public class FileNameFromPathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path)) return "未选择";
            return Path.GetFileName(path);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
