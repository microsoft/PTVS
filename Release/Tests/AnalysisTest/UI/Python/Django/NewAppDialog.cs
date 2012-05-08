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

namespace AnalysisTest.UI.Python.Django {
    class NewAppDialog : AutomationWrapper {
        public NewAppDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
        }

        public void Ok() {
            Invoke(FindButton("_ok"));
        }

        public void Cancel() {
            Invoke(FindButton("_cancel"));
        }
        
        public string AppName {
            get {
                var patterns = GetAppNameEditBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetAppNameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                return filename.Current.Value;
            }
            set {
                var patterns = GetAppNameEditBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetAppNameEditBox().GetCurrentPattern(ValuePattern.Pattern);
                filename.SetValue(value);
            }
        }

        private AutomationElement GetAppNameEditBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "_newAppName")
            );
        }
    }
}
