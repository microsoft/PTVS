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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Hpc.Scheduler;
using Microsoft.Hpc.Scheduler.Properties;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Hpc {
    class HpcLauncher : IProjectLauncher {
        private readonly IPythonProject _project;

        private static readonly Guid _authGuid = Guid.NewGuid();
        private static Thread _listenerThread;
        private static Socket _listenerSocket;
        private static readonly AutoResetEvent _listenerThreadStarted = new AutoResetEvent(false);
        private const string MpiShimExe = "Microsoft.PythonTools.MpiShim.exe";
        private static bool _createdGeneralPane;
        private static HiddenForm _pumpForm;

        #region Debugger constants for Python debugger - cloned from AD7Engine.cs

        private const string DebugEngineId = "{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}";
        private static Guid DebugEngineGuid = new Guid(DebugEngineId);

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on an abnormal exit.
        /// </summary>
        private const string WaitOnAbnormalExitSetting = "WAIT_ON_ABNORMAL_EXIT";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on a normal exit.
        /// </summary>
        private const string WaitOnNormalExitSetting = "WAIT_ON_NORMAL_EXIT";

        /// <summary>
        /// Specifies if the output should be redirected to the visual studio output window.
        /// </summary>
        private const string RedirectOutputSetting = "REDIRECT_OUTPUT";

        /// <summary>
        /// Specifies if the debugger should break on SystemExit exceptions with an exit code of zero.
        /// </summary>
        public const string BreakSystemExitZero = "BREAK_SYSTEMEXIT_ZERO";

        /// <summary>
        /// Specifies if the debugger should step/break into std lib code.
        /// </summary>
        public const string DebugStdLib = "DEBUG_STDLIB";

        /// <summary>
        /// Specifies options which should be passed to the Python interpreter before the script.  If
        /// the interpreter options should include a semicolon then it should be escaped as a double
        /// semi-colon.
        /// </summary>
        private const string InterpreterOptions = "INTERPRETER_OPTIONS";

        /// <summary>
        /// Specifies a directory mapping in the form of:
        /// 
        /// OldDir|NewDir
        /// 
        /// for mapping between the files on the local machine and the files deployed on the
        /// running machine.
        /// </summary>
        private const string DirMappingSetting = "DIR_MAPPING";

        #endregion

        public HpcLauncher(IPythonProject project) {
            _project = project;
        }

        #region IPythonLauncher Members

        public int LaunchProject(bool debug) {
            string filename = _project.GetProperty(CommonConstants.StartupFile);
            return LaunchFile(filename, debug);
        }

        private static void EnsureListenerThread() {
            lock (_listenerThreadStarted) {
                if (_listenerThread == null || !_listenerThread.IsAlive) {
                    _listenerThreadStarted.Reset();
                    _listenerThread = new Thread(SocketThread);
                    _listenerThread.SetApartmentState(ApartmentState.MTA);
                    _listenerThread.Name = "Hpc Debugging Thread";
                    _listenerThread.Start();
                    _listenerThreadStarted.WaitOne();
                }
            }
        }

        private static void SocketThread() {
            try {
                _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                _listenerSocket.Listen(512);
            } finally {
                _listenerThreadStarted.Set();
            }

            try {
                for (; ; ) {
                    Socket request;
                    try {
                        request = _listenerSocket.Accept();
                    } catch (IOException) {
                        lock (_listenerThreadStarted) {
                            _listenerThread = null;
                            _listenerSocket = null;
                        }

                        return;
                    }

                    ThreadPool.QueueUserWorkItem(ProcessDebugRequest, request);
                }
            } catch (Exception e) {
                Debug.Fail("Unexpected exception" + e);
            }
        }

        private static void ProcessDebugRequest(object socket) {
            Socket request = (Socket)socket;
            try {
                var secureStream = new NegotiateStream(new NetworkStream(request, false), true);
                
                secureStream.AuthenticateAsServer();
                
                if (!secureStream.IsAuthenticated || !secureStream.IsEncrypted) {
                    request.Close();
                    return;
                }

                WindowsIdentity winIdentity = secureStream.RemoteIdentity as WindowsIdentity;
                if (winIdentity == null ||
                    winIdentity.User != System.Security.Principal.WindowsIdentity.GetCurrent().User) {
                    request.Close();
                    return;
                }

                var reader = new StreamReader(secureStream);

                string auth = reader.ReadLine();
                Guid g;
                if (!Guid.TryParse(auth, out g) || g != _authGuid) {
                    request.Close();
                    return;
                }

                string exe = reader.ReadLine();
                string curDir = reader.ReadLine();
                string projectDir = reader.ReadLine();
                string args = reader.ReadLine();
                string machineName = reader.ReadLine();
                string options = reader.ReadLine();

                uint pid = 0;
                string errorText = "";
                var res = _pumpForm.BeginInvoke((Action)(() => {
                    pid = LaunchDebugger(exe, curDir, projectDir, args, machineName, options, out errorText);
                }));
                res.AsyncWaitHandle.WaitOne();

                var writer = new StreamWriter(secureStream);
                writer.WriteLine(pid.ToString());
                if (pid == 0) {
                    writer.WriteLine(errorText.Length);
                    writer.WriteLine(errorText);
                }
                writer.Flush();
            } catch (IOException) {
            } catch (Exception) {
                Debug.Assert(false);
            }
        }

        private static void EnsureHiddenForm() {
            if (_pumpForm == null) {
                _pumpForm = new HiddenForm();
                _pumpForm.Show();
            }
        }

        class HiddenForm : Form {
            protected override void OnShown(EventArgs e) {
                this.Hide();
            }

            const int SW_HIDE = 0;
            const int HWND_MESSAGE = -3;

            [DllImport("user32.dll")]
            static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SetParent(this.Handle, new IntPtr(HWND_MESSAGE)); }

            protected override void SetVisibleCore(bool value) {
                ShowWindow(Handle, SW_HIDE);
            }
        }

        private static uint LaunchDebugger(string exe, string curDir, string projectDir, string args, string machineName, string options, out string error) {
            var debugger = (IVsDebugger2)HpcSupportPackage.GetGlobalService(typeof(SVsShellDebugger));
            VsDebugTargetInfo2 debugInfo = new VsDebugTargetInfo2();

            debugInfo.cbSize = (uint)Marshal.SizeOf(typeof(VsDebugTargetInfo2));
            debugInfo.dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_Custom;
            debugInfo.guidLaunchDebugEngine = DebugEngineGuid;
            debugInfo.dwDebugEngineCount = 1;
            debugInfo.guidPortSupplier = new Guid("{708C1ECA-FF48-11D2-904F-00C04FA302A1}");     // local port supplier
            debugInfo.LaunchFlags = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_WaitForAttachComplete | (uint)__VSDBGLAUNCHFLAGS5.DBGLAUNCH_BreakOneProcess;
            debugInfo.bstrRemoteMachine = machineName;
            debugInfo.bstrExe = exe;
            debugInfo.bstrCurDir = curDir;
            debugInfo.bstrArg = args;
            debugInfo.bstrOptions = DirMappingSetting + "=" + projectDir + "|" + curDir;
            if (!String.IsNullOrWhiteSpace(options)) {
                debugInfo.bstrOptions += ";" + options;
            }
            debugInfo.pDebugEngines = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Guid)));

            if (debugInfo.pDebugEngines == IntPtr.Zero) {
                error = "Out of memory";
                return 0;
            }

            try {
                Marshal.StructureToPtr(DebugEngineGuid, debugInfo.pDebugEngines, false);

                IntPtr memory = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VsDebugTargetInfo2)));
                if (memory == IntPtr.Zero) {
                    error = "Out of memory";
                    return 0;
                }

                try {
                    Marshal.StructureToPtr(debugInfo, memory, false);
                    
                    int hr = debugger.LaunchDebugTargets2(1, memory);

                    if (ErrorHandler.Failed(hr)) {
                        var uiShell = (IVsUIShell)HpcSupportPackage.GetGlobalService(typeof(SVsUIShell));
                        string errorText;
                        if (ErrorHandler.Succeeded(uiShell.GetErrorInfo(out errorText))) {
                            error = errorText;
                        } else {
                            error = "Unknown error";
                        }

                        OutputState(String.Format("Launching debugger on server failed ({0:X}):\r\n\r\n{1}\r\n", hr, error));
                        return 0;
                    } else {
                        var structure = (VsDebugTargetInfo2)Marshal.PtrToStructure(memory, typeof(VsDebugTargetInfo2));

                        error = "";
                        return structure.dwProcessId;
                    }
                } finally {
                    Marshal.FreeCoTaskMem(memory);
                }
            } finally {
                //Marshal.FreeCoTaskMem(debugInfo.pDebugEngines);
            }
        }

        public int LaunchFile(string filename, bool debug) {
            var clusterEnv = new ClusterEnvironment(_project.GetProperty(ClusterOptions.RunEnvironmentSetting));

            if (debug) {
                EnsureHiddenForm();
                EnsureListenerThread();
            }

            string workingDir, publishUrl;
            if (clusterEnv.HeadNode == "localhost") {
                workingDir = _project.GetProperty(ClusterOptions.WorkingDirSetting);
                if (String.IsNullOrWhiteSpace(workingDir)) {
                    workingDir = Path.Combine(Path.GetTempPath(), "HpcPyDebug" + Guid.NewGuid().ToString());
                }
                if (!Directory.Exists(workingDir)) {
                    Directory.CreateDirectory(workingDir);
                }

                publishUrl = "file://" + workingDir;
            } else {
                workingDir = GetWorkingDir(clusterEnv);

                // make sure we have a valid deployement dir as well
                string deploymentDir;
                if (!TryGetDeploymentDir(out deploymentDir)) {
                    return VSConstants.S_OK;
                }
                publishUrl = deploymentDir;
            }            

            string exe, arguments;
            if (!TryBuildCommandLine(debug, clusterEnv, filename, workingDir, out exe, out arguments) || 
                !TryPublishProject(clusterEnv, publishUrl)) {
                return VSConstants.S_OK;
            }

            if (clusterEnv.HeadNode == "localhost") {
                // run locally               
                if (debug) {
                    var startInfo = new ProcessStartInfo(exe, arguments);

                    LaunchRedirectedToVsOutputWindow(startInfo, false);
                } else {
                    Process.Start(exe, arguments);
                }
            } else {
                EnsureGeneralPane();

                var commandLine = exe + " " + arguments;

                var scheduler = new Scheduler();
                scheduler.Connect(clusterEnv.HeadNode);
                var job = CreateSchedulerJob(commandLine, clusterEnv, scheduler, debug);

                scheduler.AddJob(job);
                SetStatus("Scheduling job on server...");

                ScheduleJob(scheduler, job);
            }

            return VSConstants.S_OK;
        }

        private bool TryGetDeploymentDir(out string deploymentDir) {
            deploymentDir = _project.GetProperty(ClusterOptions.DeploymentDirSetting);
            if (String.IsNullOrWhiteSpace(deploymentDir)) {
                deploymentDir = _project.GetProperty("PublishUrl");
                if (!String.IsNullOrWhiteSpace(deploymentDir)) {
                    if (new Uri(deploymentDir).Scheme != "file") {
                        MessageBox.Show(
                            "Publishing scheme is not a file path, please configure the deployment directory for HPC debugging in project properties",
                            "Deployment Directory Not Configured",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        return false;
                    }
                } else {
                    MessageBox.Show(
                        "Publishing path is not set, please configure the publish path in project properties.",
                        "Publishing Path Not Configured",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return false;
                }
            }

            return true;
        }

        #endregion

        private ISchedulerJob CreateSchedulerJob(string commandLine, ClusterEnvironment clusterEnv, Scheduler scheduler, bool debug) {
            var job = scheduler.CreateJob();
            job.IsExclusive = true;
            job.Name = debug ? "Python MPI Debugging Session" : "Python MPI Session";

            string mpiExecCommand, deploymentDir;
            if (!TryGetMpiExecCommand(clusterEnv.HeadNode, out mpiExecCommand) ||
                !TryGetDeploymentDir(out deploymentDir)) {
                // unreachable, we've already built the command line (which gets the MPI exec command as well) and checked that we could get the working 
                throw new InvalidOperationException();
            }

            string workingDir = GetWorkingDir(clusterEnv);

            ISchedulerTask[] tasks;

            var runTask = job.CreateTask();
            runTask.Name = debug ? "Debug MPI Task" : "Run MPI Task";

            if (!IsPathUnc(workingDir)) {
                // we need to copy the files locally onto the cluster
                var copyTask = job.CreateTask();
                copyTask.Name = "Copy files";
                copyTask.Type = TaskType.NodePrep;
                var cleanupTask = job.CreateTask();
                cleanupTask.Name = "Cleanup files";

                tasks = new[] { copyTask, runTask, cleanupTask };
                copyTask.CommandLine = String.Format("\"%WINDIR%\\System32\\cmd.exe\" /c mkdir \"{1}\" & robocopy /s \"{0}\" \"{1}\" & if not errorlevel 2 exit 0", deploymentDir, workingDir);
                cleanupTask.CommandLine = String.Format("\"%WINDIR%\\System32\\cmd.exe\" /c rmdir /s /q \"{0}\"", workingDir);
                cleanupTask.Type = TaskType.NodeRelease;
            } else {
                tasks = new[] { runTask };
            }

            runTask.CommandLine = commandLine;
            runTask.WorkDirectory = workingDir;

            job.NodeGroups.Add(clusterEnv.PickNodesFrom);

            SetJobParameters(clusterEnv, job, tasks);

            job.AddTasks(tasks);
            job.OnJobState += JobStateChanged;
            job.OnTaskState += OnTaskStateChange;

            return job;
        }

        private string GetWorkingDir(ClusterEnvironment clusterEnv) {
            string workingDir = _project.GetProperty(ClusterOptions.WorkingDirSetting);
            if (String.IsNullOrWhiteSpace(workingDir)) {
                if (clusterEnv.HeadNode == "localhost") {
                    return _project.GetWorkingDirectory();
                }
                return "%TEMP%\\%USERNAME%\\" + _project.ProjectName;
            }
            return workingDir;
        }

        private static void OnTaskStateChange(object sender, ITaskStateEventArg args) {
            if (args.NewState == TaskState.Failed) {
                var scheduler = (Scheduler)sender;
                var job = scheduler.OpenJob(args.JobId);
                var task = job.OpenTask(args.TaskId);

                string output = task.Output;
                if (!String.IsNullOrWhiteSpace(output)) {
                    var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));
                    IVsOutputWindowPane pane;
                    if (ErrorHandler.Succeeded(outWin.GetPane(VSConstants.GUID_OutWindowGeneralPane, out pane))) {
                        pane.Activate();
                        pane.OutputString(output);
                    }
                }
            }
        }

        private static void SetJobParameters(ClusterEnvironment clusterEnv, ISchedulerJob job, ISchedulerTask[] tasks) {
            if (clusterEnv.SelectedNodes != null) {
                SelectClusterNodes(clusterEnv, job, tasks);
            } else {
                switch (clusterEnv.ScheduleProcessPer) {
                    case ScheduleProcessPer.Core:
                        job.MinimumNumberOfCores = job.MaximumNumberOfCores = clusterEnv.NumberOfProcesses;
                        foreach (var task in tasks) {
                            if (ShouldSetTaskParameters(task)) {
                                task.MinimumNumberOfCores = task.MaximumNumberOfCores = clusterEnv.NumberOfProcesses;
                            }
                        }
                        break;
                    case ScheduleProcessPer.Node:
                        job.MinimumNumberOfNodes = job.MaximumNumberOfNodes = clusterEnv.NumberOfProcesses;
                        foreach (var task in tasks) {
                            if (ShouldSetTaskParameters(task)) {
                                task.MinimumNumberOfNodes = task.MaximumNumberOfNodes = clusterEnv.NumberOfProcesses;
                            }
                        }
                        break;
                    case ScheduleProcessPer.Socket:
                        job.MinimumNumberOfSockets = job.MaximumNumberOfSockets = clusterEnv.NumberOfProcesses;
                        foreach (var task in tasks) {
                            if (ShouldSetTaskParameters(task)) {
                                task.MinimumNumberOfSockets = task.MaximumNumberOfSockets = clusterEnv.NumberOfProcesses;
                            }
                        }
                        break;
                }
            }
        }

        private static bool ShouldSetTaskParameters(ISchedulerTask task) {
            return task.Type != TaskType.NodePrep && task.Type != TaskType.NodeRelease;
        }

        private static readonly string[] _pyDebuggerFiles = new string[] {
            "visualstudio_py_debugger.py",
            "visualstudio_py_launcher.py",
            "PyDebugAttach.dll",
            "PyDebugAttachX86.dll",
            "Microsoft.PythonTools.Debugger.dll",
            "Microsoft.PythonTools.Analysis.dll",
            "Microsoft.PythonTools.Attacher.exe",
            "Microsoft.PythonTools.AttacherX86.exe"
        };

        private static readonly string[] _hpcDebuggerFiles = new string[] {
            MpiShimExe 
        };

        private static readonly string[] _vsAssemblies = new string[] { 
            typeof(IDebugBinderDirect100).Assembly.Location,        // Microsoft.VisualStudio.Debugger.Interop.10.0.dll, v2.0.50727
            typeof(IDebugAddress).Assembly.Location,                // Microsoft.VisualStudio.Debugger.InteropA.dll, v1.0.3705
            typeof(IOleMenuCommand).Assembly.Location,              // Microsoft.VisualStudio.Shell.Interop.8.0.dll, v1.1.4322
            typeof(IOleComponent2).Assembly.Location,               // Microsoft.VisualStudio.Shell.Interop.9.0.dll, v1.0.3705
            typeof(VsColors).Assembly.Location,                     // Microsoft.VisualStudio.Shell.10.0.dll, v4.0.30319
            typeof(Microsoft.VisualStudio.OLE.Interop.FILETIME).Assembly.Location    // Microsoft.VisualStudio.OLE.Interop.dll, v1.1.4322, MODULE_INFO structure refers to a type in this...
        };

        /// <summary>
        /// Publishes the project if the user has configured the publish on run.
        /// </summary>
        private bool TryPublishProject(ClusterEnvironment environment, string publishOverrideUrl) {
            if (_project.PublishBeforeRun() || environment.HeadNode == "localhost") {
                string msg = null;
                try {
                    var vsInstallDir = (string)HpcSupportPackage.Instance.ApplicationRegistryRoot.GetValue("InstallDir");

                    List<IPublishFile> allFiles = new List<IPublishFile>();

                    // add python debugger files
                    string pyInstallDir = GetPythonToolsInstallPath();
                    foreach (var file in _pyDebuggerFiles) {
                        allFiles.Add(new CopyFile(Path.Combine(pyInstallDir, file), file));
                    }

                    string pyHpcInstallDir = GetPythonHpcToolsInstallPath();
                    foreach (var file in _hpcDebuggerFiles) {
                        allFiles.Add(new CopyFile(Path.Combine(pyHpcInstallDir, file), file));
                    }

                    // add VS components that we need to run
                    foreach (var file in _vsAssemblies) {
                        allFiles.Add(new CopyFile(file, Path.GetFileName(file)));
                    }

                    // Add vs remote debugger components.
                    string basePath = Path.Combine(Path.Combine(vsInstallDir, "Remote Debugger"), _project.TargetPlatform().ToString()) + "\\";
                    foreach (var file in Directory.GetFiles(basePath)) {
                        allFiles.Add(new CopyFile(file, file.Substring(basePath.Length)));
                    }

                    if (!_project.Publish(new PublishProjectOptions(allFiles.ToArray(), publishOverrideUrl))) {
                        msg = "Publishing not configured or unknown publishing schema";
                    }
                } catch (PublishFailedException e) {
                    msg = e.InnerException.Message;
                }

                if (msg != null && CancelLaunch(msg)) {
                    return false;
                }
            }
            return true;
        }

        class CopyFile : IPublishFile {
            private readonly string _source, _dest;

            public CopyFile(string source, string dest) {
                _source = source;
                _dest = dest;
            }

            #region IPublishFile Members

            public string SourceFile {
                get { return _source; }
            }

            public string DestinationFile {
                get { return _dest; }
            }

            #endregion
        }

        /// <summary>
        /// Builds the command line that each task should execute.
        /// </summary>
        private bool TryBuildCommandLine(bool debug, ClusterEnvironment environment, string startupFile, string workingDir, out string exe, out string arguments) {
            exe = arguments = null;

            string schedulerNode = environment.HeadNode;

            var appCommand = _project.GetProperty(ClusterOptions.AppCommandSetting);
            if (String.IsNullOrWhiteSpace(appCommand)) {
                MessageBox.Show("The path to the interpreter on the cluster is not configured.  Please update project properties->Debug->Python Interpreter to point at the correct location of the interpreter", "Python Tools for Visual Studio");                
                return false;
            }
            string mpiExecCommand;
            if (!TryGetMpiExecCommand(schedulerNode, out mpiExecCommand)) {
                return false;
            }

            var appArgs = _project.GetProperty(ClusterOptions.AppArgumentsSetting);
            
            if (!String.IsNullOrWhiteSpace(workingDir)) {
                startupFile = Path.Combine(workingDir, startupFile);
            }
            exe = "\"" + mpiExecCommand + "\"";
            arguments = "";
            if (schedulerNode == "localhost") {
                arguments = "-n " + environment.NumberOfProcesses + " ";
            }
            string tmpArgs = "\"" + appCommand + "\" \"" + startupFile + "\" \"" + appArgs + "\"";
            
            if (debug) {
                arguments += "\"" +  Path.Combine(workingDir, MpiShimExe) + "\" " +
                    ((IPEndPoint)_listenerSocket.LocalEndPoint).Port + " " +
                    _authGuid.ToString() + " " +
                    GetLocalIPv4Address(schedulerNode) + " " +
                    "\"" + FixDir(workingDir) + "\" " +
                    "\"" + FixDir(_project.ProjectDirectory) + "\" " +
                    "\"" + GetDebugOptions(environment) + "\" " +
                    tmpArgs;
            } else {
                arguments += tmpArgs;
            }
            return true;
        }

        private string GetDebugOptions(ClusterEnvironment clusterEnv) {
            string options = "";

            if (PythonToolsPackage.Instance.OptionsPage.TeeStandardOutput) {
                options = RedirectOutputSetting + "=True";
            }
            if (PythonToolsPackage.Instance.OptionsPage.BreakOnSystemExitZero) {
                if (!String.IsNullOrEmpty(options)) {
                    options += ";";
                }
                options += BreakSystemExitZero + "=True";
            }
            if (PythonToolsPackage.Instance.OptionsPage.DebugStdLib) {
                if (!String.IsNullOrEmpty(options)) {
                    options += ";";
                }
                options += DebugStdLib + "=True";
            }

            if (clusterEnv.HeadNode == "localhost") { // don't wait on the cluster, there's no one to press enter.
                if (PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit) {
                    if (!String.IsNullOrEmpty(options)) {
                        options += ";";
                    }
                    options += WaitOnAbnormalExitSetting + "=True";
                }
                if (PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit) {
                    if (!String.IsNullOrEmpty(options)) {
                        options += ";";
                    }
                    options += WaitOnNormalExitSetting + "=True";
                }
            }

            var interpArgs = _project.GetProperty(CommonConstants.InterpreterArguments);
            if (!String.IsNullOrWhiteSpace(interpArgs)) {
                if (!String.IsNullOrEmpty(options)) {
                    options += ";";
                }
                options += InterpreterOptions + "=" + interpArgs.Replace(";", ";;");
            }
            return options;
        }

        private bool TryGetMpiExecCommand(string schedulerNode, out string mpiExecCommand) {
            mpiExecCommand = _project.GetProperty(ClusterOptions.MpiExecPathSetting);
            if (String.IsNullOrWhiteSpace(mpiExecCommand)) {
                if (schedulerNode == "localhost") {
                    string ccpHome = Environment.GetEnvironmentVariable("CCP_HOME");
                    string mpiexecPath;
                    if (String.IsNullOrWhiteSpace(ccpHome) || !File.Exists(mpiexecPath = Path.Combine(ccpHome, "Bin\\mpiexec.exe"))) {
                        string sdkHome = Environment.GetEnvironmentVariable("CCP_SDK");
                        if (String.IsNullOrWhiteSpace(sdkHome) || !File.Exists(mpiexecPath = Path.Combine(sdkHome, "Bin\\mpiexec.exe"))) {
                            MessageBox.Show("Could not find mpiexec.exe.  Please specify path in project settings->Debug or install Microsoft HPC Pack or Microsoft HPC Pack SDK", "Python Tools for Visual Studio");
                            return false;
                        }
                    }

                    mpiExecCommand = mpiexecPath;
                } else {
                    mpiExecCommand = "%CCP_HOME%\\Bin\\mpiexec.exe";
                }
            }
            return true;
        }

        /// <summary>
        /// make sure a directory doesn't end in a backslash which messes up our escaping.
        /// </summary>
        private string FixDir(string dir) {
            if (dir.EndsWith("\\")) {
                return dir.Substring(0, dir.Length - 1);
            }
            return dir;
        }

        private string GetLocalIPv4Address(string schedulerName) {
            IPEndPoint ep = null;

            try {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                    IPAddress[] addresses = Dns.GetHostAddresses(schedulerName);
                    foreach (IPAddress ip in addresses) {
                        try {
                            IPEndPoint remoteEP = new IPEndPoint(ip, 0);
                            ep = QueryRoutingInterface(socket, remoteEP);

                            if (ep.AddressFamily == AddressFamily.InterNetwork) {
                                break;
                            }
                        } catch (SocketException) {
                            // ignore it, and try another address
                        }
                    }
                }
            } catch (SocketException) {
                // if can't get EP, we will return machine name
            }

            if (ep != null) {
                return ep.Address.ToString();
            } else {
                return Environment.MachineName;
            }
        }

        private static IPEndPoint QueryRoutingInterface(Socket socket, IPEndPoint remoteEndPoint) {
            SocketAddress address = remoteEndPoint.Serialize();

            byte[] remoteAddrBytes = new byte[address.Size];
            for (int i = 0; i < address.Size; i++) {
                remoteAddrBytes[i] = address[i];
            }

            byte[] outBytes = new byte[remoteAddrBytes.Length];
            socket.IOControl(IOControlCode.RoutingInterfaceQuery, remoteAddrBytes, outBytes);
            for (int i = 0; i < address.Size; i++) {
                address[i] = outBytes[i];
            }

            EndPoint ep = remoteEndPoint.Create(address);
            return (IPEndPoint)ep;
        }

        private static void EnsureGeneralPane() {
            if (!_createdGeneralPane) {
                var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));
                var guid = VSConstants.GUID_OutWindowGeneralPane;
                outWin.CreatePane(ref guid, "General", 1, 0);
                _createdGeneralPane = true;
            }            
        }

        private static void ScheduleJob(Scheduler scheduler, ISchedulerJob job) {
            var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));

            IVsOutputWindowPane pane;
            if (ErrorHandler.Succeeded(outWin.GetPane(VSConstants.GUID_OutWindowGeneralPane, out pane))) {
                pane.Activate();

                pane.OutputString("Submitting job " + job.Id + Environment.NewLine);
            }

            var shell = (IVsUIShell)HpcSupportPackage.GetGlobalService(typeof(SVsUIShell));
            IntPtr owner;
            if (ErrorHandler.Succeeded(shell.GetDialogOwnerHwnd(out owner))) {
                scheduler.SetInterfaceMode(false, owner);
            }


            ThreadPool.QueueUserWorkItem(x => {
                try {
                    scheduler.SubmitJob(job, null, null);
                } catch (Exception ex) {
                    string msg;
                    msg = "Failed to submit job " + ex.ToString();
                    if (pane != null) {
                        pane.OutputString(msg);
                    } else {
                        MessageBox.Show(msg, "Python Tools for Visual Studio");
                    }
                }
            });
        }

        private static Process LaunchRedirectedToVsOutputWindow(ProcessStartInfo info, bool reportExit = true) {
            info.CreateNoWindow = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;            

            var process = Process.Start(info);

            new Thread(new Redirector(process, process.StandardOutput, reportExit).RedirectOutput).Start();
            new Thread(new Redirector(process, process.StandardError).RedirectOutput).Start();
            return process;
        }

        class Redirector {
            private readonly Process _process;
            private readonly StreamReader _reader;
            private readonly bool _reportExit;

            public Redirector(Process process, StreamReader reader, bool reportExit = false) {
                _process = process;
                _reader = reader;
                _reportExit = reportExit;
            }

            public void RedirectOutput() {
                var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));
                IVsOutputWindowPane pane;
                char[] buffer = new char[1024];
                if (ErrorHandler.Succeeded(outWin.GetPane(VSConstants.GUID_OutWindowDebugPane, out pane))) {                    
                    pane.Activate();

                    while (!_process.HasExited) {
                        int bytesRead = _reader.Read(buffer, 0, buffer.Length);
                        pane.OutputString(new string(buffer, 0, bytesRead));
                    }

                    if (_reportExit) {
                        if (_process.ExitCode != 0) {
                            SetStatus("Submitting job failed.");
                        }
                    }
                }
            }
        }

        private static void SelectClusterNodes(ClusterEnvironment clusterEnv, ISchedulerJob job, ISchedulerTask[] tasks) {
            foreach (var node in clusterEnv.SelectedNodes) {
                foreach (var task in tasks) {
                    if (ShouldSetTaskParameters(task)) {
                        task.RequiredNodes.Add(node);
                    }
                }
            }

            switch (clusterEnv.ScheduleProcessPer) {
                case ScheduleProcessPer.Socket:
                    job.MinimumNumberOfSockets = job.MaximumNumberOfSockets = clusterEnv.NumberOfProcesses;
                    foreach (var task in tasks) {
                        if (ShouldSetTaskParameters(task)) {
                            task.MinimumNumberOfSockets = task.MaximumNumberOfSockets = clusterEnv.NumberOfProcesses;
                        }
                    }
                    break;
                case ScheduleProcessPer.Node:
                    job.MinimumNumberOfNodes = job.MaximumNumberOfNodes = clusterEnv.NumberOfProcesses;
                    foreach (var task in tasks) {
                        if (ShouldSetTaskParameters(task)) {
                            task.MinimumNumberOfNodes = task.MaximumNumberOfNodes = clusterEnv.NumberOfProcesses;
                        }
                    }
                    break;
                case ScheduleProcessPer.Core:
                    job.MinimumNumberOfCores = job.MaximumNumberOfCores = clusterEnv.NumberOfProcesses;
                    foreach (var task in tasks) {
                        if (ShouldSetTaskParameters(task)) {
                            task.MinimumNumberOfCores = task.MaximumNumberOfCores = clusterEnv.NumberOfProcesses;
                        }
                    }
                    break;
            }
        }

        private void JobStateChanged(object sender, JobStateEventArg e) {
            string newJobState = String.Format("Job state for job {0} changed to {1}", e.JobId, GetStateDescription(e.NewState));
            
            OutputState(newJobState);
            SetStatus(newJobState);
        }

        private static void OutputState(string state) {
            var outWin = (IVsOutputWindow)CommonPackage.GetGlobalService(typeof(IVsOutputWindow));
            IVsOutputWindowPane pane;
            if (ErrorHandler.Succeeded(outWin.GetPane(VSConstants.GUID_OutWindowGeneralPane, out pane))) {
                pane.Activate();

                pane.OutputString(state + Environment.NewLine);
            }
        }

        private static void SetStatus(string text) {
            var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));

            statusBar.SetText(text);
        }

        private static string GetStateDescription(JobState jobState) {
            switch (jobState) {
                case JobState.Canceled: return "Canceled";
                case JobState.Failed: return "Failed";
                case JobState.Running: return "Running";
                case JobState.Submitted: return "Submitted";
                case JobState.Validating: return "Validating";
                case JobState.Queued: return "Queued";
                case JobState.Finished: return "Finished";
                case JobState.Finishing: return "Finishing";
                case JobState.ExternalValidation: return "External Validation";
                case JobState.Configuring: return "Configuring";
                case JobState.Canceling: return "Cancelling";
                default: return "Unknown";
            }
        }

        private static bool CancelLaunch(string reason) {
            if (MessageBox.Show(
                String.Format("Publishing of project failed: {0}\r\nLaunch project anyway?", reason),
                "Publish project failed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.No) {
                return true;
            }
            return false;
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        internal static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(typeof(PythonToolsPackage).Assembly.Location);
            if (File.Exists(Path.Combine(path, "PyDebugAttach.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.5");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        internal static string GetPythonHpcToolsInstallPath() {
            string path = Path.GetDirectoryName(typeof(HpcSupportPackage).Assembly.Location);
            if (File.Exists(Path.Combine(path, MpiShimExe))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools HPC Support\\1.5");
                    if (File.Exists(Path.Combine(toolsPath, MpiShimExe))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (HpcSupportPackage.Instance != null) {
                return HpcSupportPackage.Instance.ApplicationRegistryRoot;
            }

            if (Environment.Is64BitOperatingSystem) {
#if DEV11
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            } else {
#if DEV11
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }

        private static bool IsPathUnc(string path) {
            Uri uri;
            if (!string.IsNullOrWhiteSpace(path) && Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out uri)) {
                return uri.IsAbsoluteUri && uri.IsUnc;
            }

            return false;
        }
    }
}
