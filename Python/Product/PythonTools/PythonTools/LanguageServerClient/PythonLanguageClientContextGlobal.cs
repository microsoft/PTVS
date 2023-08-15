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
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class PythonLanguageClientContextGlobal : IPythonLanguageClientContext {
        private readonly IInterpreterOptionsService _optionsService;
        private readonly DisposableBag _disposables;
        private IPythonInterpreterFactory _factory;

        public event EventHandler InterpreterChanged;
#pragma warning disable CS0067
        public event EventHandler SearchPathsChanged;
        public event EventHandler Closed;
        public event EventHandler ReanalyzeChanged;
#pragma warning restore CS0067

        public PythonLanguageClientContextGlobal(IInterpreterOptionsService optionsService) {
            _optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            _disposables = new DisposableBag(GetType().Name);

            _factory = _optionsService.DefaultInterpreter;

            _optionsService.DefaultInterpreterChanged += OnInterpreterChanged;
            _disposables.Add(() => {
                _optionsService.DefaultInterpreterChanged -= OnInterpreterChanged;
            });
        }

        public InterpreterConfiguration InterpreterConfiguration => _factory?.Configuration;

        public string RootPath => null;
        public IEnumerable<string> SearchPaths => Enumerable.Empty<string>();

        private void OnInterpreterChanged(object sender, EventArgs e) {
            if (_factory != _optionsService.DefaultInterpreter) {
                _factory = _optionsService.DefaultInterpreter;
                InterpreterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose() => _disposables.TryDispose();
    }
}
