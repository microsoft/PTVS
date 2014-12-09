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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonGeneralOptionsPage : PythonDialogPage {
        private PythonGeneralOptionsControl _window;

        public PythonGeneralOptionsPage()
            : base("General") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonGeneralOptionsControl();
                    LoadSettingsFromStorage();
                }
                return _window;
            }
        }

        /// <summary>
        /// The frequency at which to check for updated news. Default is once
        /// per week.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public SurveyNewsPolicy SurveyNewsCheck {
            get { return PyService.GeneralOptions.SurveyNewsCheck; }
            set { PyService.GeneralOptions.SurveyNewsCheck = value; }
        }

        /// <summary>
        /// The date/time when the last check for news occurred.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public DateTime SurveyNewsLastCheck {
            get { return PyService.GeneralOptions.SurveyNewsLastCheck; }
            set { PyService.GeneralOptions.SurveyNewsLastCheck = value; }
        }

        /// <summary>
        /// The url of the news feed.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public string SurveyNewsFeedUrl {
            get { return PyService.GeneralOptions.SurveyNewsFeedUrl; }
            set { PyService.GeneralOptions.SurveyNewsFeedUrl = value; }
        }

        /// <summary>
        /// The url of the news index page.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public string SurveyNewsIndexUrl {
            get { return PyService.GeneralOptions.SurveyNewsIndexUrl; }
            set { PyService.GeneralOptions.SurveyNewsIndexUrl = value; }
        }

        /// <summary>
        /// Show the output window for virtual environment creation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool ShowOutputWindowForVirtualEnvCreate {
            get { return PyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate; }
            set { PyService.GeneralOptions.ShowOutputWindowForVirtualEnvCreate = value; }
        }

        /// <summary>
        /// Show the output window for package installation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool ShowOutputWindowForPackageInstallation {
            get { return PyService.GeneralOptions.ShowOutputWindowForPackageInstallation; }
            set { PyService.GeneralOptions.ShowOutputWindowForPackageInstallation = value; }
        }

        /// <summary>
        /// True to always run pip elevated when installing or uninstalling
        /// packages.
        /// </summary>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool ElevatePip {
            get { return PyService.GeneralOptions.ElevatePip; }
            set { PyService.GeneralOptions.ElevatePip = value; }
        }

        /// <summary>
        /// True to always run easy_install elevated when installing packages.
        /// </summary>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool ElevateEasyInstall {
            get { return PyService.GeneralOptions.ElevateEasyInstall; }
            set { PyService.GeneralOptions.ElevateEasyInstall = value; }
        }

        /// <summary>
        /// True to warn when a module is not resolved.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool UnresolvedImportWarning {
            get { return PyService.GeneralOptions.UnresolvedImportWarning; }
            set { PyService.GeneralOptions.UnresolvedImportWarning = value; }
        }

        /// <summary>
        /// True to mask global environment paths when launching projects.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool ClearGlobalPythonPath {
            get { return PyService.GeneralOptions.ClearGlobalPythonPath; }
            set { PyService.GeneralOptions.ClearGlobalPythonPath = value; }
        }

        /// <summary>
        /// True to start analyzing an environment when it is used and has no
        /// database. Default is true.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool AutoAnalyzeStandardLibrary {
            get { return PyService.GeneralOptions.AutoAnalyzeStandardLibrary; }
            set { PyService.GeneralOptions.AutoAnalyzeStandardLibrary = value; }
        }

        /// <summary>
        /// True to update search paths when adding linked files. Default is
        /// true.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public bool UpdateSearchPathsWhenAddingLinkedFiles {
            get { return PyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles; }
            set { PyService.GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles = value; }
        }

        /// <summary>
        /// The severity to apply to inconsistent indentation. Default is warn.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        [Obsolete("Use PythonToolsService.GeneralOptions instead")]
        public Severity IndentationInconsistencySeverity {
            get { return PyService.GeneralOptions.IndentationInconsistencySeverity; }
            set { PyService.GeneralOptions.IndentationInconsistencySeverity = value; }
        }

        [Obsolete("Use PythonToolsService.DebuggerOptions instead")]
        public event EventHandler IndentationInconsistencyChanged {
            add {
                PyService.GeneralOptions.IndentationInconsistencyChanged += value;
            }
            remove {
                PyService.GeneralOptions.IndentationInconsistencyChanged -= value;
            }
        }

        /// <summary>
        /// Resets settings back to their defaults. This should be followed by
        /// a call to <see cref="SaveSettingsToStorage"/> to commit the new
        /// values.
        /// </summary>
        public override void ResetSettings() {
            PyService.GeneralOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            // Load settings from storage.            
            PyService.GeneralOptions.Load();

            // Synchronize UI with backing properties.
            if (_window != null) {
                _window.SyncControlWithPageSettings(PyService);
            }
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            if (_window != null) {
                _window.SyncPageWithControlSettings(PyService);
            }

            // Save settings.
            PyService.GeneralOptions.Save();
        }
    }
}
