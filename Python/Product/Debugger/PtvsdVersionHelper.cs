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
using System.Diagnostics;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudioTools;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Debugger {
    internal class PtvsdVersionArguments {
    }

    internal class PtvsdVersionResponse : ResponseBody {
        [JsonProperty("ptvsd")]
        public PtvsdDebuggerVersion Debugger { get; set; }

        [JsonProperty("python")]
        public PythonVersionInfo Python { get; set; }

        [JsonProperty("platform")]
        public PtvsdPlatfromInfo Platfrom { get; set; }

        [JsonProperty("process")]
        public PtvsdProcessInfo Process { get; set; }

    }

    internal class PtvsdVersionRequest : DebugRequestWithResponse<PtvsdVersionArguments, PtvsdVersionResponse> {
        public PtvsdVersionRequest(): base("ptvsd_systemInfo") {
        }
    }

    internal class PtvsdVersionHelper {
        public static void VerifyPtvsdVersion(PtvsdVersionArguments args, PtvsdVersionResponse response) {
            if (PackageVersion.TryParse(response.Debugger.Version, out PackageVersion runningVersion)) {
                var bundledPtvsdVersion = PackageVersion.Parse(PtvsdVersion.Version);
                if (runningVersion.CompareTo(bundledPtvsdVersion) < 0) {
                    ShowPtvsdMessage(
                        Strings.InstalledPtvsdOutdatedTitle,
                        Strings.InstalledPtvsdOutdatedMessage.FormatUI(response.Debugger.Version, PtvsdVersion.Version),
                        allowDisable: false,
                        isError: false
                    );
                }
            }
        }

        public static void VerifyPtvsdVersionError(PtvsdVersionArguments args, ProtocolException ex) {
            ShowPtvsdMessage(
                Strings.InstalledPtvsdOutdatedTitle,
                Strings.InstalledPtvsdOutdatedMessage.FormatUI("unknown", PtvsdVersion.Version),
                allowDisable: false,
                isError: false
            );
        }

        public static void VerifyPtvsdVersionLegacy() {
            ShowPtvsdMessage(
                Strings.InstalledPtvsdOutdatedTitle,
                Strings.InstalledPtvsdOutdatedMessage.FormatUI("3.*", PtvsdVersion.Version),
                allowDisable: false,
                isError: false
            );
        }

        public static void ShowPtvsdIncompatibleEnvError() {
            ShowPtvsdMessage(
                Strings.PtvsdIncompatibleEnvTitle,
                Strings.PtvsdIncompatibleEnvMessage,
                allowDisable: true,
                isError: true
            );
        }

        public static void ShowPtvsdModuleNotFoundError() {
            ShowPtvsdMessage(
                Strings.ImportPtvsdModuleNotFoundTitle,
                Strings.ImportPtvsdModuleNotFoundMessage,
                allowDisable: false,
                isError: true
            );
        }

        private static void ShowPtvsdMessage(string main, string content, bool allowDisable, bool isError) {
            var serviceProvider = VisualStudio.Shell.ServiceProvider.GlobalProvider;
            try {
                serviceProvider.GetUIThread().Invoke(() => {
                    var dlg = new TaskDialog(serviceProvider) {
                        Title = Strings.ProductTitle,
                        MainInstruction = main,
                        Content = content,
                        AllowCancellation = true,
                        MainIcon = isError ? TaskDialogIcon.Error : TaskDialogIcon.Warning,
                    };

                    var disable = new TaskDialogButton(Strings.PtvsdDisableCaption, Strings.PtvsdDisableSubtext);
                    var learnMore = new TaskDialogButton(Strings.PtvsdLearnMoreCaption, Strings.PtvsdLearnMoreSubtext);

                    dlg.Buttons.Add(TaskDialogButton.OK);
                    dlg.Buttons.Insert(0, learnMore);
                    if (allowDisable) {
                        dlg.Buttons.Insert(0, disable);
                    }

                    var selection = dlg.ShowModal();
                    if (selection == learnMore) {
                        Process.Start("https://aka.ms/upgradeptvsd")?.Dispose();
                    } else if (selection == disable) {
                        ExperimentalOptions.UseVsCodeDebugger = false;
                    }
                });
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(serviceProvider, typeof(PtvsdVersionHelper));
            }
        }
    }

    internal class PtvsdDebuggerVersion {
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    internal class PythonVersionInfo {
        /// <summary>
        /// Version can be a string such as '4.0.0' or a collection
        /// of objects [major, minor, micro, releaseLevel, serial]
        /// </summary>
        [JsonProperty("version")]
        public object Version { get; set; }
        [JsonProperty("implementation")]
        public PythonImplementationInfo Implementation { get; set; }
    }

    internal class PtvsdPlatfromInfo {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    internal class PythonImplementationInfo {
        /// <summary>
        /// Version can be a string such as '4.0.0' or a collection
        /// of objects [major, minor, micro, releaseLevel, serial]
        /// </summary>
        [JsonProperty("version")]
        public object Version { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
    }

    internal class PtvsdProcessInfo {
        [JsonProperty("pid")]
        public int ProcessId { get; set; }
        [JsonProperty("bitness")]
        public int Bitness { get; set; }
        [JsonProperty("executable")]
        public string Executable { get; set; }
    }
}
