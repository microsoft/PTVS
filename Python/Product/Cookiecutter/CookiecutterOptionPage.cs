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

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.CookiecutterTools {
    [Guid("BDB4E0B1-4869-4A6F-AD55-5230B768261D")]
    public class CookiecutterOptionPage : DialogPage {
        private bool _showHelp = CookiecutterSettings.DefaultShowHelp;
        private bool _checkForTemplateUpdate = CookiecutterSettings.DefaultCheckForTemplateUpdate;
        private string _feedUrl = CookiecutterSettings.DefaultFeedUrl;

        public CookiecutterOptionPage() {
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsShowHelpName)]
        [SRDescription(SR.SettingsShowHelpDescription)]
        [DefaultValue(CookiecutterSettings.DefaultShowHelp)]
#if DEV18
        [UnifiedSettingsMoniker(CookiecutterSettings.ShowHelpMoniker)]
#endif
        public bool ShowHelp {
            get { return _showHelp; }
            set { _showHelp = value; }
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsFeedUrlName)]
        [SRDescription(SR.SettingsFeedUrlDescription)]
        [DefaultValue(CookiecutterSettings.DefaultFeedUrl)]
#if DEV18
        [UnifiedSettingsMoniker(CookiecutterSettings.FeedUrlMoniker)]
#endif
        public string FeedUrl {
            get { return _feedUrl; }
            set { _feedUrl = value; }
        }

        [SRCategory(SR.SettingsGeneralCategory)]
        [SRDisplayName(SR.SettingsCheckForTemplateUpdateName)]
        [SRDescription(SR.SettingsCheckForTemplateUpdateDescription)]
        [DefaultValue(CookiecutterSettings.DefaultCheckForTemplateUpdate)]
#if DEV18
        [UnifiedSettingsMoniker(CookiecutterSettings.CheckForTemplateUpdateMoniker)]
#endif
        public bool CheckForTemplateUpdate {
            get { return _checkForTemplateUpdate; }
            set { _checkForTemplateUpdate = value; }
        }
    }

    internal static class CookiecutterSettings {
        internal const string ManifestPath = @"UnifiedSettings\Cookiecutter.registration.json";
        internal const bool DefaultShowHelp = true;
        internal const string DefaultFeedUrl = UrlConstants.DefaultRecommendedFeed;
        internal const bool DefaultCheckForTemplateUpdate = true;
        internal const string ShowHelpMoniker = "cookiecutter.general.showHelp";
        internal const string FeedUrlMoniker = "cookiecutter.general.feedUrl";
        internal const string CheckForTemplateUpdateMoniker = "cookiecutter.general.checkForTemplateUpdate";
    }
}
