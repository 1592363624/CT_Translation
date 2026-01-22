using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CT_Translation.Converters;

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        string checkValue = value.ToString();
        string targetValue = parameter.ToString();
        
        return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}