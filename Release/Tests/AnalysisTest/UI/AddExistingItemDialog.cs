/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Windows.Automation;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows;


namespace AnalysisTest.UI {
    class AddExistingItemDialog : AutomationWrapper {
        public AddExistingItemDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void Add() {
            Keyboard.PressAndRelease(Key.A, Key.LeftAlt);
        }

        public void AddLink() {
            
            var addButton = Element.FindFirst(TreeScope.Children,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Add"),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Button")
                )
            );

            // click the chevron to open the menu
            var bottomRight = addButton.Current.BoundingRectangle.BottomRight;
            Mouse.MoveTo(new Point(bottomRight.X - 3, bottomRight.Y - 3));
            Mouse.Click(MouseButton.Left);

            // type the keyboard short cut for Add to Link
            Keyboard.Type(Key.L);
        }

        public string FileName { 
            get {
                var filename = (ValuePattern)GetFilenameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                return filename.Current.Value;
            }
            set { 
                var filename = (ValuePattern)GetFilenameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                filename.SetValue(value);
            }
        }

        private AutomationElement GetFilenameEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit"),
                    new PropertyCondition(AutomationElement.NameProperty, "File name:")
                )
            );
        }
    }
}
