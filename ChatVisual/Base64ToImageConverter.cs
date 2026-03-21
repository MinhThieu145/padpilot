using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;


/// <summary>
/// Converts a base64-encoded image string to a WPF BitmapImage for display in the UI.
///
/// HOW WPF CONVERTERS WORK:
/// WPF's binding system can only bind data directly to UI properties.
/// When the data needs to be transformed before display (e.g. a string → image),
/// you need a converter — a class that sits between the data and the UI.
///
/// The binding system calls Convert() automatically for every item:
///   base64 string → [Base64ToImageConverter.Convert()] → BitmapImage → Image.Source
///
/// USAGE IN XAML:
/// 1. Register in Window.Resources:
///    <local:Base64ToImageConverter x:Key="Base64ToImageConverter"/>
///
/// 2. Use in a binding:
///    <Image Source="{Binding Converter={StaticResource Base64ToImageConverter}}"/>
///
/// ConvertBack is not implemented — this converter is one-way only.
/// </summary>

namespace ChatVisual
{
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valueString = value as string;
            if (valueString == null) return null;

            byte[] data = System.Convert.FromBase64String(valueString);

            using (MemoryStream ms = new MemoryStream(data))
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                return image;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}