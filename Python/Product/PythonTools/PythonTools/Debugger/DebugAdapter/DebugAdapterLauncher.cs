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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.ManagedSafeAttach;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
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

        private IDebugAdapterHostContext _adapterHostContext;
        private DebugInfo _debugInfo;

        // Fixed port attach state
        private int _connectPort = 0;
        private string _connectHost = "127.0.0.1";
        private bool _managedSuccess = false;

        public DebugAdapterLauncher() { }

        public void Initialize(IDebugAdapterHostContext context) {
            _adapterHostContext = context ?? throw new ArgumentNullException(nameof(context));
            PythonToolsPackage.EnsureLoaded();
        }

        public ITargetHostProcess LaunchAdapter(IAdapterLaunchInfo launchInfo, ITargetHostInterop targetInterop) {
            if (_debugInfo is DebugTcpAttachInfo tcpAttach) {
                return DebugAdapterRemoteProcess.Attach(tcpAttach);
            }

            string pythonExe, webPageUrl;
            if (_debugInfo is DebugLaunchInfo launch) {
                pythonExe = launch.InterpreterPathAndArguments.FirstOrDefault();
                webPageUrl = launch.LaunchWebPageUrl;
            } else if (_debugInfo is DebugLocalAttachInfo) {
                var interp = ((PythonToolsService)Package.GetGlobalService(typeof(PythonToolsService))).InterpreterOptionsService.DefaultInterpreter;
                if (interp == null) throw new Exception(Strings.NoInterpretersAvailable);
                interp.ThrowIfNotRunnable();
                pythonExe = interp.Configuration.InterpreterPath;
                webPageUrl = null;
            } else {
                throw new ArgumentException(nameof(launchInfo));
            }

            var debugPyAdapterDirectory = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("debugpy\\adapter\\__init__.py"));
            var targetProcess = new DebugAdapterProcess(_adapterHostContext, targetInterop, debugPyAdapterDirectory);
            return targetProcess.StartProcess(pythonExe, webPageUrl);
        }

        public void UpdateLaunchOptions(IAdapterLaunchInfo adapterLaunchInfo) {
            if (adapterLaunchInfo.LaunchType == LaunchType.Launch) {
                _debugInfo = GetLaunchDebugInfo(adapterLaunchInfo.LaunchJson);
            } else if (adapterLaunchInfo.DebugPort is PythonRemoteDebugPort) {
                _debugInfo = GetTcpAttachDebugInfo(adapterLaunchInfo);
            } else { // local attach
                _debugInfo = GetLocalAttachDebugInfo(adapterLaunchInfo);
                PrepareFixedPortAttach();
                TryManagedSafeAttachForLocalProcess(_debugInfo as DebugLocalAttachInfo);
                PromoteToTcpAttachFixed();
            }

            AddDebuggerOptions(adapterLaunchInfo, _debugInfo);
            adapterLaunchInfo.LaunchJson = _debugInfo.GetJsonString();
        }

        private void PrepareFixedPortAttach() {
            try {
                // Allocate a free TCP port (listener will be inside target process via wrapper + loader)
                var l = new TcpListener(IPAddress.Loopback, 0);
                try {
                    l.Start();
                    _connectPort = ((IPEndPoint)l.LocalEndpoint).Port;
                } finally { try { l.Stop(); } catch { } }
                _connectHost = Environment.GetEnvironmentVariable("PTVS_DEBUG_HOST");
                if (string.IsNullOrEmpty(_connectHost)) _connectHost = "127.0.0.1";
                Environment.SetEnvironmentVariable("PTVS_DEBUG_PORT", _connectPort.ToString());
                // Unified pause flag; preserve legacy envs if already set
                if (Environment.GetEnvironmentVariable("PTVS_DEBUG_BREAK") == null && Environment.GetEnvironmentVariable("PTVS_DEBUG_PAUSE") == null) {
                    // Break Immediately default for Attach UI scenario
                    Environment.SetEnvironmentVariable("PTVS_DEBUG_PAUSE", "1");
                }
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_LOADER_VERBOSE")))
                    Environment.SetEnvironmentVariable("PTVS_SAFE_ATTACH_LOADER_VERBOSE", "1");
                Debug.WriteLine($"[PTVS][ManagedSafeAttach][DA] Fixed port prepared host={_connectHost} port={_connectPort}");
            } catch (Exception ex) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach][DA] PrepareFixedPortAttach failed: " + ex.Message);
                _connectPort = 0;
            }
        }

        private void PromoteToTcpAttachFixed() {
            if (!_managedSuccess) return;
            if (!(_debugInfo is DebugLocalAttachInfo)) return;
            if (_connectPort <= 0) {
                Debug.WriteLine("[PTVS][ManagedSafeAttach][DA] Cannot promote to TCP attach; port unavailable");
                return;
            }
            _debugInfo = new DebugTcpAttachInfo {
                Host = _connectHost,
                Port = _connectPort,
                Env = _debugInfo.Env
            };
            Debug.WriteLine($"[PTVS][ManagedSafeAttach][DA] Promoted to DebugTcpAttachInfo host={_connectHost} port={_connectPort}");
        }

        #region Launch
        private static DebugLaunchInfo GetLaunchDebugInfo(string adapterLaunchJson) {
            var adapterLaunchInfoJson = JObject.Parse(adapterLaunchJson);
            adapterLaunchInfoJson = adapterLaunchInfoJson.Value<JObject>("ConfigurationProperties") ?? adapterLaunchInfoJson;
            var debugLaunchInfo = new DebugLaunchInfo() {
                CurrentWorkingDirectory = adapterLaunchInfoJson.Value<string>("cwd"),
                Console = "externalTerminal",
            };
            SetInterpreterPathAndArguments(debugLaunchInfo, adapterLaunchInfoJson);
            SetScriptPathAndArguments(debugLaunchInfo, adapterLaunchInfoJson);
            SetEnvVariables(debugLaunchInfo, adapterLaunchInfoJson);
            SetLaunchDebugOptions(debugLaunchInfo, adapterLaunchInfoJson);
            return debugLaunchInfo;
        }

        private static void SetInterpreterPathAndArguments(DebugLaunchInfo info, JObject json) {
            info.InterpreterPathAndArguments = new List<string>() { json.Value<string>("exe").Replace("\"", "") };
            string interpreterArgs = json.Value<string>("interpreterArgs");
            try { info.InterpreterPathAndArguments.AddRange(GetParsedCommandLineArguments(interpreterArgs)); }
            catch { MessageBox.Show(Strings.UnableToParseInterpreterArgs.FormatUI(interpreterArgs), Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private static void SetScriptPathAndArguments(DebugLaunchInfo info, JObject json) {
            info.Script = json.Value<string>("scriptName");
            info.ScriptArguments = new List<string>();
            string scriptArgs = json.Value<string>("scriptArgs");
            try { info.ScriptArguments.AddRange(GetParsedCommandLineArguments(scriptArgs)); }
            catch { MessageBox.Show(Strings.UnableToParseScriptArgs.FormatUI(scriptArgs), Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private static void SetEnvVariables(DebugLaunchInfo info, JObject json) {
            var env = new Dictionary<string, string>();
            foreach (var envVariable in json.Value<JArray>("env")) {
                env[envVariable.Value<string>("name")] = envVariable.Value<string>("value");
            }
            info.Env = env.Count == 0 ? null : env;
        }

        private static void SetLaunchDebugOptions(DebugLaunchInfo info, JObject json) {
            string[] options = SplitDebugOptions(json.Value<string>("options"));
            var djangoOption = options.FirstOrDefault(x => x.StartsWith("DJANGO_DEBUG"));
            if (djangoOption != null) {
                var parsed = djangoOption.Split('=');
                if (parsed.Length == 2) info.DebugDjango = parsed[1].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            var webPageUrlOption = options.FirstOrDefault(x => x.StartsWith("WEB_BROWSER_URL"));
            if (webPageUrlOption != null) {
                var parsed = webPageUrlOption.Split('=');
                if (parsed.Length == 2) info.LaunchWebPageUrl = HttpUtility.UrlDecode(parsed[1]);
            }
        }

        private static string[] SplitDebugOptions(string options) {
            if (string.IsNullOrEmpty(options)) return Array.Empty<string>();
            var res = new List<string>(); int lastStart = 0;
            for (int i = 0; i < options.Length; i++) {
                if (options[i] == ';') {
                    if (i < options.Length - 1 && options[i + 1] != ';') { res.Add(options.Substring(lastStart, i - lastStart)); lastStart = i + 1; }
                    else i++; // skip escaped ;
                }
            }
            if (options.Length - lastStart > 0) res.Add(options.Substring(lastStart));
            return res.ToArray();
        }
        #endregion

        #region Attach
        private static DebugTcpAttachInfo GetTcpAttachDebugInfo(IAdapterLaunchInfo adapterLaunchInfo) {
            var info = new DebugTcpAttachInfo();
            adapterLaunchInfo.DebugPort.GetPortName(out var adapterHostPortInfo);
            info.RemoteUri = new Uri(adapterHostPortInfo);
            var uri = new Uri(adapterHostPortInfo);
            info.Host = uri.Host; info.Port = uri.Port; return info;
        }
        private static DebugLocalAttachInfo GetLocalAttachDebugInfo(IAdapterLaunchInfo adapterLaunchInfo) => new DebugLocalAttachInfo { ProcessId = adapterLaunchInfo.AttachProcessId };
        #endregion

        private static void AddDebuggerOptions(IAdapterLaunchInfo adapterLaunchInfo, DebugInfo launchJson) {
            var debugService = (IPythonDebugOptionsService)Package.GetGlobalService(typeof(IPythonDebugOptionsService));
            launchJson.StopOnEntry = true;
            launchJson.SubProcess = false;
            launchJson.PromptBeforeRunningWithBuildError = debugService.PromptBeforeRunningWithBuildError;
            launchJson.RedirectOutput = debugService.TeeStandardOutput;
            launchJson.WaitOnAbnormalExit = debugService.WaitOnAbnormalExit;
            launchJson.WaitOnNormalExit = debugService.WaitOnNormalExit;
            launchJson.BreakOnSystemExitZero = debugService.BreakOnSystemExitZero;
            launchJson.DebugStdLib = debugService.DebugStdLib;
            launchJson.ShowReturnValue = debugService.ShowFunctionReturnValue;
            var variablePresentation = new VariablePresentation {
                Class = debugService.VariablePresentationForClasses,
                Function = debugService.VariablePresentationForFunctions,
                Protected = debugService.VariablePresentationForProtected,
                Special = debugService.VariablePresentationForSpecial
            };
            launchJson.VariablePresentation = variablePresentation;
            launchJson.Rules = new List<PathRule> { new PathRule { Path = PathUtils.GetParent(typeof(DebugAdapterLauncher).Assembly.Location), Include = false } };
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
        private static IEnumerable<string> GetParsedCommandLineArguments(string command) {
            if (string.IsNullOrEmpty(command)) yield break;
            IntPtr argPtr = CommandLineToArgvW(command, out var count);
            if (argPtr == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            try { for (int i = 0; i < count; i++) yield return Marshal.PtrToStringUni(Marshal.ReadIntPtr(argPtr, i * IntPtr.Size)); }
            finally { Marshal.FreeHGlobal(argPtr); }
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr hObject);
        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;

        private void TryManagedSafeAttachForLocalProcess(DebugLocalAttachInfo localInfo) {
            try {
                if (localInfo == null) return;
                if (Environment.GetEnvironmentVariable("PTVS_SAFE_ATTACH_MANAGED_DISABLE") == "1") { Debug.WriteLine("[PTVS][ManagedSafeAttach][DA] Disabled via env var – skipping orchestrator."); return; }
                int pid = (int)localInfo.ProcessId;
                IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
                if (hProcess == IntPtr.Zero) { Debug.WriteLine($"[PTVS][ManagedSafeAttach][DA] OpenProcess failed pid={pid} (GLE={Marshal.GetLastWin32Error()})"); return; }
                try {
                    var res = SafeAttachOrchestrator.TryManagedSafeAttach(hProcess, pid);
                    if (res.Success) {
                        _managedSuccess = true;
                        Debug.WriteLine($"[PTVS][ManagedSafeAttach][DA] Safe attach success pid={pid} v={res.MajorVersion}.{res.MinorVersion}");
                        if (_debugInfo.Env == null) _debugInfo.Env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _debugInfo.Env["PTVS_SAFE_ATTACH_MANAGED_DONE"] = "1";
                        _debugInfo.Env["PTVS_SAFE_ATTACH_VERSION"] = $"{res.MajorVersion}.{res.MinorVersion}";
                    } else {
                        _managedSuccess = false;
                        Debug.WriteLine($"[PTVS][ManagedSafeAttach][DA] Safe attach failed pid={pid} site={res.FailureSite}");
                    }
                } finally { CloseHandle(hProcess); }
            } catch (Exception ex) { _managedSuccess = false; Debug.WriteLine("[PTVS][ManagedSafeAttach][DA] Exception attempting safe attach: " + ex.Message); }
        }
    }
}