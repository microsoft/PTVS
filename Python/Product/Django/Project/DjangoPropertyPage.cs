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

namespace Microsoft.PythonTools.Django.Project {
    [Guid(GuidList.guidDjangoPropertyPageString)]
    class DjangoPropertyPage : CommonPropertyPage {
        private readonly DjangoPropertyPageControl _control;

        public const string SettingModulesSetting = "DjangoSettingsModule";
        public const string StaticUriPatternSetting = "StaticUriPattern";

        public DjangoPropertyPage() {
            _control = new DjangoPropertyPageControl(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _control.Dispose();
            }
            base.Dispose(disposing);
        }

        public override Control Control {
            get { return _control; }
        }

        public override void Apply() {
            SetProjectProperty(SettingModulesSetting, _control.SettingsModule);
            SetProjectProperty(StaticUriPatternSetting, _control.StaticUriPattern);
            IsDirty = false;
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                _control.SettingsModule = GetProjectProperty(SettingModulesSetting);
                _control.StaticUriPattern = GetProjectProperty(StaticUriPatternSetting);
                IsDirty = false;
            } finally {
                Loading = false;
            }
        }

        public override string Name {
            get { return Resources.DjangoPropertyPageTitle; }
        }
    }
}
