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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    public partial class PublishPropertyControl : UserControl {
        private readonly PublishPropertyPage _page;
        public PublishPropertyControl(PublishPropertyPage page) {
            InitializeComponent();

            var publishers = PythonToolsPackage.ComponentModel.GetExtensions<IProjectPublisher>().ToArray();
            string kinds;
            if (publishers.Length == 1) {
                kinds = publishers[0].DestinationDescription;
            } else {
                kinds = FormatPublishers(publishers);
            }

            _publishLocationLabel.Text = "Publishing folder location (" + kinds + "): ";
            _page = page;
        }

        private static string FormatPublishers(IProjectPublisher[] publishers) {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < publishers.Length; i++) {
                res.Append(publishers[i].DestinationDescription);

                if (i == publishers.Length - 2) {
                    res.Append(" or ");
                } else if (i != publishers.Length - 1) {
                    res.Append(", ");
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
