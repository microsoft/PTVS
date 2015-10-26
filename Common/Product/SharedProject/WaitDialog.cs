// Visual Studio Shared Project
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    sealed class WaitDialog : IDisposable {
        private readonly int _waitResult;
        private readonly IVsThreadedWaitDialog2 _waitDialog;

        public WaitDialog(string waitCaption, string waitMessage, IServiceProvider serviceProvider, int displayDelay = 1, bool isCancelable = false, bool showProgress = false) {
            _waitDialog = (IVsThreadedWaitDialog2)serviceProvider.GetService(typeof(SVsThreadedWaitDialog));
            _waitResult = _waitDialog.StartWaitDialog(
                waitCaption,
                waitMessage,
                null,
                null,
                null,
                displayDelay,
                isCancelable,
                showProgress
            );
        }

        public void UpdateProgress(int currentSteps, int totalSteps) {
            bool canceled;
            _waitDialog.UpdateProgress(
                null,
                null,
                null,
                currentSteps,
                totalSteps,
                false,
                out canceled
            );

        }

        public bool Canceled {
            get {
                bool canceled;
                ErrorHandler.ThrowOnFailure(_waitDialog.HasCanceled(out canceled));
                return canceled;
            }
        }

        #region IDisposable Members

        public void Dispose() {
            if (ErrorHandler.Succeeded(_waitResult)) {
                int cancelled = 0;
                _waitDialog.EndWaitDialog(out cancelled);
            }
        }

        #endregion
    }
}