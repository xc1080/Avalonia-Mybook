// existing usings
using System.Globalization;
using Avalonia.Data.Converters;
using System;
using Avalonia.Controls;

namespace MyBook.Converters
{
    // Returns true when string is null or empty (so placeholder TextBlock is visible), otherwise false
    public class StringNullOrEmptyToVisibility : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var s = value as string;
            return string.IsNullOrWhiteSpace(s);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
