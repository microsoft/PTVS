// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Globalization;
using System.Windows.Data;

namespace Microsoft.PythonTools.EnvironmentsList {
    [ValueConversion(typeof(string), typeof(string))]
    sealed class FileNameEllipsisConverter : IValueConverter {
        public bool IncludeHead { get; set; }
        public bool IncludeTail { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var path = (string)value;
            if (IncludeHead && IncludeTail) {
                return path;
            }
            if (!IncludeHead && !IncludeTail) {
                return string.Empty;
            }
            var split = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            if (split < 0) {
                return IncludeHead ? string.Empty : path;
            }
            return IncludeHead ? path.Remove(split) : path.Substring(split);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
