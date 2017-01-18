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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Project {
    sealed class AddInterpreterView : DependencyObject, IDisposable {
        private readonly PythonProjectNode _project;

        public AddInterpreterView(
            PythonProjectNode project,
            IServiceProvider serviceProvider,
            IEnumerable<string> selectedIds
        ) {
            _project = project;
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(serviceProvider, project));
            
            var map = new Dictionary<string, InterpreterView>();
            foreach (var view in Interpreters) {
                map[view.Id] = view;
                view.IsSelected = false;
            }

            foreach (var id in selectedIds) {
                InterpreterView view;
                if (map.TryGetValue(id, out view)) {
                    view.IsSelected = true;
                }
            }

            _project.InterpreterFactoriesChanged += OnInterpretersChanged;
        }

        public void Dispose() {
            _project.InterpreterFactoriesChanged -= OnInterpretersChanged;
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => OnInterpretersChanged(sender, e)));
                return;
            }
            var def = _project.ActiveInterpreter;
            Interpreters.Merge(
                InterpreterView.GetInterpreters(_project.Site, _project),
                InterpreterView.EqualityComparer,
                InterpreterView.Comparer
            );
        }

        public ObservableCollection<InterpreterView> Interpreters { get; }
    }
}
