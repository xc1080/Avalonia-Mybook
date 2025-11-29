using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;

namespace MyBook.Views
{
    public class PathToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path)) return null;

            string resolved = path;
            if (!Path.IsPathRooted(path))
            {
                resolved = Path.Combine(AppContext.BaseDirectory, path);
            }

            if (File.Exists(resolved))
            {
                try
                {
                    return new Bitmap(resolved);
                }
                catch
                {
                }
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private string AssemblyName()
        {
            return System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "MyBook";
        }
    }
}
