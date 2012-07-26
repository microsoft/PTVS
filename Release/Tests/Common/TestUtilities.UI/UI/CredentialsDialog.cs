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

namespace TestUtilities.UI {
    class CredentialsDialog : AutomationWrapper {
        public CredentialsDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void Ok() {
            Invoke(FindButton("OK"));
        }

        public void Cancel() {
            Invoke(FindButton("Cancel"));
        }

        public string UserName {
            get {
                var patterns = GetUsernameEditBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetUsernameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                return filename.Current.Value;
            }
            set {
                var patterns = GetUsernameEditBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetUsernameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                filename.SetValue(value);
            }
        }

        public string Password {
            get {
                var filename = (ValuePattern)GetPasswordEditBox().GetCurrentPattern(ValuePattern.Pattern);
                return filename.Current.Value;
            }
            set {
                var filename = (ValuePattern)GetPasswordEditBox().GetCurrentPattern(ValuePattern.Pattern);
                filename.SetValue(value);
            }
        }

        private AutomationElement GetUsernameEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "User name:"),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
                )
            );
        }
        private AutomationElement GetPasswordEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Password:"),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
                )
            );
        }
    }
}
