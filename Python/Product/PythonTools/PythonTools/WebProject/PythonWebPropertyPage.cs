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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "object is owned by VS")]
    [Guid(PythonConstants.WebPropertyPageGuid)]
    class PythonWebPropertyPage : CommonPropertyPage {
        private readonly PythonWebPropertyPageControl _control;

        public const string StaticUriPatternSetting = "StaticUriPattern";
        public const string StaticUriRewriteSetting = "StaticUriRewrite";
        public const string WsgiHandlerSetting = "PythonWsgiHandler";

        public PythonWebPropertyPage() {
            _control = new PythonWebPropertyPageControl(this);
        }

        public override Control Control {
            get { return _control; }
        }

        public override void Apply() {
            SetProjectProperty(StaticUriPatternSetting, _control.StaticUriPattern);
            SetProjectProperty(StaticUriRewriteSetting, _control.StaticUriRewrite);
            SetProjectProperty(WsgiHandlerSetting, _control.WsgiHandler);
            IsDirty = false;
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                _control.StaticUriPattern = GetProjectProperty(StaticUriPatternSetting);
                _control.StaticUriRewrite = GetProjectProperty(StaticUriRewriteSetting);
                _control.WsgiHandler = GetProjectProperty(WsgiHandlerSetting);
                IsDirty = false;
            } finally {
                Loading = false;
            }
        }

        public override string Name {
            get { return Strings.PythonWebPropertyPageTitle; }
        }
    }
}
