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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;

namespace Microsoft.PythonTools.Project {
    class VsPackageManagerUI : IPackageManagerUI {
        private readonly Redirector _outputWindow;
        private readonly GeneralOptions _options;
        private readonly bool _alwaysElevate;

        public VsPackageManagerUI(IServiceProvider provider, bool alwaysElevate = false) {
            _outputWindow = OutputWindowRedirector.GetGeneral(provider);
            _options = provider.GetPythonToolsService().GeneralOptions;
            _alwaysElevate = alwaysElevate;
        }

        public void OnErrorTextReceived(string text) {
            _outputWindow.WriteErrorLine(text);
        }

        public void OnOperationFinished(string operation, bool success) {
            if (_options.ShowOutputWindowForPackageInstallation) {
                _outputWindow.ShowAndActivate();
            }
        }

        public void OnOperationStarted(string operation) {
            if (_options.ShowOutputWindowForPackageInstallation) {
                _outputWindow.ShowAndActivate();
            }
        }

        public void OnOutputTextReceived(string text) {
            _outputWindow.WriteLine(text);
        }

        public Task<bool> ShouldElevateAsync(string operation) {
            return Task.FromResult(_options.ElevatePip || _alwaysElevate);
        }
    }
}
