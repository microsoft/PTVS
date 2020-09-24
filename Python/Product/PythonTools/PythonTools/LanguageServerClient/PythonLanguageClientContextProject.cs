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
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal sealed class PythonLanguageClientContextProject : IPythonLanguageClientContext {
        private readonly PythonProjectNode _project;
        private readonly DisposableBag _disposables;

        public event EventHandler InterpreterChanged;
        public event EventHandler SearchPathsChanged;
        public event EventHandler Closed;

        public PythonLanguageClientContextProject(PythonProjectNode project) {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _disposables = new DisposableBag(GetType().Name);

            _project.LanguageServerInterpreterChanged += OnInterpreterChanged;
            _project.LanguageServerSearchPathsChanged += OnSearchPathsChanged;
            _disposables.Add(() => {
                _project.LanguageServerInterpreterChanged -= OnInterpreterChanged;
                _project.LanguageServerSearchPathsChanged -= OnSearchPathsChanged;
            });

            _project.AddActionOnClose(this, (obj) => Closed?.Invoke(this, EventArgs.Empty));
        }

        public InterpreterConfiguration InterpreterConfiguration => _project.ActiveInterpreter?.Configuration;

        public string RootPath => _project.ProjectHome;
        public IEnumerable<string> SearchPaths => _project._searchPaths.GetAbsoluteSearchPaths();

        private void OnInterpreterChanged(object sender, EventArgs e) => InterpreterChanged?.Invoke(this, EventArgs.Empty);
        private void OnSearchPathsChanged(object sender, EventArgs e) => SearchPathsChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => _disposables.TryDispose();
    }
}
