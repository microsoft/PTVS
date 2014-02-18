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
    public class AutomationDialog  : AutomationWrapper, IDisposable {
        private bool _isDisposed;

        public AutomationDialog(VisualStudioApp app, AutomationElement element)
            : base(element) {
            App = app;
        }

        ~AutomationDialog()
        {
            Dispose(false);
        }

        public VisualStudioApp App { get; private set; }

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    try {
                        Element.GetWindowPattern().Close();
                    } catch (InvalidOperationException) {
                    } catch (ElementNotAvailableException) {
                    }
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
