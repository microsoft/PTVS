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
// See the Apache License, Version 2.0 for the specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    internal sealed class CondaEnvironmentInfoBarTrigger : IDisposable {
        private readonly object _lock = new object();
        private readonly IInterpreterRegistryService _interpreterRegistry;
        private readonly Action _triggerInfoBarCheck;
        private bool _isDisposed;
        private bool _isProjectOpened;
        private bool _isTriggered;

        public CondaEnvironmentInfoBarTrigger(
            IInterpreterRegistryService interpreterRegistry,
            Action triggerInfoBarCheck
        ) {
            _interpreterRegistry = interpreterRegistry ?? throw new ArgumentNullException(nameof(interpreterRegistry));
            _triggerInfoBarCheck = triggerInfoBarCheck ?? throw new ArgumentNullException(nameof(triggerInfoBarCheck));

            _interpreterRegistry.CondaInterpreterDiscoveryCompleted += OnInterpreterDiscoveryCompleted;
        }

        public void OnProjectOpened() {
            lock (_lock) {
                if (_isDisposed) {
                    return;
                }

                _isProjectOpened = true;
                TryTrigger();
            }
        }

        private void OnInterpreterDiscoveryCompleted(object sender, EventArgs e) {
            lock (_lock) {
                TryTrigger();
            }
        }

        private void TryTrigger() {
            if (_isDisposed ||
                _isTriggered ||
                !_isProjectOpened ||
                !_interpreterRegistry.IsCondaInterpreterDiscoveryComplete) {
                return;
            }

            _isTriggered = true;
            _triggerInfoBarCheck();
        }

        public void Dispose() {
            lock (_lock) {
                if (_isDisposed) {
                    return;
                }

                _isDisposed = true;
                _interpreterRegistry.CondaInterpreterDiscoveryCompleted -= OnInterpreterDiscoveryCompleted;
            }
        }
    }
}
