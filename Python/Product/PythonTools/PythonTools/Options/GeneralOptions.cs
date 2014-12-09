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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    public sealed class GeneralOptions{
        private readonly PythonToolsService _pyService;
        private Severity _indentationInconsistencySeverity;

        private const string AdvancedCategory = "Advanced";
        private const string GeneralCategory = "Advanced";

        private const string IndentationInconsistencySeveritySetting = "IndentationInconsistencySeverity";
        private const string AutoAnalysisSetting = "AutoAnalysis";
        private const string CrossModuleAnalysisLimitSetting = "CrossModuleAnalysisLimit";
        private const string UpdateSearchPathsWhenAddingLinkedFilesSetting = "UpdateSearchPathsWhenAddingLinkedFiles";

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

        private const string DefaultSurveyNewsFeedUrl = "http://go.microsoft.com/fwlink/?LinkId=303967";
        private const string DefaultSurveyNewsIndexUrl = "http://go.microsoft.com/fwlink/?LinkId=309158";

        internal GeneralOptions(PythonToolsService service) {
            _pyService = service;
            Load();
        }

        public void Load() {
            SurveyNewsCheck = _pyService.LoadEnum<SurveyNewsPolicy>(SurveyNewsCheckSetting, GeneralCategory) ?? SurveyNewsPolicy.CheckOnceWeek;
            SurveyNewsLastCheck = _pyService.LoadDateTime(SurveyNewsLastCheckSetting, GeneralCategory) ?? DateTime.MinValue;
            SurveyNewsFeedUrl = _pyService.LoadString(SurveyNewsFeedUrlSetting, GeneralCategory) ?? DefaultSurveyNewsFeedUrl;
            SurveyNewsIndexUrl = _pyService.LoadString(SurveyNewsIndexUrlSetting, GeneralCategory) ?? DefaultSurveyNewsIndexUrl;
            ShowOutputWindowForVirtualEnvCreate = _pyService.LoadBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory) ?? true;
            ShowOutputWindowForPackageInstallation = _pyService.LoadBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory) ?? true;
            ElevatePip = _pyService.LoadBool(ElevatePipSetting, GeneralCategory) ?? false;
            ElevateEasyInstall = _pyService.LoadBool(ElevateEasyInstallSetting, GeneralCategory) ?? false;
            UnresolvedImportWarning = _pyService.LoadBool(UnresolvedImportWarningSetting, GeneralCategory) ?? true;
            ClearGlobalPythonPath = _pyService.LoadBool(ClearGlobalPythonPathSetting, GeneralCategory) ?? true;


            AutoAnalyzeStandardLibrary = _pyService.LoadBool(AutoAnalysisSetting, AdvancedCategory) ?? true;
            IndentationInconsistencySeverity = _pyService.LoadEnum<Severity>(IndentationInconsistencySeveritySetting, AdvancedCategory) ?? Severity.Warning;
            UpdateSearchPathsWhenAddingLinkedFiles = _pyService.LoadBool(UpdateSearchPathsWhenAddingLinkedFilesSetting, AdvancedCategory) ?? true;
            var analysisLimit = _pyService.LoadString(CrossModuleAnalysisLimitSetting, AdvancedCategory);
            if (analysisLimit == null) {
                CrossModuleAnalysisLimit = 1300;    // default analysis limit
            } else if (analysisLimit == "-") {
                CrossModuleAnalysisLimit = null;
            } else {
                CrossModuleAnalysisLimit = Convert.ToInt32(analysisLimit);
            }
        }

        public void Save() {
            _pyService.SaveEnum(SurveyNewsCheckSetting, GeneralCategory, SurveyNewsCheck);
            _pyService.SaveDateTime(SurveyNewsLastCheckSetting, GeneralCategory, SurveyNewsLastCheck);
            _pyService.SaveBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory, ShowOutputWindowForVirtualEnvCreate);
            _pyService.SaveBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory, ShowOutputWindowForPackageInstallation);
            _pyService.SaveBool(ElevatePipSetting, GeneralCategory, ElevatePip);
            _pyService.SaveBool(ElevateEasyInstallSetting, GeneralCategory, ElevateEasyInstall);
            _pyService.SaveBool(UnresolvedImportWarningSetting, GeneralCategory, UnresolvedImportWarning);
            _pyService.SaveBool(ClearGlobalPythonPathSetting, GeneralCategory, ClearGlobalPythonPath);

            _pyService.SaveBool(AutoAnalysisSetting, AdvancedCategory, AutoAnalyzeStandardLibrary);
            _pyService.SaveBool(UpdateSearchPathsWhenAddingLinkedFilesSetting, AdvancedCategory, UpdateSearchPathsWhenAddingLinkedFiles);
            _pyService.SaveEnum(IndentationInconsistencySeveritySetting, AdvancedCategory, _indentationInconsistencySeverity);
            if (CrossModuleAnalysisLimit != null) {
                _pyService.SaveInt(CrossModuleAnalysisLimitSetting, AdvancedCategory, CrossModuleAnalysisLimit.Value);
            } else {
                _pyService.SaveString(CrossModuleAnalysisLimitSetting, AdvancedCategory, "-");
            }
        }

        public void Reset() {
            SurveyNewsCheck = SurveyNewsPolicy.CheckOnceWeek;
            SurveyNewsLastCheck = DateTime.MinValue;
            SurveyNewsFeedUrl = DefaultSurveyNewsFeedUrl;
            SurveyNewsIndexUrl = DefaultSurveyNewsIndexUrl;
            ShowOutputWindowForVirtualEnvCreate = true;
            ShowOutputWindowForPackageInstallation = true;
            ElevatePip = false;
            ElevateEasyInstall = false;
            UnresolvedImportWarning = true;
            ClearGlobalPythonPath = true;

            IndentationInconsistencySeverity = Severity.Warning;
            AutoAnalyzeStandardLibrary = true;
            UpdateSearchPathsWhenAddingLinkedFiles = true;
            CrossModuleAnalysisLimit = null;
        }

        /// <summary>
        /// True to start analyzing an environment when it is used and has no
        /// database. Default is true.
        /// </summary>
        public bool AutoAnalyzeStandardLibrary {
            get;
            set;
        }

        /// <summary>
        /// The severity to apply to inconsistent indentation. Default is warn.
        /// </summary>
        public Severity IndentationInconsistencySeverity {
            get { return _indentationInconsistencySeverity; }
            set {
                _indentationInconsistencySeverity = value;
                var changed = IndentationInconsistencyChanged;
                if (changed != null) {
                    changed(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Maximum number of calls between modules to analyze. Default is 1300.
        /// </summary>
        public int? CrossModuleAnalysisLimit {
            get;
            set;
        }

        /// <summary>
        /// True to update search paths when adding linked files. Default is
        /// true.
        /// </summary>
        /// <remarks>New in 1.1</remarks>
        public bool UpdateSearchPathsWhenAddingLinkedFiles {
            get;
            set;
        }

        public event EventHandler IndentationInconsistencyChanged;

        /// <summary>
        /// The frequency at which to check for updated news. Default is once
        /// per week.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public SurveyNewsPolicy SurveyNewsCheck {
            get;
            set;
        }

        /// <summary>
        /// The date/time when the last check for news occurred.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public DateTime SurveyNewsLastCheck {
            get;
            set;
        }

        /// <summary>
        /// The url of the news feed.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public string SurveyNewsFeedUrl {
            get;
            set;
        }

        /// <summary>
        /// The url of the news index page.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public string SurveyNewsIndexUrl {
            get;
            set;
        }

        /// <summary>
        /// Show the output window for virtual environment creation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool ShowOutputWindowForVirtualEnvCreate {
            get;
            set;
        }

        /// <summary>
        /// Show the output window for package installation.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool ShowOutputWindowForPackageInstallation {
            get;
            set;
        }

        /// <summary>
        /// True to always run pip elevated when installing or uninstalling
        /// packages.
        /// </summary>
        public bool ElevatePip {
            get;
            set;
        }

        /// <summary>
        /// True to always run easy_install elevated when installing packages.
        /// </summary>
        public bool ElevateEasyInstall {
            get;
            set;
        }

        /// <summary>
        /// True to warn when a module is not resolved.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        public bool UnresolvedImportWarning {
            get;
            set;
        }

        /// <summary>
        /// True to mask global environment paths when launching projects.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        public bool ClearGlobalPythonPath {
            get;
            set;
        }
    }
}
