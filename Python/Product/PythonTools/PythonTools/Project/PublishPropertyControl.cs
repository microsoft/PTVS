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

using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    public partial class PublishPropertyControl : UserControl {
        private readonly PublishPropertyPage _page;
        public PublishPropertyControl(PublishPropertyPage page) {
            InitializeComponent();

            _page = page;
        }

        internal void LoadSettings() {
            PublishUrl = _page.Project.GetProjectProperty(CommonConstants.PublishUrl);
            var publishers = _page.Project.Site.GetComponentModel().GetExtensions<IProjectPublisher>().ToArray();
            string kinds;
            if (publishers.Length == 1) {
                kinds = publishers[0].DestinationDescription;
            } else {
                kinds = FormatPublishers(publishers);
            }

            _publishLocationLabel.Text = Strings.PublishPropertyControl_LocationLabel.FormatUI(kinds);
        }

        private static string FormatPublishers(IProjectPublisher[] publishers) {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < publishers.Length; i++) {
                res.Append(publishers[i].DestinationDescription);

                if (i == publishers.Length - 2) {
                    res.Append(Strings.PublishPropertyControl_LocationTypeSeparatorLast);
                } else if (i != publishers.Length - 1) {
                    res.Append(Strings.PublishPropertyControl_LocationTypeSeparator);
                }
            }
            return res.ToString();
        }

        public string PublishUrl {
            get {
                return _pubUrl.Text;
            }
            set {
                // don't deliver events when just updating the value internally
                _pubUrl.TextChanged -= _pubUrl_TextChanged;
                _pubUrl.Text = value;
                _pubUrl.TextChanged += _pubUrl_TextChanged;
            }
        }

        private void _pubNowButton_Click(object sender, EventArgs e) {
            _page.Project.Publish(PublishProjectOptions.Default, true);
        }

        private void _pubUrl_TextChanged(object sender, EventArgs e) {
            _page.IsDirty = true;
        }
    }
}
