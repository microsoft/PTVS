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
        private SurveyNewsPolicy _surveyNewsCheck;
        private DateTime _surveyNewsLastCheck;
        private string _surveyNewsFeedUrl;
        private string _surveyNewsIndexUrl;
        private PythonGeneralOptionsControl _window;
        private bool _showOutputWindowForVirtualEnvCreate;
        private bool _showOutputWindowForPackageInstallation;
        private bool _elevatePip;
        private bool _elevateEasyInstall;
        private bool _unresolvedImportWarning;
        private bool _clearGlobalPythonPath;

        private PythonDebuggingOptionsPage _debugOptions;

        public PythonGeneralOptionsPage()
            : base("General") {
        }

        private PythonDebuggingOptionsPage DebugOptions {
            get {
                if (_debugOptions == null) {
                    _debugOptions = PythonToolsPackage.Instance.DebuggingOptionsPage;
                }
                return _debugOptions;
            }
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonGeneralOptionsControl();
                }
                return _window;
            }
        }

        /// <summary>
        /// The frequency at which to check for updated news. Default is once
        /// per week.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public SurveyNewsPolicy SurveyNewsCheck {
            get { return _surveyNewsCheck; }
            set { _surveyNewsCheck = value; }
        }

        /// <summary>
        /// The date/time when the last check for news occurred.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public DateTime SurveyNewsLastCheck {
            get { return _surveyNewsLastCheck; }
            set { _surveyNewsLastCheck = value; }
        }

        /// <summary>
        /// The url of the news feed.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public string SurveyNewsFeedUrl {
            get { return _surveyNewsFeedUrl; }
            set { _surveyNewsFeedUrl = value; }
        }

        /// <summary>
        /// The url of the news index page.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public string SurveyNewsIndexUrl {
            get { return _surveyNewsIndexUrl; }
            set { _surveyNewsIndexUrl = value; }
        }

        /// <summary>
        /// Show the output window for virtual environment creation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool ShowOutputWindowForVirtualEnvCreate {
            get { return _showOutputWindowForVirtualEnvCreate; }
            set { _showOutputWindowForVirtualEnvCreate = value; }
        }

        /// <summary>
        /// Show the output window for package installation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool ShowOutputWindowForPackageInstallation {
            get { return _showOutputWindowForPackageInstallation; }
            set { _showOutputWindowForPackageInstallation = value; }
        }

        /// <summary>
        /// True to always run pip elevated when installing or uninstalling
        /// packages.
        /// </summary>
        public bool ElevatePip {
            get { return _elevatePip; }
            set { _elevatePip = value; }
        }

        /// <summary>
        /// True to always run easy_install elevated when installing packages.
        /// </summary>
        public bool ElevateEasyInstall {
            get { return _elevateEasyInstall; }
            set { _elevateEasyInstall = value; }
        }

        /// <summary>
        /// True to warn when a module is not resolved.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        public bool UnresolvedImportWarning {
            get { return _unresolvedImportWarning; }
            set { _unresolvedImportWarning = value; }
        }

        /// <summary>
        /// True to mask global environment paths when launching projects.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        public bool ClearGlobalPythonPath {
            get { return _clearGlobalPythonPath; }
            set { _clearGlobalPythonPath = value; }
        }

        /// <summary>
        /// True to start analyzing an environment when it is used and has no
        /// database. Default is true.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        public bool AutoAnalyzeStandardLibrary {
            get { return DebugOptions.AutoAnalyzeStandardLibrary; }
            set { DebugOptions.AutoAnalyzeStandardLibrary = value; }
        }

        /// <summary>
        /// True to update search paths when adding linked files. Default is
        /// true.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        public bool UpdateSearchPathsWhenAddingLinkedFiles {
            get { return DebugOptions.UpdateSearchPathsWhenAddingLinkedFiles; }
            set { DebugOptions.UpdateSearchPathsWhenAddingLinkedFiles = value; }
        }

        /// <summary>
        /// The severity to apply to inconsistent indentation. Default is warn.
        /// </summary>
        /// <remarks>
        /// This option lives in <see cref="PythonDebugOptionsPage"/> for
        /// backwards-compatibility.
        /// </remarks>
        public Severity IndentationInconsistencySeverity {
            get { return DebugOptions.IndentationInconsistencySeverity; }
            set { DebugOptions.IndentationInconsistencySeverity = value; }
        }

        public event EventHandler IndentationInconsistencyChanged {
            add {
                DebugOptions.IndentationInconsistencyChanged += value;
            }
            remove {
                DebugOptions.IndentationInconsistencyChanged -= value;
            }
        }

        /// <summary>
        /// Resets settings back to their defaults. This should be followed by
        /// a call to <see cref="SaveSettingsToStorage"/> to commit the new
        /// values.
        /// </summary>
        public override void ResetSettings() {
            _surveyNewsCheck = SurveyNewsPolicy.CheckOnceWeek;
            _surveyNewsLastCheck = DateTime.MinValue;
            _surveyNewsFeedUrl = DefaultSurveyNewsFeedUrl;
            _surveyNewsIndexUrl = DefaultSurveyNewsIndexUrl;
            _showOutputWindowForVirtualEnvCreate = true;
            _showOutputWindowForPackageInstallation = true;
            _elevatePip = false;
            _elevateEasyInstall = false;
            _unresolvedImportWarning = true;
            _clearGlobalPythonPath = true;
            DebugOptions.ResetGeneralSettings();
        }

        private const string DefaultSurveyNewsFeedUrl = "http://go.microsoft.com/fwlink/?LinkId=303967";
        private const string DefaultSurveyNewsIndexUrl = "http://go.microsoft.com/fwlink/?LinkId=309158";

        private const string ShowOutputWindowForVirtualEnvCreateSetting = "ShowOutputWindowForVirtualEnvCreate";
        private const string ShowOutputWindowForPackageInstallationSetting = "ShowOutputWindowForPackageInstallation";
        private const string ElevatePipSetting = "ElevatePip";
        private const string ElevateEasyInstallSetting = "ElevateEasyInstall";
        private const string SurveyNewsCheckSetting = "SurveyNewsCheck";
        private const string SurveyNewsLastCheckSetting = "SurveyNewsLastCheck";
        private const string SurveyNewsFeedUrlSetting = "SurveyNewsFeedUrl";
        private const string SurveyNewsIndexUrlSetting = "SurveyNewsIndexUrl";
        private const string UnresolvedImportWarningSetting = "UnresolvedImportWarning";
        private const string ClearGlobalPythonPathSetting = "ClearGlobalPythonPath";

        public override void LoadSettingsFromStorage() {
            _surveyNewsCheck = LoadEnum<SurveyNewsPolicy>(SurveyNewsCheckSetting) ?? SurveyNewsPolicy.CheckOnceWeek;
            _surveyNewsLastCheck = LoadDateTime(SurveyNewsLastCheckSetting) ?? DateTime.MinValue;
            _surveyNewsFeedUrl = LoadString(SurveyNewsFeedUrlSetting) ?? DefaultSurveyNewsFeedUrl;
            _surveyNewsIndexUrl = LoadString(SurveyNewsIndexUrlSetting) ?? DefaultSurveyNewsIndexUrl;
            _showOutputWindowForVirtualEnvCreate = LoadBool(ShowOutputWindowForVirtualEnvCreateSetting) ?? true;
            _showOutputWindowForPackageInstallation = LoadBool(ShowOutputWindowForPackageInstallationSetting) ?? true;
            _elevatePip = LoadBool(ElevatePipSetting) ?? false;
            _elevateEasyInstall = LoadBool(ElevateEasyInstallSetting) ?? false;
            _unresolvedImportWarning = LoadBool(UnresolvedImportWarningSetting) ?? true;
            _clearGlobalPythonPath = LoadBool(ClearGlobalPythonPathSetting) ?? true;
            DebugOptions.LoadGeneralSettingsFromStorage();
        }

        public override void SaveSettingsToStorage() {
            SaveEnum(SurveyNewsCheckSetting, _surveyNewsCheck);
            SaveDateTime(SurveyNewsLastCheckSetting, _surveyNewsLastCheck);
            SaveBool(ShowOutputWindowForVirtualEnvCreateSetting, _showOutputWindowForVirtualEnvCreate);
            SaveBool(ShowOutputWindowForPackageInstallationSetting, _showOutputWindowForPackageInstallation);
            SaveBool(ElevatePipSetting, _elevatePip);
            SaveBool(ElevateEasyInstallSetting, _elevateEasyInstall);
            SaveBool(UnresolvedImportWarningSetting, _unresolvedImportWarning);
            SaveBool(ClearGlobalPythonPathSetting, _clearGlobalPythonPath);
            DebugOptions.SaveGeneralSettingsToStorage();
        }
    }
}
