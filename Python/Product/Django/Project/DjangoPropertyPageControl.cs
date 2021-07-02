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

namespace Microsoft.PythonTools.Django.Project
{
    public partial class DjangoPropertyPageControl : UserControl
    {
        private readonly DjangoPropertyPage _properties;

        private DjangoPropertyPageControl()
        {
            InitializeComponent();

            _toolTip.SetToolTip(_settingsModule, Resources.DjangoSettingsModuleHelp);
            _toolTip.SetToolTip(_settingsModuleLabel, Resources.DjangoSettingsModuleHelp);

            _toolTip.SetToolTip(_staticUri, Resources.StaticUriHelp);
            _toolTip.SetToolTip(_staticUriLabel, Resources.StaticUriHelp);
        }

        internal DjangoPropertyPageControl(DjangoPropertyPage properties)
            : this()
        {
            _properties = properties;
        }

        public string SettingsModule
        {
            get { return _settingsModule.Text; }
            set { _settingsModule.Text = value; }
        }

        public string StaticUriPattern
        {
            get { return _staticUri.Text; }
            set { _staticUri.Text = value; }
        }

        private void Setting_TextChanged(object sender, EventArgs e)
        {
            if (_properties != null)
            {
                _properties.IsDirty = true;
            }
        }
    }
}
