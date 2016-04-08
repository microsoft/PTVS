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
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace Microsoft.PythonTools.EnvironmentsList {
    [ValueConversion(typeof(string), typeof(string))]
    sealed class FileNameEllipsisConverter : IValueConverter {
        public bool IncludeHead { get; set; }
        public bool IncludeBody { get; set; }
        public bool IncludeTail { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var path = value as string;
            if (string.IsNullOrEmpty(path) || IncludeHead && IncludeBody && IncludeTail) {
                return path;
            }

            var headSplit = path.IndexOf('\\') + 1;
            var headSplit2 = path.IndexOf('/') + 1;
            if (headSplit > 0 && headSplit2 > 0 && headSplit2 < headSplit) {
                headSplit = headSplit2;
            }
            var tailSplit = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));

            var head = (headSplit > 0) ? path.Remove(headSplit) : string.Empty;
            var tail = (tailSplit > 0) ? path.Substring(tailSplit) : string.Empty;

            var body = string.Empty;
            if (tailSplit > headSplit) {
                if (headSplit > 0) {
                    body = path.Substring(headSplit, tailSplit - headSplit);
                } else {
                    body = path.Remove(tailSplit);
                }
            } else if (tailSplit < headSplit) {
                Debug.Assert(headSplit > 0);
                body = path.Substring(Math.Max(headSplit, 0));
            }

            var result = string.Empty;
            if (IncludeHead) {
                result += head;
            }
            if (IncludeBody) {
                result += body;
            }
            if (IncludeTail) {
                result += tail;
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
