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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonFormattingGeneralOptionsPage : PythonDialogPage {
        private PythonFormattingGeneralOptionsControl _window;

        public PythonFormattingGeneralOptionsPage()
            : base("Advanced") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonFormattingGeneralOptionsControl();
                }
                return _window;
            }
        }

        public override void ResetSettings() {
            base.ResetSettings();
        }

        public override void LoadSettingsFromStorage() {
            base.LoadSettingsFromStorage();
        }

        public override void SaveSettingsToStorage() {
            base.SaveSettingsToStorage();
        }
    }
}
