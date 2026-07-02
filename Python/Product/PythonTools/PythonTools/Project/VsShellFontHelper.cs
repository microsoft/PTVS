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

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Helpers for making WinForms property pages honor the Visual Studio environment
    /// (dialog) font. The environment font tracks the OS "Text size" accessibility
    /// setting, so applying it lets property page text resize with that setting instead
    /// of staying pinned to the fixed design-time font (MAS 1.4.4 Resize Text).
    /// </summary>
    public static class VsShellFontHelper {
        /// <summary>
        /// Applies the Visual Studio dialog font to the specified control. Child controls
        /// that don't set an explicit font inherit it through the ambient font, and because
        /// the property pages use <see cref="AutoScaleMode.Font"/> with auto-sizing layout,
        /// the page grows and reflows to fit the larger text.
        /// </summary>
        public static void ApplyEnvironmentFont(Control control) {
            if (control == null) {
                return;
            }

            try {
                if (Package.GetGlobalService(typeof(IUIService)) is IUIService uiService &&
                    uiService.Styles["DialogFont"] is Font dialogFont) {
                    control.Font = dialogFont;
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to apply Visual Studio environment font: {ex.Message}");
            }
        }
    }
}
