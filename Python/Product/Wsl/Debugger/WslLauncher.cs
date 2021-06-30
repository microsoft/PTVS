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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Wsl.Debugger {
    class WslPythonLauncher : IProjectLauncher {
        private readonly IServiceProvider _serviceProvider;
        private readonly LaunchConfiguration _config;

        private static readonly Lazy<string> _bashPath = new Lazy<string>(() => {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysNative", "bash.exe");
            if (File.Exists(path)) {
                return path;
            }
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "bash.exe");
            if (File.Exists(path)) {
                return path;
            }
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "bash.exe");
            if (File.Exists(path)) {
                return path;
            }
            return null;
        });

        public WslPythonLauncher(IServiceProvider provider, LaunchConfiguration config) {
            _serviceProvider = provider;
            _config = config;
        }

        public int LaunchProject(bool debug) {
            return Launch(_config, debug);
        }

        public int LaunchFile(string/*!*/ file, bool debug) {
            var config = _config.Clone();
            config.ScriptName = file;
            return Launch(config, debug);
        }

        private int Launch(LaunchConfiguration config, bool debug) {
            DebugLaunchHelper.RequireStartupFile(config);

            if (debug) {
                StartWithDebugger(config);
            } else {
                StartWithoutDebugger(config).Dispose();
            }

            return VSConstants.S_OK;
        }

        private string GetPyVariable(Version version) {
            if (version == null) {
                return "if hash python3 2>/dev/null; then export py=python3;" +
                    "elif hash python2 2>/dev/null; then export py=python2;" +
                    "elif hash python 2>/dev/null; then export py=python;" +
                    "else echo 'Cannot locate Python'; read -p 'Press Enter to exit . . .'; exit 1;" +
                    "fi";
            }

            return (
                "if hash python{0}.{1} 2>/dev/null; then export py=python{0}.{1};" +
                "elif hash python{0} 2>/dev/null; then export py=python{0}; echo 'WARNING: Using python{0} instead of python{0}.{1}';" +
                "else echo 'Cannot locate Python {0}.{1}'; read -p 'Press Enter to exit . . .'; exit 1;" +
                "fi"
                ).FormatInvariant(version.Major, version.Minor);
        }

        private string CreateScriptFile(params string[] lines) {
            var sh = Path.GetTempFileName();

            var content = string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));
#if !DEBUG
            content += "\nrm -f " + QuoteSingleArgument(FixPath(sh)) + "\n";
#endif

            var bytes = new UTF8Encoding(false).GetBytes(content);
            using (var stream = new FileStream(sh, FileMode.Truncate, FileAccess.Write, FileShare.None)) {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            return sh;
        }

        private Process StartWithoutDebugger(LaunchConfiguration config) {
            var sh = CreateScriptFile(
                GetPyVariable(config.Interpreter?.Version),
                // TODO: Search paths
                "$py {0} {1} {2}".FormatInvariant(
                    config.InterpreterArguments,
                    config.ScriptName == null ? "" : QuoteSingleArgument(FixPath(config.ScriptName)),
                    config.ScriptArguments
                ),
                "read -p 'Press Enter to exit . . .'"
            );

            var psi = new ProcessStartInfo {
                FileName = _bashPath.Value ?? throw new InvalidOperationException("WSL is not installed"),
                Arguments = QuoteSingleArgument(FixPath(sh)),
                WorkingDirectory = config.WorkingDirectory,
                UseShellExecute = false
            };

            return Process.Start(psi);
        }

        private static string PtvsdSearchPath {
            get {
                return PathUtils.GetParent(PathUtils.GetParent(PythonToolsInstallPath.GetFile("ptvsd\\__main__.py")));
            }
        }

        private void StartWithDebugger(LaunchConfiguration config) {
            int port = Enumerable.Range(new Random().Next(49152, 65536), 60000)
                .Except(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Select(c => c.LocalEndPoint.Port))
                .First();

            string secret = Path.GetRandomFileName().Replace(".", "");

            var sh = CreateScriptFile(
                GetPyVariable(config.Interpreter?.Version),
                // TODO: Search paths
                string.Join(" ",
                    "PYTHONPATH={0}".FormatInvariant(QuoteSingleArgument(FixPath(PtvsdSearchPath))),
                    "$py",
                    config.InterpreterArguments,
                    "-m",
                    "ptvsd",
                    "-s", secret,
                    "-i", "127.0.0.1",
                    "-p", port.ToString(),
                    "--wait",
                    QuoteSingleArgument(FixPath(config.ScriptName)),
                    config.ScriptArguments
                )
            );

            var psi = new ProcessStartInfo {
                FileName = _bashPath.Value ?? throw new InvalidOperationException("WSL is not installed"),
                Arguments = QuoteSingleArgument(FixPath(sh)),
                WorkingDirectory = config.WorkingDirectory,
                UseShellExecute = false
            };

            var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));

            using (var proc = Process.Start(psi)) {
                AttachToProcess(
                    dte,
                    proc,
                    PythonRemoteDebugPortSupplier.PortSupplierGuid,
                    "tcp://{0}@127.0.0.1:{1}".FormatInvariant(secret, port)
                );
            }
        }

        private static bool AttachToProcess(EnvDTE.DTE dte, Process processOutput, Guid portSupplier, string transportQualifierUri) {
            var debugger3 = (EnvDTE90.Debugger3)dte.Debugger;
            var transports = debugger3.Transports;
            EnvDTE80.Transport transport = null;
            for (int i = 1; i <= transports.Count; ++i) {
                var t = transports.Item(i);
                if (Guid.Parse(t.ID) == portSupplier) {
                    transport = t;
                    break;
                }
            }
            if (transport == null) {
                return false;
            }

            var processes = debugger3.GetProcesses(transport, transportQualifierUri);
            if (processes.Count < 1) {
                return false;
            }

            var process = processes.Item(1);
            return AttachToProcess(processOutput, process);
        }

        private static bool AttachToProcess(Process processOutput, EnvDTE.Process process, Guid[] engines = null) {
            // Retry the attach itself 3 times before displaying a Retry/Cancel
            // dialog to the user.
            var dte = process.DTE;
            dte.SuppressUI = true;
            try {
                try {
                    if (engines == null) {
                        process.Attach();
                    } else {
                        var process3 = process as EnvDTE90.Process3;
                        if (process3 == null) {
                            return false;
                        }
                        process3.Attach2(engines.Select(engine => engine.ToString("B")).ToArray());
                    }
                    return true;
                } catch (COMException) {
                    if (processOutput.WaitForExit(500)) {
                        // Process exited while we were trying
                        return false;
                    }
                }
            } finally {
                dte.SuppressUI = false;
            }

            // Another attempt, but display UI.
            process.Attach();
            return true;
        }


        private static string FixPath(string path) {
            // TODO: Use `bash.exe -c pwd` to resolve path
            var dir = PathUtils.GetParent(path);
            var newDir = GetFinalPathName(dir);
            if (newDir != dir) {
                // Minimize normalization that happens here
                path = PathUtils.TrimEndSeparator(newDir) + "\\" + PathUtils.GetFileOrDirectoryName(path);
            }

            if (Path.IsPathRooted(path)) {
                var root = Path.GetPathRoot(path);
                var newRoot = "/mnt/" + root.ToLowerInvariant().Replace(":", "");
                path = newRoot + path.Substring(root.Length);
            }

            return path.Replace('\\', '/');
        }

        private static string QuoteSingleArgument(string arg) {
            return "'{0}'".FormatInvariant(arg.Replace("'", "'\\''"));
        }

        public static string GetFinalPathName(string dir) {
            using (var dirHandle = NativeMethods.CreateFile(
                dir,
                NativeMethods.FileDesiredAccess.FILE_LIST_DIRECTORY,
                NativeMethods.FileShareFlags.FILE_SHARE_DELETE |
                    NativeMethods.FileShareFlags.FILE_SHARE_READ |
                    NativeMethods.FileShareFlags.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.FileCreationDisposition.OPEN_EXISTING,
                NativeMethods.FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero
            )) {
                if (!dirHandle.IsInvalid) {
                    uint pathLen = NativeMethods.MAX_PATH + 1;
                    uint res;
                    StringBuilder filePathBuilder;
                    for (; ; ) {
                        filePathBuilder = new StringBuilder(checked((int)pathLen));
                        res = NativeMethods.GetFinalPathNameByHandle(
                            dirHandle,
                            filePathBuilder,
                            pathLen,
                            0
                        );
                        if (res != 0 && res < pathLen) {
                            // we had enough space, and got the filename.
                            break;
                        }
                    }

                    if (res != 0) {
                        Debug.Assert(filePathBuilder.ToString().StartsWithOrdinal("\\\\?\\"));
                        return filePathBuilder.ToString().Substring(4);
                    }
                }
            }
            return dir;
        }

    }

    [Export(typeof(IPythonLauncherProvider))]
    class WslPythonLauncherProvider : IPythonLauncherProvider {
        [Import(typeof(SVsServiceProvider))]
        internal IServiceProvider _provider = null;

        public string LocalizedName => "WSL Launcher";

        public int SortPriority => 200;

        public string Name => "WSL Launcher";

        public string Description => "Uses the Windows Subsystem for Linux (bash.exe) to launch your project.";

        public IProjectLauncher CreateLauncher(IPythonProject project) {
            return new WslPythonLauncher(_provider, project.GetLaunchConfigurationOrThrow());
        }

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties) {
            return new DefaultPythonLauncherOptions(properties);
        }
    }
}
