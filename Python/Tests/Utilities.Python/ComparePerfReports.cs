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

namespace TestUtilities.UI.Python {
    class ComparePerfReports : AutomationWrapper, IDisposable {
        public ComparePerfReports(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
            WaitForInputIdle();
        }

        public void Dispose() {
            object pattern;
            if (Element.TryGetCurrentPattern(WindowPattern.Pattern, out pattern)) {
                try {
                    ((WindowPattern)pattern).Close();
                } catch (ElementNotAvailableException) {
                }
            }
        }

        public void Ok() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByName("OK"));
        }

        public void Cancel() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByName("Cancel"));
        }

        public string ComparisonFile {
            get {
                return ComparisonFileTextBox.GetValue();
            }
            set {
                ComparisonFileTextBox.SetValue(value);
            }
        }

        private AutomationWrapper ComparisonFileTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("ComparisonFile"));
            }
        }

        public string BaselineFile {
            get {
                return BaselineFileTextBox.GetValue();
            }
            set {
                BaselineFileTextBox.SetValue(value);
            }
        }

        private AutomationWrapper BaselineFileTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("BaselineFile"));
            }
        }
    }
}
