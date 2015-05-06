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
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Uap.Project {
    [Guid(GuidList.guidUapPropertyPageString)]
    class PythonUapPropertyPage : CommonPropertyPage {
        private readonly PythonUapPropertyPageControl _control;

        public const string RemoteDeviceSetting = "RemoteDebugMachine";
        public const string RemotePortSetting = "RemoteDebugPort";
        public const string UapUserSetting = "UserSettingsChanged";
        public const decimal DefaultPort = 5678;

        public PythonUapPropertyPage() {
            _control = new PythonUapPropertyPageControl(this);
        }
        
        public override Control Control {
            get { return _control; }
        }

        public override void Apply() {
            SetConfigUserProjectProperty(RemoteDeviceSetting, _control.RemoteDevice);
            SetConfigUserProjectProperty(RemotePortSetting, _control.RemotePort.ToString());

            // Workaround to reload user project file
            SetProjectProperty(UapUserSetting, DateTime.UtcNow.ToString());
            SetProjectProperty(UapUserSetting, string.Empty);

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
            get { return Resources.UapPropertyPageTitle; }
        }
    }
}
