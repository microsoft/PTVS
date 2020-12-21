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
using System.Diagnostics;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Debugger {
    internal class DebugPyVersionArguments {
    }

    internal class DebugPyVersionResponse : ResponseBody {
        [JsonProperty("debugpy")]
        public DebugPyDebuggerVersion Debugger { get; set; }

        [JsonProperty("python")]
        public PythonVersionInfo Python { get; set; }

        [JsonProperty("platform")]
        public DebugPyPlatformInfo Platfrom { get; set; }

        [JsonProperty("process")]
        public DebugPyProcessInfo Process { get; set; }

    }

    internal class DebugPyVersionRequest : DebugRequestWithResponse<DebugPyVersionArguments, DebugPyVersionResponse> {
        public DebugPyVersionRequest() : base("debugpySystemInfo") {
        }
    }

    internal class DebugPyVersionHelper {
        public static void VerifyDebugPyVersion(DebugPyVersionArguments args, DebugPyVersionResponse response) {
            if (PackageVersion.TryParse(response.Debugger.Version, out PackageVersion runningVersion)) {
                var bundledDebugPyVersion = PackageVersion.Parse(DebugPyVersion.Version);
                if (runningVersion.CompareTo(bundledDebugPyVersion) < 0) {
                    ShowDebuggingErrorMessage(
                        Strings.InstalledDebugPyOutdatedTitle,
                        Strings.InstalledDebugPyOutdatedMessage.FormatUI(response.Debugger.Version, DebugPyVersion.Version),
                        allowDisable: false,
                        isError: false
                    );
                }
            }
        }

        public static void ShowDebugPyVersionError(DebugPyVersionArguments args, ProtocolException ex) {
            ShowDebuggingErrorMessage(
                Strings.InstalledDebugPyOutdatedTitle,
                Strings.InstalledDebugPyOutdatedMessage.FormatUI("unknown", DebugPyVersion.Version),
                allowDisable: false,
                isError: false
            );
        }

        public static void ShowLegacyPtvsdVersionError() {
            ShowDebuggingErrorMessage(
                Strings.InstalledDebugPyOutdatedTitle,
                Strings.InstalledDebugPyOutdatedMessage.FormatUI("3.*", DebugPyVersion.Version),
                allowDisable: false,
                isError: false
            );
        }

        public static void ShowDebugPyIncompatibleEnvError() {
            ShowDebuggingErrorMessage(
                Strings.PtvsdIncompatibleEnvTitle,
                Strings.PtvsdIncompatibleEnvMessage,
                allowDisable: true,
                isError: true
            );
        }

        public static void ShowDebugPyModuleNotFoundError() {
            ShowDebuggingErrorMessage(
                Strings.ImportPtvsdModuleNotFoundTitle,
                Strings.ImportPtvsdModuleNotFoundMessage,
                allowDisable: false,
                isError: true
            );
        }

        private static void ShowDebuggingErrorMessage(string main, string content, bool allowDisable, bool isError) {
            var serviceProvider = VisualStudio.Shell.ServiceProvider.GlobalProvider;
            try {
                serviceProvider.GetUIThread().Invoke(() => {
                    var dlg = new TaskDialog(serviceProvider) {
                        Title = Strings.ProductTitle,
                        MainInstruction = main,
                        Content = content,
                        AllowCancellation = true,
                        MainIcon = isError ? TaskDialogIcon.Error : TaskDialogIcon.Warning,
                        EnableHyperlinks = true,
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
                        var debuggerOptions = ((PythonToolsService)Package.GetGlobalService(typeof(PythonToolsService))).DebuggerOptions;
                        debuggerOptions.UseLegacyDebugger = true;
                        debuggerOptions.Save();
                    }
                });
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                ex.ReportUnhandledException(serviceProvider, typeof(DebugPyVersionHelper));
            }
        }
    }

    internal class DebugPyDebuggerVersion {
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

    internal class DebugPyPlatformInfo {
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

    internal class DebugPyProcessInfo {
        [JsonProperty("pid")]
        public int ProcessId { get; set; }
        [JsonProperty("bitness")]
        public int Bitness { get; set; }
        [JsonProperty("executable")]
        public string Executable { get; set; }
    }
}


