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
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal class PythonLanguageClientContextWorkspace : IPythonLanguageClientContext, IDisposable {
        private readonly IPythonWorkspaceContext _pythonWorkspace;
        private readonly DisposableBag _disposables;

        public event EventHandler InterpreterChanged;
        public event EventHandler SearchPathsChanged;
        public event EventHandler Closed;

        public PythonLanguageClientContextWorkspace(
            IPythonWorkspaceContext pythonWorkspace,
            string contentTypeName
        ) {
            _pythonWorkspace = pythonWorkspace ?? throw new ArgumentNullException(nameof(pythonWorkspace));
            ContentTypeName = contentTypeName ?? throw new ArgumentNullException(nameof(contentTypeName));
            _disposables = new DisposableBag(GetType().Name);

            _pythonWorkspace.ActiveInterpreterChanged += OnInterpreterChanged;
            _pythonWorkspace.SearchPathsSettingChanged += OnSearchPathsChanged;
            _disposables.Add(() => {
                _pythonWorkspace.ActiveInterpreterChanged -= OnInterpreterChanged;
                _pythonWorkspace.SearchPathsSettingChanged -= OnSearchPathsChanged;
            });

            _pythonWorkspace.AddActionOnClose(this, (obj) => Closed?.Invoke(this, EventArgs.Empty));
        }

        public string ContentTypeName { get; }

        public InterpreterConfiguration InterpreterConfiguration => _pythonWorkspace.CurrentFactory?.Configuration;

        public string RootPath => _pythonWorkspace.Location;

        public IEnumerable<string> SearchPaths => _pythonWorkspace.GetAbsoluteSearchPaths();

        private void OnInterpreterChanged(object sender, EventArgs e) {
            InterpreterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnSearchPathsChanged(object sender, EventArgs e) {
            SearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        public object Clone() {
            return new PythonLanguageClientContextWorkspace(
                _pythonWorkspace,
                ContentTypeName
            );
        }

        public void Dispose() {
            _disposables.TryDispose();
        }
    }
}
