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

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;

namespace Microsoft.PythonTools.LanguageServerClient {
    class NodeEnvironmentProvider {
        private readonly IServiceProvider _site;
        private readonly JoinableTaskContext _joinableTaskContext;

        public NodeEnvironmentProvider(IServiceProvider site, JoinableTaskContext joinableTaskContext) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        public async Task<string> GetNodeExecutablePath() {
            string filePath;

            // Python workload will have a dependency on this, so it should
            // always be found. If you are a PTVS dev without the Python
            // workload installed, then you may not have the component installed.
            filePath = await GetNodePathFromSharedComponent();
            if (File.Exists(filePath)) {
                return filePath;
            }

#if DEBUG
            // For development convenience, allow use of global node.js install
            if (Environment.Is64BitOperatingSystem) {
                filePath = GetNodePathFromRegistry(RegistryView.Registry64);
                if (File.Exists(filePath)) {
                    return filePath;
                }
            }

            filePath = GetNodePathFromRegistry(RegistryView.Registry32);
            if (File.Exists(filePath)) {
                return filePath;
            }
#endif

            return null;
        }

        private async Task<string> GetNodePathFromSharedComponent() {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            // The Node.js installed by component id Microsoft.VisualStudio.Package.NodeJs
            var shell = _site.GetService<SVsShell, IVsShell>();
            shell.GetProperty((int)__VSSPROPID2.VSSPROPID_InstallRootDir, out object installDir);

            var folderPath = Path.Combine((string)installDir, @"MSBuild\Microsoft\VisualStudio\NodeJs");
            if (Directory.Exists(folderPath)) {
                var filePath = Environment.Is64BitOperatingSystem
                    ? Path.Combine(folderPath, "win-x64", "node.exe")
                    : Path.Combine(folderPath, "node.exe");

                if (File.Exists(filePath)) {
                    return filePath;
                }
            }

            return null;
        }

        private static string GetNodePathFromRegistry(RegistryView view) {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
            using (var key = root.OpenSubKey("Software\\Node.js")) {
                var installPath = key.GetValue("InstallPath", null) as string;
                if (Directory.Exists(installPath)) {
                    var filePath = Path.Combine(installPath, "node.exe");
                    if (File.Exists(filePath)) {
                        return filePath;
                    }
                }
            }

            return null;
        }
    }
}
