using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.PythonTools.Wpf;

namespace Microsoft.PythonTools.Environments {
    /// <summary>
    /// Converts parameters into styles based on landscape or portrait mode
    /// </summary>
    public sealed class LandscapePortraitConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            // value should be the user control that owns the styles
            var userControl = value as UserControl;
            // Parameter should have names of two different styles. First one is landscape, second one is portrait
            if (parameter != null && userControl != null) {
                var styles = parameter.ToString().Split('|');
                if (styles.Length == 2) {
                    var style = Utilities.IsLandscape ? styles[0] : styles[1];
                    return userControl.Resources[style];
                }
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
