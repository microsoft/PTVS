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
