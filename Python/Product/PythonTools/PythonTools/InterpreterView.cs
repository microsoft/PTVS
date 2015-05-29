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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools {
    internal class InterpreterView : DependencyObject, INotifyPropertyChanged {
        private readonly string _identifier;
        private DateTime _expectFirstUpdateBy;
        private bool _isRunning;

        public static readonly IEqualityComparer<InterpreterView> EqualityComparer = new InterpreterViewComparer();
        public static readonly IComparer<InterpreterView> Comparer = (IComparer<InterpreterView>)EqualityComparer;

        public static IEnumerable<InterpreterView> GetInterpreters(
            IServiceProvider serviceProvider,
            IInterpreterOptionsService interpreterService = null
        ) {
            if (interpreterService == null) {
                interpreterService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                if (interpreterService == null) {
                    return Enumerable.Empty<InterpreterView>();
                }
            }

            return interpreterService.KnownProviders
                .Where(p => !(p is LoadedProjectInterpreterFactoryProvider))
                .SelectMany(p => p.GetInterpreterFactories())
                .Where(PythonInterpreterFactoryExtensions.IsUIVisible)
                .OrderBy(fact => fact.Description)
                .ThenBy(fact => fact.Configuration.Version)
                .Select(i => new InterpreterView(i, i.Description, i == interpreterService.DefaultInterpreter));
        }

        public InterpreterView(IPythonInterpreterFactory interpreter, string name, PythonProjectNode project)
            : this(interpreter, name, false) {
            Project = project;
            if (Project != null) {
                CanBeDefault = false;
                SubName = project.Caption;
            }
        }

        public InterpreterView(IPythonInterpreterFactory interpreter, string name, bool isDefault) {
            Interpreter = interpreter;
            Name = name;
            SubName = string.Empty;
            Identifier = _identifier = AnalyzerStatusListener.GetIdentifier(interpreter);

            var withDb = interpreter as IPythonInterpreterFactoryWithDatabase;
            if (withDb != null) {
                CanRefresh = File.Exists(interpreter.Configuration.InterpreterPath) &&
                    Directory.Exists(interpreter.Configuration.LibraryPath);
                withDb.IsCurrentChanged += Interpreter_IsCurrentChanged;
                IsCurrent = withDb.IsCurrent;
                IsCurrentReason = withDb.GetFriendlyIsCurrentReason(CultureInfo.CurrentUICulture);
            }
            IsRunning = false;
            IsDefault = isDefault;
        }

        public InterpreterView(string name) {
            Name = name;
        }

        public override string ToString() {
            return Name;
        }

        private void Interpreter_IsCurrentChanged(object sender, EventArgs e) {
            var withDb = sender as IPythonInterpreterFactoryWithDatabase;
            if (withDb != null) {
                IsCurrent = withDb.IsCurrent;
                IsCurrentReason = withDb.GetFriendlyIsCurrentReason(CultureInfo.CurrentUICulture);
                IsCheckingDatabase = withDb.IsCheckingDatabase;
            }
        }

        public void ProgressUpdate(Dictionary<string, AnalysisProgress> updateInfo) {
            var withDb = Interpreter as IPythonInterpreterFactoryWithDatabase;
            if (withDb == null) {
                return;
            }

            AnalysisProgress update;
            if (updateInfo.TryGetValue(_identifier, out update)) {
                if (!IsRunning) {
                    // We're analyzing, but we weren't started by this process.
                    IsRunning = true;
                }

                // We've received a message, so stop worrying about the timeout.
                _expectFirstUpdateBy = DateTime.MinValue;

                if (update.Progress < int.MaxValue) {
                    Dispatcher.BeginInvoke((Action)(() => {
                        if (update.Progress > Progress || update.Maximum != Maximum) {
                            Progress = update.Progress;
                            Maximum = update.Maximum;
                        }
                        Message = update.Message;
                    }));
                } else {
                    Dispatcher.BeginInvoke((Action)(() => {
                        Progress = 0;
                        Message = "Starting refresh DB";
                    }));
                }
            } else if (IsRunning && DateTime.Now > _expectFirstUpdateBy) {
                IsRunning = false;
            }
        }

        internal void DefaultInterpreterUpdate(IPythonInterpreterFactory newDefault) {
            if (Dispatcher.CheckAccess()) {
                IsDefault = (Interpreter == newDefault);
            } else {
                Dispatcher.BeginInvoke((Action)(() => DefaultInterpreterUpdate(newDefault)));
            }
        }

        public void Start() {
            if (Dispatcher.CheckAccess()) {
                var withDb = Interpreter as IPythonInterpreterFactoryWithDatabase;
                if (withDb != null) {
                    // Expect the first update within 10 seconds or else stop
                    // running.
                    _expectFirstUpdateBy = DateTime.Now + TimeSpan.FromSeconds(10);
                    IsRunning = true;
                    Message = "Starting refresh DB";
                    withDb.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged, exitCode => {
                        if (exitCode != PythonTypeDatabase.AlreadyGeneratingExitCode) {
                            IsRunning = false;
                        }
                    });
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

        public PythonProjectNode Project {
            get;
            private set;
        }

        public string Name {
            get { return (string)GetValue(NameProperty); }
            private set { SafeSetValue(NamePropertyKey, value); }
        }

        public string SubName {
            get { return (string)GetValue(SubNameProperty); }
            private set { SafeSetValue(SubNamePropertyKey, value); }
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
            get { return _isRunning; }
            private set { SafeSetValue(IsRunningPropertyKey, value); }
        }

        public bool IsCheckingDatabase {
            get { return (bool)GetValue(IsCheckingDatabaseProperty); }
            private set { SafeSetValue(IsCheckingDatabasePropertyKey, value); }
        }

        public bool IsCurrent {
            get { return (bool)SafeGetValue(IsCurrentProperty); }
            private set { SafeSetValue(IsCurrentPropertyKey, value); }
        }

        public int Progress {
            get { return (int)GetValue(ProgressProperty); }
            private set { SetValue(ProgressPropertyKey, value); }
        }

        public string Message {
            get { return (string)GetValue(MessageProperty); }
            private set { SafeSetValue(MessagePropertyKey, value); }
        }

        public int Maximum {
            get { return (int)GetValue(MaximumProperty); }
            private set { SetValue(MaximumPropertyKey, value); }
        }

        public bool IsDefault {
            get { return (bool)GetValue(IsDefaultProperty); }
            private set { SetValue(IsDefaultPropertyKey, value); }
        }

        public bool CanBeDefault {
            get { return (bool)GetValue(CanBeDefaultProperty); }
            private set { SetValue(CanBeDefaultPropertyKey, value); }
        }

        public bool IsSelected {
            get { return (bool)SafeGetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
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
        private static readonly DependencyPropertyKey SubNamePropertyKey = DependencyProperty.RegisterReadOnly("SubName", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsCurrentReasonPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrentReason", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        private static readonly DependencyPropertyKey IsCurrentPropertyKey = DependencyProperty.RegisterReadOnly("IsCurrent", typeof(bool), typeof(InterpreterView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey IsRunningPropertyKey = DependencyProperty.RegisterReadOnly("IsRunning", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false, IsRunning_Changed));
        private static readonly DependencyPropertyKey IsCheckingDatabasePropertyKey = DependencyProperty.RegisterReadOnly("IsCheckingDatabase", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly("Progress", typeof(int), typeof(InterpreterView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey MessagePropertyKey = DependencyProperty.RegisterReadOnly("Message", typeof(string), typeof(InterpreterView), new PropertyMetadata());
        private static readonly DependencyPropertyKey MaximumPropertyKey = DependencyProperty.RegisterReadOnly("Maximum", typeof(int), typeof(InterpreterView), new PropertyMetadata(0));
        private static readonly DependencyPropertyKey IsDefaultPropertyKey = DependencyProperty.RegisterReadOnly("IsDefault", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));
        private static readonly DependencyPropertyKey CanBeDefaultPropertyKey = DependencyProperty.RegisterReadOnly("CanBeDefault", typeof(bool), typeof(InterpreterView), new PropertyMetadata(true));
        private static readonly DependencyPropertyKey CanRefreshPropertyKey = DependencyProperty.RegisterReadOnly("CanRefresh", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));

        public static readonly DependencyProperty NameProperty = NamePropertyKey.DependencyProperty;
        public static readonly DependencyProperty SubNameProperty = SubNamePropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentReasonProperty = IsCurrentReasonPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCurrentProperty = IsCurrentPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsCheckingDatabaseProperty = IsCheckingDatabasePropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsRunningProperty = IsRunningPropertyKey.DependencyProperty;
        public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;
        public static readonly DependencyProperty MessageProperty = MessagePropertyKey.DependencyProperty;
        public static readonly DependencyProperty MaximumProperty = MaximumPropertyKey.DependencyProperty;
        public static readonly DependencyProperty IsDefaultProperty = IsDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanBeDefaultProperty = CanBeDefaultPropertyKey.DependencyProperty;
        public static readonly DependencyProperty CanRefreshProperty = CanRefreshPropertyKey.DependencyProperty;

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(InterpreterView), new PropertyMetadata(false));

        private static void IsRunning_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(e.NewValue as bool? ?? true)) {
                d.SetValue(ProgressPropertyKey, 0);
                d.SetValue(MaximumPropertyKey, 0);
            }
            var iv = d as InterpreterView;
            if (iv != null) {
                iv._isRunning = (e.NewValue as bool?) ?? false;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);
            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(e.Property.Name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private sealed class InterpreterViewComparer : IEqualityComparer<InterpreterView>, IComparer<InterpreterView> {
            public bool Equals(InterpreterView x, InterpreterView y) {
                return ReferenceEquals(
                    x == null ? null : x.Interpreter,
                    y == null ? null : y.Interpreter
                );
            }

            public int GetHashCode(InterpreterView obj) {
                return obj == null || obj.Interpreter == null ? 0 : obj.Interpreter.GetHashCode();
            }

            public int Compare(InterpreterView x, InterpreterView y) {
                return StringComparer.CurrentCultureIgnoreCase.Compare(
                    x == null ? "" : x.Name,
                    y == null ? "" : y.Name
                );
            }
        }
    }
}
