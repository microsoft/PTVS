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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.CookiecutterTools {
    [Guid("BDB4E0B1-4869-4A6F-AD55-5230B768261D")]
    public class CookiecutterOptionPage : DialogPage {
        private bool _showHelp = true;
        private string _feedUrl = UrlConstants.DefaultRecommendedFeed;

        public CookiecutterOptionPage() {
        }

        [Category("General")]
        [DisplayName("Show Help")]
        [Description("Show the help information bar.")]
        public bool ShowHelp {
            get { return _showHelp; }
            set { _showHelp = value; }
        }

        [Category("General")]
        [DisplayName("Recommended Feed URL")]
        [Description("Location of the feed. The contents of the feed consists of line separated URLs of template locations.")]
        public string FeedUrl {
            get { return _feedUrl; }
            set { _feedUrl = value; }
        }
    }
}
