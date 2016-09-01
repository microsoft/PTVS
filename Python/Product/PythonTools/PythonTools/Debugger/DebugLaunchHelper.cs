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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Debugger {
    public static class DebugLaunchHelper {
        private static readonly Regex SubstitutionPattern = new Regex(@"\%([\w_]+)\%");

        private static IEnumerable<string> GetGlobalDebuggerOptions(
            PythonToolsService pyService,
            bool allowPauseAtEnd = true,
            bool alwaysPauseAtEnd = false
        ) {
            var options = pyService.DebuggerOptions;

            if (alwaysPauseAtEnd || allowPauseAtEnd && options.WaitOnAbnormalExit) {
                yield return AD7Engine.WaitOnAbnormalExitSetting + "=True";
            }
            if (alwaysPauseAtEnd || allowPauseAtEnd && options.WaitOnNormalExit) {
                yield return AD7Engine.WaitOnNormalExitSetting + "=True";
            }
            if (options.TeeStandardOutput) {
                yield return AD7Engine.RedirectOutputSetting + "=True";
            }
            if (options.BreakOnSystemExitZero) {
                yield return AD7Engine.BreakSystemExitZero + "=True";
            }
            if (options.DebugStdLib) {
                yield return AD7Engine.DebugStdLib + "=True";
            }
        }

        private static IEnumerable<string> GetLaunchConfigurationOptions(LaunchConfiguration config) {
            if (config.Interpreter != null) {
                yield return string.Format("{0}={1}", AD7Engine.VersionSetting, config.Interpreter.Version);
            }
            yield return string.Format("{0}={1}",
                AD7Engine.InterpreterOptions,
                config.InterpreterArguments ?? string.Empty
            );
            var url = config.GetLaunchOption(PythonConstants.WebBrowserUrlSetting);
            if (!string.IsNullOrWhiteSpace(url)) {
                yield return string.Format("{0}={1}", AD7Engine.WebBrowserUrl, HttpUtility.UrlEncode(url));
            }

            if (config.GetLaunchOption("DjangoDebug").IsTrue()) {
                yield return AD7Engine.EnableDjangoDebugging + "=True";
            }
        }

        private static string DoSubstitutions(IDictionary<string, string> environment, string str) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            return SubstitutionPattern.Replace(
                str,
                m => {
                    string value;
                    return environment.TryGetValue(m.Groups[1].Value, out value) ? value : "";
                }
            );
        }

        public static unsafe DebugTargetInfo CreateDebugTargetInfo(IServiceProvider provider, LaunchConfiguration config) {
            var pyService = provider.GetPythonToolsService();
            var dti = new DebugTargetInfo(provider);

            try {
                dti.Info.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
                dti.Info.bstrExe = config.GetInterpreterPath();
                dti.Info.bstrCurDir = config.WorkingDirectory;
                if (string.IsNullOrEmpty(dti.Info.bstrCurDir)) {
                    dti.Info.bstrCurDir = PathUtils.GetParent(config.ScriptName);
                }

                dti.Info.bstrRemoteMachine = null;
                dti.Info.fSendStdoutToOutputWindow = 0;

                bool nativeDebug = config.GetLaunchOption(PythonConstants.EnableNativeCodeDebugging).IsTrue();
                if (!nativeDebug) {
                    dti.Info.bstrOptions = string.Join(";",
                        GetGlobalDebuggerOptions(pyService)
                            .Concat(GetLaunchConfigurationOptions(config))
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Select(s => s.Replace(";", ";;"))
                    );
                }

                // Environment variables should be passed as a 
                // null-terminated block of null-terminated strings. 
                // Each string is in the following form:name=value\0
                var buf = new StringBuilder();
                foreach (var kv in provider.GetPythonToolsService().GetFullEnvironment(config)) {
                    buf.AppendFormat("{0}={1}\0", kv.Key, kv.Value);
                }
                if (buf.Length > 0) {
                    buf.Append("\0");
                    dti.Info.bstrEnv = buf.ToString();
                }

                var args = string.Join(" ", new[] {
                    config.InterpreterArguments,
                    config.ScriptName == null ? "" : ProcessOutput.QuoteSingleArgument(config.ScriptName),
                    config.ScriptArguments
                }.Where(s => !string.IsNullOrEmpty(s)));

                if (config.Environment != null) {
                    args = DoSubstitutions(config.Environment, args);
                }
                dti.Info.bstrArg = args;

                if (nativeDebug) {
                    dti.Info.dwClsidCount = 2;
                    dti.Info.pClsidList = Marshal.AllocCoTaskMem(sizeof(Guid) * 2);
                    var engineGuids = (Guid*)dti.Info.pClsidList;
                    engineGuids[0] = dti.Info.clsidCustom = DkmEngineId.NativeEng;
                    engineGuids[1] = AD7Engine.DebugEngineGuid;
                } else {
                    // Set the Python debugger
                    dti.Info.clsidCustom = new Guid(AD7Engine.DebugEngineId);
                    dti.Info.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;
                }

                // Null out dti so that it is not disposed before we return.
                var result = dti;
                dti = null;
                return result;
            } finally {
                if (dti != null) {
                    dti.Dispose();
                }
            }
        }

        public static ProcessStartInfo CreateProcessStartInfo(IServiceProvider provider, LaunchConfiguration config) {
            var psi = new ProcessStartInfo {
                FileName = config.GetInterpreterPath(),
                Arguments = string.Join(" ", new[] {
                    config.InterpreterArguments,
                    config.ScriptName == null ? "" : ProcessOutput.QuoteSingleArgument(config.ScriptName),
                    config.ScriptArguments
                }.Where(s => !string.IsNullOrEmpty(s))),
                WorkingDirectory = config.WorkingDirectory,
                UseShellExecute = false
            };

            if (string.IsNullOrEmpty(psi.FileName)) {
                throw new FileNotFoundException(Strings.DebugLaunchInterpreterMissing);
            }
            if (!File.Exists(psi.FileName)) {
                throw new FileNotFoundException(Strings.DebugLaunchInterpreterMissing_Path.FormatUI(psi.FileName));
            }
            if (string.IsNullOrEmpty(psi.WorkingDirectory)) {
                psi.WorkingDirectory = PathUtils.GetParent(config.ScriptName);
            }
            if (string.IsNullOrEmpty(psi.WorkingDirectory)) {
                throw new DirectoryNotFoundException(Strings.DebugLaunchWorkingDirectoryMissing);
            }
            if (!Directory.Exists(psi.WorkingDirectory)) {
                throw new DirectoryNotFoundException(Strings.DebugLaunchWorkingDirectoryMissing_Path.FormatUI(psi.FileName));
            }

            foreach (var kv in provider.GetPythonToolsService().GetFullEnvironment(config)) {
                psi.Environment[kv.Key] = kv.Value;
            }

            var pyService = provider.GetPythonToolsService();
            // Pause if the user has requested it.
            string pauseCommand = null;
            if (config.GetLaunchOption(PythonConstants.NeverPauseOnExit).IsTrue()) {
                // Do nothing
            } else if (pyService.DebuggerOptions.WaitOnAbnormalExit && pyService.DebuggerOptions.WaitOnNormalExit) {
                pauseCommand = "pause";
            } else if (pyService.DebuggerOptions.WaitOnAbnormalExit && !pyService.DebuggerOptions.WaitOnNormalExit) {
                pauseCommand = "if errorlevel 1 pause";
            } else if (pyService.DebuggerOptions.WaitOnNormalExit && !pyService.DebuggerOptions.WaitOnAbnormalExit) {
                pauseCommand = "if not errorlevel 1 pause";
            }

            if (!string.IsNullOrEmpty(pauseCommand)) {
                psi.Arguments = string.Format("/c \"{0} {1}\" & {2}",
                    ProcessOutput.QuoteSingleArgument(psi.FileName),
                    psi.Arguments,
                    pauseCommand
                );
                psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            }

            return psi;
        }
    }

    public sealed class DebugTargetInfo : IDisposable {
        private readonly IServiceProvider _provider;
        public VsDebugTargetInfo Info;

        public DebugTargetInfo(IServiceProvider provider) {
            _provider = provider;
            Info = new VsDebugTargetInfo();
            Info.cbSize = (uint)Marshal.SizeOf(Info);
        }

        private static string UnquotePath(string p) {
            if (string.IsNullOrEmpty(p) || !p.StartsWith("\"") || !p.EndsWith("\"")) {
                return p;
            }
            return p.Substring(1, p.Length - 2);
        }

        /// <summary>
        /// Validates the provided info.
        /// </summary>
        /// <exception cref="FileNotFoundException">
        /// The provided executable was not found.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// The provided working directory was not found.
        /// </exception>
        public void Validate() {
            var cwd = UnquotePath(Info.bstrCurDir);
            if (string.IsNullOrEmpty(cwd)) {
                throw new DirectoryNotFoundException(Strings.DebugLaunchWorkingDirectoryMissing);
            }
            if (!Directory.Exists(cwd)) {
                throw new DirectoryNotFoundException(Strings.DebugLaunchWorkingDirectoryMissing_Path.FormatUI(Info.bstrCurDir));
            }

            var exe = UnquotePath(Info.bstrExe);
            if (string.IsNullOrEmpty(exe)) {
                throw new FileNotFoundException(Strings.DebugLaunchInterpreterMissing);
            }
            if (!File.Exists(exe)) {
                throw new FileNotFoundException(Strings.DebugLaunchInterpreterMissing_Path.FormatUI(exe));
            }
        }

        /// <summary>
        /// Starts the debugger with the provided info.
        /// </summary>
        /// <exception cref="FileNotFoundException">
        /// The provided executable was not found.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// The provided working directory was not found.
        /// </exception>
        public void Launch() {
            Validate();

            VsShellUtilities.LaunchDebugger(_provider, Info);
        }

        public void Dispose() {
            if (Info.pClsidList != IntPtr.Zero) {
                Marshal.FreeCoTaskMem(Info.pClsidList);
                Info.pClsidList = IntPtr.Zero;
            }
        }
    }
}
