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
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    sealed class AddInterpreterView : DependencyObject, INotifyPropertyChanged, IDisposable {
        private readonly PythonProjectNode _project;

        public AddInterpreterView(
            PythonProjectNode project,
            IServiceProvider serviceProvider,
            IEnumerable<IPythonInterpreterFactory> selected
        ) {
            _project = project;
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(serviceProvider, project));
            
            var map = new Dictionary<IPythonInterpreterFactory, InterpreterView>();
            foreach (var view in Interpreters) {
                map[view.Interpreter] = view;
                view.IsSelected = false;
            }

            foreach (var interp in selected) {
                InterpreterView view;
                if (map.TryGetValue(interp, out view)) {
                    view.IsSelected = true;
                } else {
                    view = new InterpreterView(interp, interp.Configuration.Description, false);
                    view.IsSelected = true;
                    Interpreters.Add(view);
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
                _project.InterpreterFactories.Select(i => new InterpreterView(i, i.Configuration.Description, i == def)),
                InterpreterView.EqualityComparer,
                InterpreterView.Comparer
            );
        }

        public ObservableCollection<InterpreterView> Interpreters {
            get { return (ObservableCollection<InterpreterView>)GetValue(InterpretersProperty); }
            private set { SetValue(InterpretersPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey InterpretersPropertyKey = DependencyProperty.RegisterReadOnly("Interpreters", typeof(ObservableCollection<InterpreterView>), typeof(AddInterpreterView), new PropertyMetadata());
        public static readonly DependencyProperty InterpretersProperty = InterpretersPropertyKey.DependencyProperty;


        public event PropertyChangedEventHandler PropertyChanged;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(e.Property.Name));
            }
        }
    }
}
