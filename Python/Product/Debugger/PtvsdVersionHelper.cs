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

using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Debugger {
    internal class PtvsdVersionArguments {
    }

    internal class PtvsdVersionResponse : ResponseBody {
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    internal class PtvsdVersionRequest : DebugRequestWithResponse<PtvsdVersionArguments, PtvsdVersionResponse> {
        public PtvsdVersionRequest(): base("ptvsd_version") {
        }
    }

    internal class PtvsdVersionHelper {
        public static void VerifyPtvsdVersion(PtvsdVersionArguments args, PtvsdVersionResponse response) {
            if (PackageVersion.TryParse(response.Version, out PackageVersion runningVersion)) {
                var bundledPtvsdVersion = PackageVersion.Parse(PtvsdVersion.Version);
                if (runningVersion.CompareTo(bundledPtvsdVersion) < 0) {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        MessageBox.Show(
                            new Win32Window(Process.GetCurrentProcess().MainWindowHandle),
                            Strings.InstalledPtvsdOutdatedMessage.FormatUI(response.Version, PtvsdVersion.Version),
                            Strings.ProductTitle,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    });
                }
            }
        }
    }
}
