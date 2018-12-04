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

using System.Windows.Automation;

namespace TestUtilities.UI.Python {
    public class PythonPerfToolBar : AutomationWrapper {
        public PythonPerfToolBar(AutomationElement element)
            : base(element) {
        }

        public void NewPerfSession() {
            ClickButtonByName("Add Performance Session");
        }

        public void LaunchSession() {
            ClickButtonByName("Start Profiling");
        }

        public void Stop() {
            var button = FindByName("Stop Profiling");
            for (int i = 0; i < 100 && !button.Current.IsEnabled; i++) {
                System.Threading.Thread.Sleep(100);
            }

            Invoke(button);
        }
    }
}
