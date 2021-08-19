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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Windows;

namespace Microsoft.PythonTools.Wpf {
    internal static class Utilities {
        /// <summary>
        /// Convert a WinForms MSG to a WPF MSG.
        /// </summary>
        public static System.Windows.Interop.MSG ConvertToInteropMsg(System.Windows.Forms.Message m) {
            System.Windows.Interop.MSG msg = new System.Windows.Interop.MSG();

            msg.hwnd = m.HWnd;
            msg.lParam = m.LParam;
            msg.message = m.Msg;
            msg.wParam = m.WParam;

            return msg;
        }

        /// <summary>
        /// Determines if dialogs should display in landscape or portrait mode
        /// </summary>
        /// <remarks>
        /// Picks portrait mode if screen is too small as well (with assumption scrolling will be used)
        /// </remarks>
        public static bool IsLandscape => (SystemParameters.PrimaryScreenWidth > SystemParameters.PrimaryScreenHeight) && (SystemParameters.PrimaryScreenWidth > 900);
    }
}
