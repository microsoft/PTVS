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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.InterpreterList {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif
    internal partial class InterpreterList : UserControl, IDisposable {
        AnalyzerStatusListener _listener;
        readonly object _listenerLock = new object();
        int _refreshCount;

        readonly List<InterpreterView> _interpreters;
        readonly DispatcherTimer _refreshTimer;
        readonly IInterpreterOptionsService _interpreterService;
        readonly IVsSolution _solutionService;
        readonly SolutionEventsListener _solutionEvents;
        readonly Dictionary<IVsProject, PythonProjectNode> _hookedProjects;

        public static readonly RoutedCommand RefreshCommand = new RoutedUICommand("_Refresh", "Refresh", typeof(InterpreterList));
        public static readonly RoutedCommand RegenerateCommand = new RoutedUICommand("Refresh", "Regenerate", typeof(InterpreterList));
        public static readonly RoutedCommand AbortCommand = new RoutedUICommand("Abort", "Abort", typeof(InterpreterList));
        public static readonly RoutedCommand OpenReplCommand = new RoutedUICommand("Interactive Window", "OpenInteractive", typeof(InterpreterList));
        public static readonly RoutedCommand OpenOptionsCommand = new RoutedUICommand("Options", "OpenOptions", typeof(InterpreterList));
        public static readonly RoutedCommand OpenReplOptionsCommand = new RoutedUICommand("Interactive Options", "OpenInteractiveOptions", typeof(InterpreterList));
        public static readonly RoutedCommand OpenPathCommand = new RoutedUICommand("View in File Explorer", "OpenPath", typeof(InterpreterList));
        public static readonly RoutedCommand MakeDefaultCommand = new RoutedUICommand("Make Default", "MakeDefault", typeof(InterpreterList));
        public static readonly RoutedCommand CopyReasonCommand = new RoutedUICommand("Copy", "CopyReason", typeof(InterpreterList));
        public static readonly RoutedCommand WebChooseInterpreterCommand = new RoutedUICommand("Help me choose an interpreter", "WebChooseInterpreter", typeof(InterpreterList));

        internal InterpreterList(IInterpreterOptionsService service)
            : this(service, null, true) { }

        public InterpreterList(IInterpreterOptionsService service, IServiceProvider provider)
            : this(service, provider, false) { }

        private InterpreterList(IInterpreterOptionsService service, IServiceProvider provider, bool ignoreProvider) {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(500.0);
            _refreshTimer.Tick += AutoRefresh_Elapsed;
            _interpreterService = service;

            if (!ignoreProvider) {
                _solutionService = provider.GetService(typeof(SVsSolution)) as IVsSolution;
                if (_solutionService != null) {
                    _hookedProjects = new Dictionary<IVsProject, PythonProjectNode>();
                }
            }

            _interpreters = new List<InterpreterView>();
            Interpreters = new BulkObservableCollection<InterpreterView>();

            InterpretersChanged(this, EventArgs.Empty);

            _interpreterService.InterpretersChanged += InterpretersChanged;
            _interpreterService.DefaultInterpreterChanged += DefaultInterpreterChanged;

            if (_solutionService != null) {
                _solutionEvents = new SolutionEventsListener(_solutionService);
                _solutionEvents.ProjectLoaded += ProjectInterpretersAdded;
                _solutionEvents.ProjectUnloading += ProjectInterpretersRemoved;
                _solutionEvents.ProjectClosing += ProjectInterpretersRemoved;
                _solutionEvents.ProjectRenamed += ProjectInterpretersAdded;
            }

            DataContext = this;

            _listener = new AnalyzerStatusListener(Update);
            InitializeComponent();

            _refreshTimer.Start();

            if (_solutionEvents != null) {
                _solutionEvents.StartListeningForChanges();
            }
        }

        private void ProjectInterpretersRemoved(object sender, ProjectEventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => ProjectInterpretersRemoved(sender, e)));
                return;
            }

            lock (_interpreters) {
                PythonProjectNode project;
                if (_hookedProjects.TryGetValue(e.Project, out project)) {
                    for (int i = 0; i < Interpreters.Count; ) {
                        if (_interpreters[i].Project == project) {
                            _interpreters[i].PropertyChanged -= View_PropertyChanged;
                            _interpreters.RemoveAt(i);
                            Interpreters.RemoveAt(i);
                        } else {
                            i += 1;
                        }
                    }

                    if (project.Interpreters != null) {
                        project.Interpreters.InterpreterFactoriesChanged -= InterpretersChanged;
                        _hookedProjects.Remove(e.Project);
                    }
                }
            }
            // The column only resizes when the value changes. Since it's
            // currently set to NaN, we need to set it to something else
            // first. Setting it to ActualWidth results in no visible
            // effects to the user, since the column is already ActualWidth
            // wide.
            if (nameColumn != null) {
                nameColumn.Width = nameColumn.ActualWidth;
                nameColumn.Width = double.NaN;
            }
        }

        private void ProjectInterpretersAdded(object sender, ProjectEventArgs e) {
            InterpretersChanged(sender, EventArgs.Empty);
        }

        void IDisposable.Dispose() {
            if (_solutionEvents != null) {
                _solutionEvents.Dispose();
            }
            lock (_listenerLock) {
                _listener.Dispose();
            }
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
        }

        void InterpretersChanged(object sender, EventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => InterpretersChanged(sender, e)));
                return;
            }

            lock (_interpreters) {
                foreach (var interp in _interpreters) {
                    interp.PropertyChanged -= View_PropertyChanged;
                }
                if (_hookedProjects != null) {
                    foreach (var project in _hookedProjects.Values) {
                        if (project.Interpreters != null) {
                            project.Interpreters.ActiveInterpreterChanged -= InterpretersChanged;
                        }
                    }
                    _hookedProjects.Clear();
                }

                _interpreters.Clear();

                _interpreters.AddRange(InterpreterView.GetInterpreters(_interpreterService));
                if (_hookedProjects != null) {
                    foreach (var project in _solutionService.EnumerateLoadedProjects()) {
                        var pyProject = project.GetPythonProject();
                        if (pyProject == null) {
                            continue;
                        }
                        var interp = pyProject.Interpreters;
                        if (interp == null) {
                            continue;
                        }
                        _hookedProjects[project] = pyProject;
                        interp.InterpreterFactoriesChanged += InterpretersChanged;
                        _interpreters.AddRange(
                            from f in interp.GetInterpreterFactories()
                            // Interpreter references are already included
                            where interp.IsProjectSpecific(f)
                            select new InterpreterView(f, f.Description, pyProject)
                        );
                    }
                }

                foreach (var interp in _interpreters) {
                    interp.PropertyChanged += View_PropertyChanged;
                }
                Interpreters.Clear();
                ((BulkObservableCollection<InterpreterView>)Interpreters).AddRange(_interpreters);
            }
            // The column only resizes when the value changes. Since it's
            // currently set to NaN, we need to set it to something else
            // first. Setting it to ActualWidth results in no visible
            // effects to the user, since the column is already ActualWidth
            // wide.
            if (nameColumn != null) {
                nameColumn.Width = nameColumn.ActualWidth;
                nameColumn.Width = double.NaN;
            }
        }

        void DefaultInterpreterChanged(object sender, EventArgs e) {
            InterpreterView[] interps;
            lock (_interpreters) {
                interps = _interpreters.ToArray();
            }
            foreach (var interp in interps) {
                interp.DefaultInterpreterUpdate(_interpreterService.DefaultInterpreter);
            }
        }

        private void AutoRefresh_Elapsed(object sender, EventArgs e) {
            Debug.Assert(Dispatcher.CheckAccess());
            lock (_listenerLock) {
                _listener.ThrowPendingExceptions();
                _listener.RequestUpdate();
            }
        }

        public ObservableCollection<InterpreterView> Interpreters {
            get { return (ObservableCollection<InterpreterView>)GetValue(InterpretersProperty); }
            private set { SetValue(InterpretersPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey InterpretersPropertyKey = DependencyProperty.RegisterReadOnly("Interpreters", typeof(ObservableCollection<InterpreterView>), typeof(InterpreterList), new PropertyMetadata());
        public static readonly DependencyProperty InterpretersProperty = InterpretersPropertyKey.DependencyProperty;


        private void Abort_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = false;

            // TODO: Uncomment when analysis can be aborted
            //var interp = e.Parameter as InterpreterView;
            //e.CanExecute = interp != null && interp.IsRunning;
        }

        private void Abort_Executed(object sender, ExecutedRoutedEventArgs e) {
            // TODO: Uncomment when analysis can be aborted
            //((InterpreterView)e.Parameter).Abort();
        }

        private void Refresh_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            try {
                lock (_listenerLock) {
                    _listener.ThrowPendingExceptions();
                }
                e.CanExecute = true;
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                Debug.WriteLine(ex.ToString());
                e.CanExecute = false;
            }
        }

        private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e) {
            lock (_listenerLock) {
                if (++_refreshCount >= 120) {
                    _refreshCount = 0;
                    _listener.Dispose();
                    _listener = new AnalyzerStatusListener(Update);
                    _listener.WaitForWorkerStarted();
                    _listener.ThrowPendingExceptions();
                }
                _listener.RequestUpdate();
            }
        }

        internal void Update(Dictionary<string, AnalysisProgress> data) {
            InterpreterView[] interps;
            lock (_interpreters) {
                interps = _interpreters.ToArray();
            }
            foreach (var interp in interps) {
                interp.ProgressUpdate(data);
            }
        }


        private void Regenerate_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var interp = e.Parameter as InterpreterView;
            e.CanExecute = interp != null && interp.CanRefresh && !interp.IsRunning;
        }

        private void Regenerate_Executed(object sender, ExecutedRoutedEventArgs e) {
            ((InterpreterView)e.Parameter).Start();
        }

        private void OpenRepl_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as InterpreterView;
            e.CanExecute = view != null && view.Interpreter != null &&
                !string.IsNullOrEmpty(view.Interpreter.Configuration.InterpreterPath) &&
                File.Exists(view.Interpreter.Configuration.InterpreterPath);
        }

        private void OpenRepl_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (InterpreterView)e.Parameter;
            var interp = view.Interpreter;
            IReplWindow window;

            try {
                window = ExecuteInReplCommand.EnsureReplWindow(interp, view.Project);
            } catch (InvalidOperationException ex) {
                MessageBox.Show(SR.GetString(SR.ErrorOpeningInteractiveWindow, ex), SR.ProductName);
                return;
            }
            if (window != null) {
                var pane = window as ToolWindowPane;
                if (pane != null) {
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)pane.Frame).Show());
                }
                window.Focus();
            }
        }


        private void OpenWindow_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as InterpreterView;
            e.CanExecute = e.Parameter == null || (view != null && view.Interpreter != null && view.Project == null);
        }

        private void OpenWindow_Executed(object sender, ExecutedRoutedEventArgs e) {
            IPythonInterpreterFactory interp = null;
            if (e.Parameter != null) {
                interp = ((InterpreterView)e.Parameter).Interpreter;
            }
            if (e.Command == OpenOptionsCommand) {
                PythonToolsPackage.Instance.ShowOptionPage(typeof(PythonInterpreterOptionsPage), interp);

            } else if (e.Command == OpenReplOptionsCommand) {
                PythonToolsPackage.Instance.ShowOptionPage(typeof(PythonInteractiveOptionsPage), interp);
            }
        }

        private void MakeDefault_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as InterpreterView;

            // Check to see if the interpreter is associated with a project. If it is, then it's a virtual
            // environment, so change the logic to just check if it's the project's current active interpreter.
            // If not, then check to see if it can be set as default.
            if (view.Project != null) {
                e.CanExecute = view.Project.Interpreters.ActiveInterpreter != view.Interpreter;
            } else {
                e.CanExecute = view != null && view.Interpreter != null && !view.IsDefault && view.CanBeDefault;
            }
        }

        private void MakeDefault_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (InterpreterView)e.Parameter;

            // Check to see if the interpreter is associated with a project. If it is, then it's a virtual
            // environment, so change the logic to just activate it for the project. If not, then it's for the
            // global scope, so set it as default on the interpreter service.
            if (view.Project != null) {
                // Safe to ignore failure here, because we don't update state
                // until the event comes through.
                view.Project.SetInterpreterFactory(view.Interpreter);
            } else {
                _interpreterService.DefaultInterpreter = view.Interpreter;
            }
        }

        private void CopyReason_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as InterpreterView;
            e.CanExecute = view != null && view.Interpreter is IPythonInterpreterFactoryWithDatabase && !view.IsCurrent;
        }

        private void CopyReason_Executed(object sender, ExecutedRoutedEventArgs e) {
            var withDb = (IPythonInterpreterFactoryWithDatabase)((InterpreterView)e.Parameter).Interpreter;
            Clipboard.SetText(withDb.GetIsCurrentReason(CultureInfo.CurrentUICulture));
        }

        private void OpenPath_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as InterpreterView;
            e.CanExecute = view != null && view.Interpreter != null &&
                Directory.Exists(view.Interpreter.Configuration.PrefixPath);
        }

        private void OpenPath_Executed(object sender, ExecutedRoutedEventArgs e) {
            Process.Start(new ProcessStartInfo {
                FileName = ((InterpreterView)e.Parameter).Interpreter.Configuration.PrefixPath,
                Verb = "open",
                UseShellExecute = true
            });
        }

        private void WebChooseInterpreter_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void WebChooseInterpreter_Executed(object sender, ExecutedRoutedEventArgs e) {
            PythonToolsPackage.OpenWebBrowser(PythonToolsPackage.InterpreterHelpUrl);
        }
    }
}
