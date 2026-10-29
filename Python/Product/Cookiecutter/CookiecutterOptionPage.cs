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
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.CookiecutterTools {
    [Guid("BDB4E0B1-4869-4A6F-AD55-5230B768261D")]
    public class CookiecutterOptionPage : DialogPage {
        private bool? _showHelp;
        private bool? _checkForTemplateUpdate;
        private string _feedUrl;
        private bool _feedUrlChanged;

        private CookiecutterSettings Settings => CookiecutterPackage.Instance.Settings;

        public CookiecutterOptionPage() {
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsShowHelpName)]
        [SRDescription(SR.SettingsShowHelpDescription)]
        [DefaultValue(true)]
        public bool ShowHelp {
            get => _showHelp ?? Settings.ShowHelp;
            set => _showHelp = value;
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsFeedUrlName)]
        [SRDescription(SR.SettingsFeedUrlDescription)]
        [DefaultValue(UrlConstants.DefaultRecommendedFeed)]
        public string FeedUrl {
            get => _feedUrlChanged ? _feedUrl : Settings.FeedUrl;
            set {
                _feedUrl = value;
                _feedUrlChanged = true;
            }
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsCheckForTemplateUpdateName)]
        [SRDescription(SR.SettingsCheckForTemplateUpdateDescription)]
        [DefaultValue(true)]
        public bool CheckForTemplateUpdate {
            get => _checkForTemplateUpdate ?? Settings.CheckForTemplateUpdate;
            set => _checkForTemplateUpdate = value;
        }

        public override void LoadSettingsFromStorage() {
            _showHelp = null;
            _checkForTemplateUpdate = null;
            _feedUrl = null;
            _feedUrlChanged = false;
        }

        public override void SaveSettingsToStorage() {
            if (_showHelp.HasValue) {
                Settings.ShowHelp = _showHelp.Value;
                _showHelp = null;
            }
            if (_feedUrlChanged) {
                Settings.FeedUrl = _feedUrl;
                _feedUrlChanged = false;
            }
            if (_checkForTemplateUpdate.HasValue) {
                Settings.CheckForTemplateUpdate = _checkForTemplateUpdate.Value;
                _checkForTemplateUpdate = null;
            }
        }

        public override void ResetSettings() {
            ShowHelp = true;
            FeedUrl = UrlConstants.DefaultRecommendedFeed;
            CheckForTemplateUpdate = true;
        }

        protected override void OnClosed(EventArgs e) {
            LoadSettingsFromStorage();
            base.OnClosed(e);
        }
    }
}
