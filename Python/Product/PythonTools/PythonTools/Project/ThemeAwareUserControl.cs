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
                // Get theme colors from VS - use Window colors for property pages
                // This provides the correct background for dialogs in all themes including Blue
                Color backColor = VSColorTheme.GetThemedColor(EnvironmentColors.SystemWindowColorKey);
                Color foreColor = VSColorTheme.GetThemedColor(EnvironmentColors.SystemWindowTextColorKey);

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
                // Special handling for different control types
                if (control is TextBox) {
                    // Use themed dialog colors for consistency with environment dialogs
                    control.BackColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxBackgroundColorKey);
                    control.ForeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxTextColorKey);
                } else if (control is ComboBox comboBox) {
                    // Use the same colors as TextBox for consistency
                    var comboBackColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxBackgroundColorKey);
                    var comboForeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxTextColorKey);
                    
                    // Apply colors
                    comboBox.BackColor = comboBackColor;
                    comboBox.ForeColor = comboForeColor;
                    
                    // For DropDownList style (non-editable), enable custom drawing for proper theming
                    if (comboBox.DropDownStyle == ComboBoxStyle.DropDownList) {
                        // Use Flat style to avoid double arrow issue
                        comboBox.FlatStyle = FlatStyle.Flat;
                        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                        
                        // Remove existing handler to avoid duplicates
                        comboBox.DrawItem -= ComboBox_DrawItem;
                        // Add our custom drawing handler
                        comboBox.DrawItem += ComboBox_DrawItem;
                    } else {
                        // For editable combo boxes, use standard rendering
                        comboBox.DrawMode = DrawMode.Normal;
                        comboBox.FlatStyle = FlatStyle.Standard;
                        comboBox.DrawItem -= ComboBox_DrawItem;
                    }
                } else if (control is Button button) {
                    button.UseVisualStyleBackColor = true;
                    // Apply themed dialog button colors
                    button.FlatStyle = FlatStyle.System;
                } else if (control is CheckBox || control is RadioButton) {
                    control.ForeColor = foreColor;
                    // Make background transparent for checkboxes and radio buttons
                    control.BackColor = Color.Transparent;
                } else if (control is ListView || control is TreeView || control is DataGridView) {
                    control.BackColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                    control.ForeColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
                } else if (control is Label) {
                    // Explicitly handle labels to ensure they get the correct foreground color
                    control.ForeColor = foreColor;
                    control.BackColor = Color.Transparent; // Labels typically have transparent background
                } else if (control is GroupBox) {
                    // Handle GroupBox - set foreground color for the title
                    control.ForeColor = foreColor;
                    // GroupBox background should be transparent
                    control.BackColor = Color.Transparent;
                } else if (control is TableLayoutPanel || control is Panel) {
                    // Panels should use the parent's background
                    control.BackColor = backColor;
                } else {
                    // Default for other controls
                    control.BackColor = backColor;
                    control.ForeColor = foreColor;
                }

                // Recurse into child controls if any
                if (control.Controls.Count > 0) {
                    ApplyThemeToControls(control.Controls, backColor, foreColor);
                }
            }
        }
        
        // Custom drawing for ComboBox items to ensure they render correctly in dark theme
        private void ComboBox_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) return;
            
            ComboBox combo = sender as ComboBox;
            
            // Get colors for drawing
            Color textColor;
            Color backColor;
            
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected) {
                // Selected item
                backColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxItemMouseOverBackgroundColorKey);
                textColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxItemMouseOverTextColorKey);
            } else {
                // Normal item
                backColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxBackgroundColorKey);
                textColor = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxTextColorKey);
            }
            
            // Draw the background
            using (var brush = new SolidBrush(backColor)) {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
            
            // Draw separator item (for the General page's interpreter dropdown)
            if (combo.Items[e.Index].ToString() == "-") {
                // Draw a separator line
                using (var pen = new Pen(textColor)) {
                    int y = e.Bounds.Top + e.Bounds.Height / 2;
                    e.Graphics.DrawLine(pen, e.Bounds.Left + 2, y, e.Bounds.Right - 2, y);
                }
            } else {
                // Draw the item text
                string text = combo.GetItemText(combo.Items[e.Index]);
                using (var brush = new SolidBrush(textColor)) {
                    // Use TextRenderer for better text quality
                    TextRenderer.DrawText(
                        e.Graphics, 
                        text, 
                        e.Font, 
                        new Rectangle(
                            e.Bounds.X + 2, 
                            e.Bounds.Y, 
                            e.Bounds.Width - 4, 
                            e.Bounds.Height
                        ), 
                        textColor, 
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
            
            // Draw the focus rectangle if the item has focus
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus) {
                e.DrawFocusRectangle();
            }
        }
    }
}