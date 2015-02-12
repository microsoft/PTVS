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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    sealed class AddInterpreterView : DependencyObject, INotifyPropertyChanged, IDisposable {
        readonly IInterpreterOptionsService _interpreterService;
        
        public AddInterpreterView(
            IServiceProvider serviceProvider,
            IInterpreterOptionsService interpreterService,
            IEnumerable<IPythonInterpreterFactory> selected
        ) {
            _interpreterService = interpreterService;
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(serviceProvider, interpreterService));
            
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
                    view = new InterpreterView(interp, interp.Description, false);
                    view.IsSelected = true;
                    Interpreters.Add(view);
                }
            }

            _interpreterService.InterpretersChanged += OnInterpretersChanged;
        }

        public void Dispose() {
            if (_interpreterService != null) {
                _interpreterService.InterpretersChanged -= OnInterpretersChanged;
            }
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => OnInterpretersChanged(sender, e)));
                return;
            }
            var def = _interpreterService.DefaultInterpreter;
            Interpreters.Merge(
                _interpreterService.Interpreters.Select(i => new InterpreterView(i, i.Description, i == def)),
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
