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
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Base user control that automatically adapts to Visual Studio theme changes
    /// </summary>
    public class ThemeAwareUserControl : UserControl {
        public ThemeAwareUserControl() {
            // Apply theme colors as soon as the control is created
            ApplyThemeColors();
            
            // Subscribe to theme change events from VS
            VSColorTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                // Unsubscribe from the event when the control is disposed
                VSColorTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        // Fix the event handler signature to match what VSColorTheme.ThemeChanged expects
        private void OnThemeChanged(ThemeChangedEventArgs e) {
            ApplyThemeColors();
        }

        /// <summary>
        /// Apply the current Visual Studio theme colors to this control and all child controls
        /// </summary>
        public void ApplyThemeColors() {
            try {
                // Get theme colors from VS
                Color backColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                Color foreColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);

                // Apply to this control
                BackColor = backColor;
                ForeColor = foreColor;

                // Apply to all child controls
                ApplyThemeToControls(Controls, backColor, foreColor);
            }
            catch (Exception ex) {
                // Log or handle exceptions that might occur during theming
                System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
            }
        }

        private void ApplyThemeToControls(Control.ControlCollection controls, Color backColor, Color foreColor) {
            foreach (Control control in controls) {
                // Different control types need special handling
                if (control is TextBox || control is ComboBox) {
                    // Input controls
                    control.BackColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                    control.ForeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
                } else if (control is Button) {
                    // Use VS styling for buttons
                    ((Button)control).UseVisualStyleBackColor = true;
                } else if (!(control is Label)) {
                    // Most controls get the standard background/foreground
                    control.BackColor = backColor;
                    control.ForeColor = foreColor;
                }

                // Recursively apply to any child controls
                if (control.Controls.Count > 0) {
                    ApplyThemeToControls(control.Controls, backColor, foreColor);
                }
            }
        }
    }
}