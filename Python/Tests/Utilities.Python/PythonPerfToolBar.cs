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

using System.Windows.Automation;

namespace TestUtilities.UI.Python {
    class PythonPerfToolBar : AutomationWrapper {
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
