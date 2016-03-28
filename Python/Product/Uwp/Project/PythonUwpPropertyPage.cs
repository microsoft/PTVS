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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Uwp.Project {
    [Guid(GuidList.guidUwpPropertyPageString)]
    class PythonUwpPropertyPage : CommonPropertyPage, IDisposable {
        private readonly PythonUwpPropertyPageControl _control;

        public const string RemoteDeviceSetting = "RemoteDebugMachine";
        public const string RemotePortSetting = "RemoteDebugPort";
        public const string UwpUserSetting = "UserSettingsChanged";
        public const decimal DefaultPort = 5678;

        public PythonUwpPropertyPage() {
            _control = new PythonUwpPropertyPageControl(this);
        }

        public void Dispose() {
            Dispose(true);
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                _control.Dispose();
            }
        }
        
        public override Control Control {
            get { return _control; }
        }

        public override void Apply() {
            SetConfigUserProjectProperty(RemoteDeviceSetting, _control.RemoteDevice);
            SetConfigUserProjectProperty(RemotePortSetting, _control.RemotePort.ToString());

            // Workaround to reload user project file
            SetProjectProperty(UwpUserSetting, DateTime.UtcNow.ToString());
            SetProjectProperty(UwpUserSetting, string.Empty);

            IsDirty = false;
        }

        public override void LoadSettings() {
            Loading = true;
            try {
                var portSetting = GetConfigUserProjectProperty(RemotePortSetting);
                decimal portValue = DefaultPort;

                _control.RemoteDevice = GetConfigUserProjectProperty(RemoteDeviceSetting);

                if (!decimal.TryParse(portSetting, out portValue)) {
                    portValue = DefaultPort;
                }

                _control.RemotePort = portValue;

                IsDirty = false;
            } finally {
                Loading = false;
            }
        }

        public override string Name {
            get { return Resources.UwpPropertyPageTitle; }
        }
    }
}
