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

namespace Microsoft.CookiecutterTools {
    [Guid("BDB4E0B1-4869-4A6F-AD55-5230B768261D")]
    public class CookiecutterOptionPage : DialogPage {
        private bool _showHelp = true;
        private bool _checkForTemplateUpdate = true;
        private string _feedUrl = UrlConstants.DefaultRecommendedFeed;

        public CookiecutterOptionPage() {
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsShowHelpName)]
        [SRDescription(SR.SettingsShowHelpDescription)]
        [DefaultValue(true)]
        public bool ShowHelp {
            get { return _showHelp; }
            set { _showHelp = value; }
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsFeedUrlName)]
        [SRDescription(SR.SettingsFeedUrlDescription)]
        [DefaultValue(UrlConstants.DefaultRecommendedFeed)]
        public string FeedUrl {
            get { return _feedUrl; }
            set { _feedUrl = value; }
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsCheckForTemplateUpdateName)]
        [SRDescription(SR.SettingsCheckForTemplateUpdateDescription)]
        [DefaultValue(true)]
        public bool CheckForTemplateUpdate {
            get { return _checkForTemplateUpdate; }
            set { _checkForTemplateUpdate = value; }
        }
    }
}
