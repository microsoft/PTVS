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
using EnvDTE;

namespace TestUtilities.Python {
    public class DebuggingGeneralOptionsSetter : IDisposable {
        private readonly DTE _dte;
        private readonly bool? _enableJustMyCode;

        public DebuggingGeneralOptionsSetter(
            DTE dte,
            bool? enableJustMyCode = null
        ) {
            _dte = dte;

            if (enableJustMyCode.HasValue) {
                _enableJustMyCode = (bool)_dte.Properties["Debugging", "General"].Item("EnableJustMyCode").Value;
                _dte.Properties["Debugging", "General"].Item("EnableJustMyCode").Value = enableJustMyCode.Value;
            }
        }

        public void Dispose() {
            if (_enableJustMyCode.HasValue) {
                _dte.Properties["Debugging", "General"].Item("EnableJustMyCode").Value = _enableJustMyCode.Value;
            }
        }
    }
}
