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
using System.Globalization;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.InterpreterList {
    internal class InterpreterView : DependencyObject {
        private readonly string _identifier;
        private bool _startedRunning;

        public static IEnumerable<InterpreterView> GetInterpreters() {
            var componentService = (PythonToolsPackage.GetGlobalService(typeof(SComponentModel))) as IComponentModel;
            var factoryProviders = componentService.GetExtensions<IPythonInterpreterFactoryProvider>();

            var defaultId = PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterValue;
            var defaultVersion = PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterVersionValue;
            
            foreach (var factory in factoryProviders) {
                foreach (var interp in factory.GetInterpreterFactories()) {
                    if (interp != null) {
                        yield return new InterpreterView(
                            interp,
                            interp.GetInterpreterDisplay(),
                            interp.Id == defaultId && interp.Configuration.Version == defaultVersion);
                    }
                }
            }
        }

        public InterpreterView(IPythonInterpreterFactory interpreter, string name, bool isDefault) {
            Interpreter = interpreter;
            Name = name;
            Identifier = _identifier = string.Format(
                CultureInfo.InvariantCulture,
                "{0};{1}",
                interpreter.Id,
                interpreter.GetLanguageVersion());

            var withDb = interpreter as IInterpreterWithCompletionDatabase;
            if (withDb != null) {
                CanRefresh = true;
                withDb.IsCurrentChanged += Interpreter_IsCurrentChanged;
                IsCurrent = withDb.IsCurrent;
                IsCurrentReason = withDb.GetIsCurrentReason(CultureInfo.CurrentUICulture);
            }
            IsRunning = false;
            IsDefault = isDefault;
        }

        private void Interpreter_IsCurrentChanged(object sender, EventArgs e) {
            var withDb = sender as IInterpreterWithCompletionDatabase;
            if (withDb != null) {
                IsCurrent = withDb.IsCurrent;
                IsCurrentReason = withDb.GetIsCurrentReason(CultureInfo.CurrentUICulture);
            }
        }

        public void ProgressUpdate(Dictionary<string, AnalysisProgress> updateInfo) {
            AnalysisProgress update;
            if (updateInfo.TryGetValue(_identifier, out update)) {
                IsRunning = true;
                _startedRunning = false;

                if (update.Progress < int.MaxValue) {
                    Dispatcher.BeginInvoke((Action)(() => {
                        if (update.Progress > Progress) {
                            Progress = update.Progress;
                        }
                        Maximum = update.Maximum;
                    }));
                } else {
                    update.Progress = 0;
                }
            } else if (IsRunning && !_startedRunning) {
                IsRunning = false;
            }
        }

        internal void DefaultInterpreterUpdate(Guid defaultId, Version defaultVer) {
            if (Dispatcher.CheckAccess()) {
                IsDefault = (Interpreter.Id == defaultId && Interpreter.Configuration.Version == defaultVer);
            } else {
                Dispatcher.BeginInvoke((Action)(() => DefaultInterpreterUpdate(defaultId, defaultVer)));
            }
        }

        public void Start() {
            if (Dispatcher.CheckAccess()) {
                var withDb = Interpreter as IInterpreterWithCompletionDatabase;
                if (withDb != null) {
                    IsRunning = _startedRunning = withDb.GenerateCompletionDatabase(
                        GenerateDatabaseOptions.BuiltinDatabase | GenerateDatabaseOptions.StdLibDatabase,
                        () => { });
                }
            } else {
                Dispatcher.BeginInvoke((Action)(() => Start()));
            }
        }

        public void Abort() {
            throw new NotImplementedException();
        }

        public IPythonInterpreterFactory Interpreter {
            get;
            private set;
        }

        public string Identifier {
            get;
            private set;
        }


        public string Name {
            get { return (string)GetValue(NameProperty); }
            private set { SafeSetValue(NamePropertyKey, value); }
        }

        public bool CanRefresh {
            get { return (bool)SafeGetValue(CanRefreshProperty); }
            private set { SetValue(CanRefreshPropertyKey, value); }
        }

        public string IsCurrentReason {
            get { return (string)GetValue(IsCurrentReasonProperty); }
            private set { SafeSetValue(IsCurrentReasonPropertyKey, value); }
        }

        public bool IsRunning {
            get { return (bool)SafeGetValue(IsRunningProperty); }
            private set { SafeSetValue(IsRunningPropertyKey, value); }
        }

        public bool IsCurrent {
            get { return (bool)SafeGetValue(IsCurrentProperty); }
            private set { SafeSetValue(IsCurrentPropertyKey, value); }
        }

        public int Progress {
            get { return (int)GetValue(ProgressProperty); }
            private set { SetValue(ProgressPropertyKey, value); }
        }

        public int Maximum {
            get { return (int)GetValue(MaximumProperty); }
            private set { SetValue(MaximumPropertyKey, value); }
        }

        public bool IsDefault {
            get { return (bool)GetValue(IsDefaultProperty); }
            private set { SetValue(IsDefaultPropertyKey, value); }
        }

        private object SafeGetValue(DependencyProperty property) {
            if (Dispatcher.CheckAccess()) {
                return GetValue(property);
            } else {
                return Dispatcher.Invoke((Func<object>)(() => GetValue(property)));
            }
        }

        private void SafeSetValue(DependencyPropertyKey property, object value) {
            if (Dispatcher.CheckAccess()) {
                SetValue(property, value);
            } else {
                Dispatcher.BeginInvoke((Action)(() => SetValue(property, value)));
            }
        }

        private static readonly DependencyPropertyKey NamePropertyKey = DependencyProperty.RegisterReadOnly("Name", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsCurrentReasonPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrentReason", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsCurrentPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrent", typeof(bool), typeof(InterpreterView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsRunningPropertyKey = DependencyProperty.RegisterReadOnly("IsRunning", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false, IsRunning_Changed));
        private static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly("Progress", typeof(int), typeof(InterpreterView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey MaximumPropertyKey = DependencyProperty.RegisterReadOnly("Maximum", typeof(int), typeof(InterpreterView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey IsDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsDefault", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanRefreshPropertyKey = DependencyProperty.RegisterReadOnly("CanRefresh", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));

        public static readonly DependencyProperty NameProperty = NamePropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentReasonProperty = IsCurrentReasonPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentProperty = IsCurrentPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRunningProperty = IsRunningPropertyKey.DependencyProperty;
        public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;
        public static readonly DependencyProperty MaximumProperty = MaximumPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsDefaultProperty = IsDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanRefreshProperty = CanRefreshPropertyKey.DependencyProperty;

        private static void IsRunning_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(e.NewValue as bool? ?? true)) {
                d.SetValue(ProgressPropertyKey, 0);
                d.SetValue(MaximumPropertyKey, 0);
            }
        }
    }
}
