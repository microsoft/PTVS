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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.Python.Parsing;

namespace Microsoft.PythonTools.Options {
    public sealed class PythonGeneralOptions{
        private readonly PythonToolsService _pyService;
        private Severity _indentationInconsistencySeverity;

        private const string AdvancedCategory = "Advanced";
        private const string GeneralCategory = "Advanced";

        private const string IndentationInconsistencySeveritySetting = "IndentationInconsistencySeverity";
        private const string UpdateSearchPathsWhenAddingLinkedFilesSetting = "UpdateSearchPathsWhenAddingLinkedFiles";

        private const string ShowOutputWindowForVirtualEnvCreateSetting = "ShowOutputWindowForVirtualEnvCreate";
        private const string ShowOutputWindowForPackageInstallationSetting = "ShowOutputWindowForPackageInstallation";
        private const string PromptForEnvCreateSetting = "PromptForEnvCreate";
        private const string PromptForPackageInstallationSetting = "PromptForPackageInstallation";
        private const string PromptForTestFrameWorkInfoBarSetting = "PromptForTestFrameWorkInfoBar";
        private const string PromptForPythonVersionNotSupportedInfoBarSetting = "PromptForPythonVersionNotSupportedInfoBarSetting";
        private const string ElevatePipSetting = "ElevatePip";
        private const string UnresolvedImportWarningSetting = "UnresolvedImportWarning";
        private const string InvalidEncodingWarningSetting = "InvalidEncodingWarningWarning";
        private const string ClearGlobalPythonPathSetting = "ClearGlobalPythonPath";

        internal PythonGeneralOptions(PythonToolsService service) {
            _pyService = service;
        }

        public void Load() {
            ShowOutputWindowForVirtualEnvCreate = _pyService.LoadBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory) ?? true;
            ShowOutputWindowForPackageInstallation = _pyService.LoadBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory) ?? true;
            PromptForEnvCreate = _pyService.LoadBool(PromptForEnvCreateSetting, GeneralCategory) ?? true;
            PromptForPackageInstallation = _pyService.LoadBool(PromptForPackageInstallationSetting, GeneralCategory) ?? true;
            PromptForTestFrameWorkInfoBar = _pyService.LoadBool(PromptForTestFrameWorkInfoBarSetting, GeneralCategory) ?? true;
            PromptForPythonVersionNotSupported = _pyService.LoadBool(PromptForPythonVersionNotSupportedInfoBarSetting, GeneralCategory) ?? true;

            ElevatePip = _pyService.LoadBool(ElevatePipSetting, GeneralCategory) ?? false;
            UnresolvedImportWarning = _pyService.LoadBool(UnresolvedImportWarningSetting, GeneralCategory) ?? true;
            InvalidEncodingWarning = _pyService.LoadBool(InvalidEncodingWarningSetting, GeneralCategory) ?? true;
            ClearGlobalPythonPath = _pyService.LoadBool(ClearGlobalPythonPathSetting, GeneralCategory) ?? true;

            IndentationInconsistencySeverity = _pyService.LoadEnum<Severity>(IndentationInconsistencySeveritySetting, AdvancedCategory) ?? Severity.Warning;
            UpdateSearchPathsWhenAddingLinkedFiles = _pyService.LoadBool(UpdateSearchPathsWhenAddingLinkedFilesSetting, AdvancedCategory) ?? true;

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _pyService.SaveBool(ShowOutputWindowForVirtualEnvCreateSetting, GeneralCategory, ShowOutputWindowForVirtualEnvCreate);
            _pyService.SaveBool(ShowOutputWindowForPackageInstallationSetting, GeneralCategory, ShowOutputWindowForPackageInstallation);
            _pyService.SaveBool(PromptForEnvCreateSetting, GeneralCategory, PromptForEnvCreate);
            _pyService.SaveBool(PromptForPackageInstallationSetting, GeneralCategory, PromptForPackageInstallation);
            _pyService.SaveBool(PromptForTestFrameWorkInfoBarSetting, GeneralCategory, PromptForTestFrameWorkInfoBar);
            _pyService.SaveBool(PromptForPythonVersionNotSupportedInfoBarSetting, GeneralCategory, PromptForPythonVersionNotSupported);
            _pyService.SaveBool(ElevatePipSetting, GeneralCategory, ElevatePip);
            _pyService.SaveBool(UnresolvedImportWarningSetting, GeneralCategory, UnresolvedImportWarning);
            _pyService.SaveBool(ClearGlobalPythonPathSetting, GeneralCategory, ClearGlobalPythonPath);
            _pyService.SaveBool(UpdateSearchPathsWhenAddingLinkedFilesSetting, AdvancedCategory, UpdateSearchPathsWhenAddingLinkedFiles);
            _pyService.SaveEnum(IndentationInconsistencySeveritySetting, AdvancedCategory, _indentationInconsistencySeverity);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            ShowOutputWindowForVirtualEnvCreate = true;
            ShowOutputWindowForPackageInstallation = true;
            PromptForEnvCreate = true;
            PromptForPackageInstallation = true;
            PromptForTestFrameWorkInfoBar = true;
            PromptForPythonVersionNotSupported = true;
            ElevatePip = false;
            UnresolvedImportWarning = true;
            ClearGlobalPythonPath = true;
            IndentationInconsistencySeverity = Severity.Warning;
            UpdateSearchPathsWhenAddingLinkedFiles = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

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
        /// Show an info bar to propose creating an environment.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool PromptForEnvCreate {
            get;
            set;
        }

        /// <summary>
        /// Show an info bar to propose installing missing packages.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool PromptForPackageInstallation {
            get;
            set;
        }

        /// <summary>
        /// Show an info bar to set up a testing framework
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool PromptForTestFrameWorkInfoBar {
            get;
            set;
        }

        /// <summary>
        /// Show an info bar if an unsupported Python version is in use
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public bool PromptForPythonVersionNotSupported {
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

        /// <summary>
        /// True to warn when a file encoding does not match Python 
        /// 'coding' designation in the beginning of the file.
        /// </summary>
        /// <remarks>New in 3.3</remarks>
        public bool InvalidEncodingWarning {
            get;
            set;
        }
    }
}
