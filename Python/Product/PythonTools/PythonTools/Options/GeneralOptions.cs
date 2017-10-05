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
        private const string UnresolvedImportWarningSetting = "UnresolvedImportWarning";
        private const string ClearGlobalPythonPathSetting = "ClearGlobalPythonPath";

        private const string DefaultSurveyNewsFeedUrl = "https://go.microsoft.com/fwlink/?LinkId=303967";
        private const string DefaultSurveyNewsIndexUrl = "https://go.microsoft.com/fwlink/?LinkId=309158";

        internal GeneralOptions(PythonToolsService service) {
            _pyService = service;
        }

        public void Load() {
            ShowOutputWindowForVirtualEnvCreate = _pyService.LoadBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory) ?? true;
            ShowOutputWindowForPackageInstallation = _pyService.LoadBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory) ?? true;
            ElevatePip = _pyService.LoadBool(ElevatePipSetting, GeneralCategory) ?? false;
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
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _pyService.SaveBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory, ShowOutputWindowForVirtualEnvCreate);
            _pyService.SaveBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory, ShowOutputWindowForPackageInstallation);
            _pyService.SaveBool(ElevatePipSetting, GeneralCategory, ElevatePip);
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
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            ShowOutputWindowForVirtualEnvCreate = true;
            ShowOutputWindowForPackageInstallation = true;
            ElevatePip = false;
            UnresolvedImportWarning = true;
            ClearGlobalPythonPath = true;

            IndentationInconsistencySeverity = Severity.Warning;
            AutoAnalyzeStandardLibrary = true;
            UpdateSearchPathsWhenAddingLinkedFiles = true;
            CrossModuleAnalysisLimit = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

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
        [Obsolete]
        public SurveyNewsPolicy SurveyNewsCheck {
            get;
            set;
        }

        /// <summary>
        /// The date/time when the last check for news occurred.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete]
        public DateTime SurveyNewsLastCheck {
            get;
            set;
        }

        /// <summary>
        /// The url of the news feed.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete]
        public string SurveyNewsFeedUrl {
            get;
            set;
        }

        /// <summary>
        /// The url of the news index page.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        [Obsolete]
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
