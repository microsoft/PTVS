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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    [ComVisible(true)]
    [Guid(DebugAdapterLauncherCLSIDNoBraces)]
    public sealed class DebugAdapterLauncher : IAdapterLauncher {
        public const string DebugAdapterLauncherCLSIDNoBraces = "C2990BF1-A87B-4459-9478-322482C535D6";
        public const string DebugAdapterLauncherCLSID = "{" + DebugAdapterLauncherCLSIDNoBraces + "}";
        public const string VSCodeDebugEngineId = "{86432F39-ADFD-4C56-AA8F-AF8FCDC66039}";
        public static Guid VSCodeDebugEngine = new Guid(VSCodeDebugEngineId);

        private DebugInfo _debugInfo;

        public DebugAdapterLauncher() { }

        public void Initialize(IDebugAdapterHostContext context) { }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            if (launchInfo.LaunchType == LaunchType.Attach) {
                DebugAttachInfo debugAttachInfo = (DebugAttachInfo)_debugInfo;

                return DebugAdapterRemoteProcess.Attach(debugAttachInfo);
            }

            DebugLaunchInfo debugLaunchInfo = (DebugLaunchInfo)_debugInfo;
            //var ptvsdAdapterDirectory = Path.GetDirectoryName(PythonToolsInstallPath.GetFile(@"C:\Users\Raymon G\Desktop\MainDrive\ptvsd 5.0\ptvsd\adapter\__init__.py"));
            var ptvsdAdapterDirectory = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("Packages\\ptvsd\\adapter\\__init__.py"));

            DebugAdapterProcess targetProcess = new DebugAdapterProcess(targetInterop, $"\"{ptvsdAdapterDirectory}\"");
            return targetProcess.StartProcess(debugLaunchInfo.Python.FirstOrDefault(), debugLaunchInfo.WebPageUrl);
        }

        public void UpdateLaunchOptions(IAdapterLaunchInfo adapterLaunchInfo) {
            if (adapterLaunchInfo.LaunchType == LaunchType.Launch) {
                _debugInfo = GetLaunchDebugInfo(adapterLaunchInfo.LaunchJson);
            } else {
                _debugInfo = GetTcpAttachDebugInfo(adapterLaunchInfo);
            }

            AddDebugOptions(adapterLaunchInfo, _debugInfo);

            adapterLaunchInfo.LaunchJson = _debugInfo.GetJsonString();
        }

        #region Launch
        private static DebugLaunchInfo GetLaunchDebugInfo(string adapterLaunchJson) {
            var adapterLaunchInfoJson = JObject.Parse(adapterLaunchJson);
            adapterLaunchInfoJson = adapterLaunchInfoJson.Value<JObject>("ConfigurationProperties") ?? adapterLaunchInfoJson;//Based on the VS version, the JSON could be nested in ConfigurationProperties

            DebugLaunchInfo debugLaunchInfo = new DebugLaunchInfo() {
                Cwd = adapterLaunchInfoJson.Value<string>("cwd"),
                Console = "externalTerminal",
            };

            SetInterpreterOptions(debugLaunchInfo, adapterLaunchInfoJson);
            SetProgramInfo(debugLaunchInfo, adapterLaunchInfoJson);
            SetEnvVariables(debugLaunchInfo, adapterLaunchInfoJson);
            SetLaunchDebugOptions(debugLaunchInfo, adapterLaunchInfoJson);

            return debugLaunchInfo;

        }
        private static void SetInterpreterOptions(DebugLaunchInfo debugLaunchInfo, JObject adapterLaunchInfoJson) {
            debugLaunchInfo.Python = new List<string>() {
                adapterLaunchInfoJson.Value<string>("exe").Replace("\"", "")
            };

            string interpreterArgs = adapterLaunchInfoJson.Value<string>("interpreterArgs");
            debugLaunchInfo.Python.AddRange(GetParsedCommandLineArguments(interpreterArgs));
        }
        private static void SetProgramInfo(DebugLaunchInfo debugLaunchInfo, JObject adapterLaunchInfoJson) {
            debugLaunchInfo.Program = new List<string> {
                adapterLaunchInfoJson.Value<string>("scriptName")
            };

            string scriptArgs = adapterLaunchInfoJson.Value<string>("scriptArgs");
            debugLaunchInfo.Program.AddRange(GetParsedCommandLineArguments(scriptArgs));
        }
        private static void SetEnvVariables(DebugLaunchInfo debugLaunchInfo, JObject adapterLaunchInfoJson) {
            var env = new Dictionary<string, string>();
            foreach (var envVariable in adapterLaunchInfoJson.Value<JArray>("env")) {
                env[envVariable.Value<string>("name")] = envVariable.Value<string>("value");
            }

            debugLaunchInfo.Env = env.Count == 0 ? null : env;
        }
        private static void SetLaunchDebugOptions(DebugLaunchInfo debugLaunchInfo, JObject adapterLaunchInfoJson) {
            string[] options = SplitDebugOptions(adapterLaunchInfoJson.Value<string>("options"));

            string djangoOption = options.FirstOrDefault(x => x.StartsWith("DJANGO_DEBUG"));
            if (djangoOption != null) {
                string[] parsedOption = djangoOption.Split('=');
                if (parsedOption.Length == 2) {
                    debugLaunchInfo.DebugDjango = parsedOption[1].ToLower().Trim().Equals("true");
                }
            }

            string webPageUrlOption = options.FirstOrDefault(x => x.StartsWith("WEB_BROWSER_URL"));
            if (webPageUrlOption != null) {
                string[] parsedOption = webPageUrlOption.Split('=');
                if (parsedOption.Length == 2) {
                    debugLaunchInfo.WebPageUrl = WebUtility.UrlDecode(parsedOption[1].ToLower().Trim());
                }
            }
        }

        private static string[] SplitDebugOptions(string options) {
            List<string> res = new List<string>();
            int lastStart = 0;
            for (int i = 0; i < options.Length; i++) {
                if (options[i] == ';') {
                    if (i < options.Length - 1 && options[i + 1] != ';') {
                        // valid option boundary
                        res.Add(options.Substring(lastStart, i - lastStart));
                        lastStart = i + 1;
                    } else {
                        i++;
                    }
                }
            }
            if (options.Length - lastStart > 0) {
                res.Add(options.Substring(lastStart, options.Length - lastStart));
            }
            return res.ToArray();
        }

        #endregion

        #region Attach
        public static DebugAttachInfo GetTcpAttachDebugInfo(IAdapterLaunchInfo adapterLaunchInfo) {
            DebugAttachInfo debugAttachInfo = new DebugAttachInfo();

            adapterLaunchInfo.DebugPort.GetPortName(out string adapterHostPortInfo);
            debugAttachInfo.RemoteUri = new Uri(adapterHostPortInfo);

            var uriInfo = new Uri(adapterHostPortInfo);
            debugAttachInfo.Host = uriInfo.Host;
            debugAttachInfo.Port = uriInfo.Port;

            return debugAttachInfo;
        }

        #endregion

        private static void AddDebugOptions(IAdapterLaunchInfo adapterLaunchInfo, DebugInfo launchJson) {
            var debugService = (IPythonDebugOptionsService)Package.GetGlobalService(typeof(IPythonDebugOptionsService));

            // Stop on entry should always be true for VS Debug Adapter Host.
            // If stop on entry is disabled then VS will automatically issue
            // continue when it sees "stopped" event with "reason=entry".
            launchJson.StopOnEntry = true;

            launchJson.PromptBeforeRunningWithBuildError = debugService.PromptBeforeRunningWithBuildError;
            launchJson.RedirectOutput = debugService.TeeStandardOutput;
            launchJson.WaitOnAbnormalExit = debugService.WaitOnAbnormalExit;
            launchJson.WaitOnNormalExit = debugService.WaitOnNormalExit;
            launchJson.BreakOnSystemExitZero = debugService.BreakOnSystemExitZero;
            launchJson.DebugStdLib = debugService.DebugStdLib;
            launchJson.ShowReturnValue = debugService.ShowFunctionReturnValue;
        }

        [DllImport("shell32.dll")]
        static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        private static IEnumerable<string> GetParsedCommandLineArguments(string command) {
            if (String.IsNullOrEmpty(command)) {
                yield break;
            }

            IntPtr argPointer = CommandLineToArgvW(command, out int argumentCount);
            string[] arguments = new string[argumentCount];

            if (argPointer != IntPtr.Zero) {
                try {
                    for (int i = 0; i < arguments.Length; i++) {
                        yield return Marshal.PtrToStringUni(Marshal.ReadIntPtr(argPointer, i * IntPtr.Size));
                    }

                } finally {
                    Marshal.FreeHGlobal(argPointer);
                }
            }
        }

    }
}


