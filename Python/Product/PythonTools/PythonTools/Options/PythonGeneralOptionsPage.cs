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

        public PythonGeneralOptionsPage()
            : base("General") {
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

        public override void LoadSettingsFromStorage() {
            _surveyNewsCheck = LoadEnum<SurveyNewsPolicy>(SurveyNewsCheckSetting) ?? SurveyNewsPolicy.CheckOnceWeek;
            _surveyNewsLastCheck = LoadDateTime(SurveyNewsLastCheckSetting) ?? DateTime.MinValue;
            _surveyNewsFeedUrl = LoadString(SurveyNewsFeedUrlSetting) ?? DefaultSurveyNewsFeedUrl;
            _surveyNewsIndexUrl = LoadString(SurveyNewsIndexUrlSetting) ?? DefaultSurveyNewsIndexUrl;
            _showOutputWindowForVirtualEnvCreate = LoadBool(ShowOutputWindowForVirtualEnvCreateSetting) ?? true;
            _showOutputWindowForPackageInstallation = LoadBool(ShowOutputWindowForPackageInstallationSetting) ?? true;
            _elevatePip = LoadBool(ElevatePipSetting) ?? false;
            _elevateEasyInstall = LoadBool(ElevateEasyInstallSetting) ?? false;
        }

        public override void SaveSettingsToStorage() {
            SaveEnum(SurveyNewsCheckSetting, _surveyNewsCheck);
            SaveDateTime(SurveyNewsLastCheckSetting, _surveyNewsLastCheck);
            SaveBool(ShowOutputWindowForVirtualEnvCreateSetting, _showOutputWindowForVirtualEnvCreate);
            SaveBool(ShowOutputWindowForPackageInstallationSetting, _showOutputWindowForPackageInstallation);
            SaveBool(ElevatePipSetting, _elevatePip);
            SaveBool(ElevateEasyInstallSetting, _elevateEasyInstall);
        }

    }
}
