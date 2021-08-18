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
    public sealed class ScreenAdjustingConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            // parameter should be wanted width
            var width = parameter != null ? System.Convert.ToDouble(parameter) : -1;

            // Parameter should be the width we want
            if (width > 0) {
                // Ideally we'd use the actual width of the control, but at this
                // point the control is not rendered. So instead scale based on screen width
                // only care for situations where the width is less than 1200 (above 1200 we don't have a problem)
                if (SystemParameters.PrimaryScreenWidth < 1200) {
                    // How much less than 1200?
                    var percentOfBigger = SystemParameters.PrimaryScreenWidth / 1200;
                    return width * percentOfBigger;
                }

                return width;
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
