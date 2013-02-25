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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

[assembly: InternalsVisibleTo("Microsoft.PythonTools.Debugger, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]
[assembly: InternalsVisibleTo("Microsoft.PythonTools.AttacherX86, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293")]

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Handles attach logic for attaching to Python processes.  We spin up a new thread in the target process
    /// which loads our library and then the library handles the attach info. We use a shared memory block to
    /// communicate with the target process - both sending over where to load visualstudio_py_debugger.py, what port number to
    /// use, and sending back the Python language version and any error information.
    /// 
    /// Also supports detecting if a process has Python loaded or not - which is fairly simple but requires a helper
    /// process on 64-bit machines because VS is 32-bit.
    /// </summary>
    internal sealed class DebugAttach : IDisposable {
        private readonly EventWaitHandle _attachDone;
        private readonly ConnErrorMessages _error;
        private int _langVersion;

        private static Process _checkProcess;
        private static readonly string[] _pythonMods = new[] { 
            "python24.dll",   "python25.dll",   "python26.dll",   "python27.dll",   "python30.dll",   "python31.dll",   "python32.dll", "python33.dll",
            "python24_d.dll", "python25_d.dll", "python26_d.dll", "python27_d.dll", "python30_d.dll", "python31_d.dll", "python32_d.dll", "python33_d.dll",
        };

        private static IsWow64Process _isWow64;
        private static bool _checkedIsWow64;
        private static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        const int MAX_PATH = 260;
        const int MAX_MODULE_NAME32 = 255;

        private DebugAttach(ConnErrorMessages error) {
            _error = error;
        }

        private DebugAttach(EventWaitHandle attachDone, ConnErrorMessages error, int langVersion) {
            _attachDone = attachDone;
            _error = error;
            _langVersion = langVersion;
        }

        public static int Main(string[] args) {
            int pid, portNum;
            Guid debugId;

            if (args.Length == 1 && args[0] == "CHECK") {
                return RunCheck();
            } else if (args.Length != 4 || !Int32.TryParse(args[0], out pid) || !Int32.TryParse(args[1], out portNum) || !Guid.TryParse(args[2], out debugId)) {
                Help();
                return -1;
            }

            EventWaitHandle doneEvent;
            try {
                doneEvent = AutoResetEvent.OpenExisting(args[3]);
            } catch {
                Help();
                return -2;
            }

            var res = AttachWorker(pid, portNum, debugId, doneEvent);
            return ((int)res._error) | (res._langVersion << 16);
        }

        public static bool IsPythonProcess(int id) {
            bool isTarget64Bit = Is64BitProcess(id);
            bool isAttacher64Bit = Environment.Is64BitProcess;

            if (isTarget64Bit != isAttacher64Bit) {
                // we need a remote 64-bit process to check
                if (_checkProcess == null || _checkProcess.HasExited) {
                    _checkProcess = isTarget64Bit ? Create64BitProcess("CHECK") : Create32BitProcess("CHECK");
                }

                _checkProcess.StandardInput.WriteLine(id);
                if (_checkProcess.StandardOutput.ReadLine() == "YES") {
                    return true;
                }
                return false;
            }

            return HasPython(id);
        }

        public static DebugAttach Attach(int pid, int portNum, Guid debugId) {
            bool isTarget64Bit = Is64BitProcess(pid);
            bool isAttacher64Bit = Environment.Is64BitProcess;

            if (isAttacher64Bit != isTarget64Bit) {
                // attaching from 32-bit to 64-bit or vice versa.  We need to start a new process to handle the attach
                // Create the event that will be used for signaling in our local process here, we'll open it in the
                // remote process and dup it there...  The remote process will communicate the error & language
                // version back to us via its exit code.
                IntPtr eventHandle;
                string name;
                do {
                    name = Guid.NewGuid().ToString();
                    eventHandle = CreateEvent(IntPtr.Zero, false, false, name);
                } while (eventHandle == IntPtr.Zero);

                try {
                    string args = String.Format("{0} {1} {2} {3}", pid, portNum, debugId, name);
                    var process = isTarget64Bit ? Create64BitProcess(args) : Create32BitProcess(args);

                    var attachDoneEvent = AutoResetEvent.OpenExisting(name);

                    process.WaitForExit();
                    return new DebugAttach(attachDoneEvent, (ConnErrorMessages)(process.ExitCode & 0xffff), process.ExitCode >> 16);
                } finally {
                    CloseHandle(eventHandle);
                }
            }

            return AttachWorker(pid, portNum, debugId);
        }

        /// <summary>
        /// Gets the event which will be signaled when the attach is complete.
        /// </summary>
        public EventWaitHandle AttachDone {
            get {
                return _attachDone;
            }
        }

        /// <summary>
        /// Gets the error status if an error occured during attach (or None if no error occured).
        /// </summary>
        public ConnErrorMessages Error {
            get {
                return _error;
            }
        }

        /// <summary>
        /// 16-bit language version, minor version in low byte, major version in high byte.  
        /// 
        /// Returned as an int but upper 16 bits are unused.
        /// </summary>
        public int LanguageVersion {
            get {
                return _langVersion;
            }
        }

        private static void Help() {
            Console.WriteLine("Expected: (TargetPID PortNum EventName) | CHECK");
        }

        internal static int RunCheck() {
            for (; ; ) {
                string process = Console.ReadLine();
                if (process == null || process == "END") {
                    return 0;
                }

                int processId;
                if (!Int32.TryParse(process, out processId)) {
                    return -1;
                }

                if (HasPython(processId)) {
                    Console.WriteLine("YES");
                } else {
                    Console.WriteLine("NO");
                }
            }
        }

        private static bool HasPython(int processId) {
            // calling CreateToolhelp32Snapshot directly is significantly (~5x) faster than accessing 
            // Process.Modules.  This gets called during the Debug->attach to process API so that ends
            // up being a difference of less than 1 sec vs. 5 seconds.
            IntPtr h = CreateToolhelp32Snapshot(SnapshotFlags.Module, (uint)processId);
            if (h != INVALID_HANDLE_VALUE) {
                uint marshalSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                MODULEENTRY32 me = new MODULEENTRY32();
                me.dwSize = (uint)marshalSize;
                if (Module32First(h, ref me)) {
                    do {
                        if (IsPythonModule(me.szModule)) {
                            return true;
                        }

                        me.dwSize = marshalSize;
                    } while (Module32Next(h, ref me));
                }
                CloseHandle(h);
            }
            return false;
        }

        private static bool IsPythonModule(string filename) {
            foreach (string modName in _pythonMods) {
                if (filename.EndsWith(modName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Launches our 64-bit executable w/ the specifed arguments.  We then use the process for performing
        /// the attach or detecting if a process is attachable (depending on the arguments).
        /// </summary>
        private static Process Create64BitProcess(string args) {
            Debug.Assert(Environment.Is64BitOperatingSystem);

            return CreateProcess(args, "Microsoft.PythonTools.Attacher.exe");
        }

        /// <summary>
        /// Launches our 32-bit executable w/ the specifed arguments.  We then use the process for performing
        /// the attach or detecting if a process is attachable (depending on the arguments).
        /// </summary>
        private static Process Create32BitProcess(string args) {
            Debug.Assert(Environment.Is64BitOperatingSystem);

            return CreateProcess(args, "Microsoft.PythonTools.AttacherX86.exe");
        }

        private static Process CreateProcess(string args, string exeName) {
            string basePath = GetPythonToolsInstallPath();
            if (string.IsNullOrEmpty(basePath)) {
                return null;
            }

            return ConfigureAndStartProcess(new ProcessStartInfo(Path.Combine(basePath, exeName), args));
        }

        private static Process ConfigureAndStartProcess(ProcessStartInfo psi) {
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = psi.RedirectStandardInput = psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            return Process.Start(psi);
        }

        /// <summary>
        /// Attaches to the specified PID and returns a DebugAttach object indicating the result.
        /// </summary>
        internal static DebugAttach AttachWorker(int pid, int portNum, Guid debugId, EventWaitHandle attachDoneEvent = null) {
            var hProcess = OpenProcess(ProcessAccessFlags.All, false, pid);
            if (hProcess != IntPtr.Zero) {
                string basePath = GetPythonToolsInstallPath();
                string dll;
                if (IntPtr.Size == 4) {
                    dll = "PyDebugAttachX86.dll";
                } else {
                    dll = "PyDebugAttach.dll";
                }

                string dllPath;
                if (string.IsNullOrEmpty(basePath) || !File.Exists(dllPath = Path.Combine(basePath, dll))) {
                    return new DebugAttach(ConnErrorMessages.PyDebugAttachNotFound);
                }

                // load our code into the process...
                
                // http://msdn.microsoft.com/en-us/library/windows/desktop/ms682631(v=vs.85).aspx
                // If the module list in the target process is corrupted or not yet initialized, 
                // or if the module list changes during the function call as a result of DLLs 
                // being loaded or unloaded, EnumProcessModules may fail or return incorrect 
                // information.
                // So we'll retry a handful of times to get the attach...
                ConnErrorMessages? error = null;
                for (int i = 0; i < 5; i++) {
                    IntPtr kernelMod;
                    if ((error = TryAttach(hProcess, out kernelMod)) == null) {
                        return InjectDebugger(dllPath, hProcess, kernelMod, portNum, pid, debugId, attachDoneEvent);
                    }
                }

                return new DebugAttach(error.Value);
            }

            return new DebugAttach(ConnErrorMessages.CannotOpenProcess);
        }

        private static ConnErrorMessages? TryAttach(IntPtr hProcess, out IntPtr kernelMod) {
            kernelMod = IntPtr.Zero;
            int modSize = IntPtr.Size * 1024;

            IntPtr hMods = Marshal.AllocHGlobal(modSize);
            if (hMods == IntPtr.Zero) {
                return ConnErrorMessages.OutOfMemory;
            }
            try {
                int modsNeeded;
                bool gotZeroMods = false;
                while (!EnumProcessModules(hProcess, hMods, modSize, out modsNeeded) || modsNeeded == 0) {
                    if (modsNeeded == 0) {
                        if (!gotZeroMods) {
                            // Give the process a chance to get into a sane state...
                            Thread.Sleep(100);
                            gotZeroMods = true;
                            continue;
                        } else {
                            // process has exited?
                            return ConnErrorMessages.CannotOpenProcess;
                        }
                    }
                    // try again w/ more space...
                    Marshal.FreeHGlobal(hMods);
                    hMods = Marshal.AllocHGlobal(modsNeeded);
                    if (hMods == IntPtr.Zero) {
                        return ConnErrorMessages.OutOfMemory;
                    }
                    modSize = modsNeeded;
                }

                for (int i = 0; i < modsNeeded / IntPtr.Size; i++) {
                    StringBuilder modName = new StringBuilder();
                    modName.Capacity = MAX_PATH;
                    var curMod = Marshal.ReadIntPtr(hMods, i * IntPtr.Size);

                    if (GetModuleBaseName(hProcess, curMod, modName, MAX_PATH) != 0) {
                        if (String.Equals(modName.ToString(), "kernel32.dll", StringComparison.OrdinalIgnoreCase)) {
                            // kernel module, we want to use this to inject ourselves
                            kernelMod = curMod;
                            return null;
                        }
                    }
                }
            } finally {
                if (hMods != IntPtr.Zero) {
                    Marshal.FreeHGlobal(hMods);
                }
            }
            return ConnErrorMessages.CannotInjectThread;
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        private static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "PyDebugAttach.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\2.0");
                    if (File.Exists(Path.Combine(toolsPath, "PyDebugAttach.dll"))) {
                        return toolsPath;
                    }
                }
            }

            Debug.Assert(false, "Unable to determine Python Tools installation path");
            return string.Empty;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
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

        private static DebugAttach InjectDebugger(string dllPath, IntPtr hProcess, IntPtr curMod, int portNum, int pid, Guid debugId, EventWaitHandle attachDoneEvent) {
            // create our our shared memory region so that we can read the port number from the other side and we can indicate when
            // the attach has completed
            try {
                using (MemoryMappedFile sharedMemoryComm = MemoryMappedFile.CreateNew("PythonDebuggerMemory" + pid, 1000, MemoryMappedFileAccess.ReadWrite)) {
                    using (var viewStream = sharedMemoryComm.CreateViewStream()) {
                        // write the information the process needs to communicate with us.  This includes:
                        //      the port number it should connect to for communicating with us
                        //      two auto reset events.  
                        //          The first signals with the attach is ready to start (the port is open)
                        //          The second signals when the attach is completely finished
                        //      space for reporting back an error and the version of the Python interpreter attached to.

                        viewStream.Write(BitConverter.GetBytes(portNum), 0, 4);
                        viewStream.WriteByte(0);
                        viewStream.WriteByte(0);
                        viewStream.WriteByte(0);
                        viewStream.WriteByte(0);

                        // write a handle for shared process communication
                        AutoResetEvent attachStarting = new AutoResetEvent(false);
                        EventWaitHandle attachDone = attachDoneEvent ?? new AutoResetEvent(false);
                        try {
#pragma warning disable 618
                            IntPtr attachStartingSourceHandle = attachStarting.Handle, attachDoneSourceHandle = attachDone.Handle;
#pragma warning restore 618
                            IntPtr attachStartingTargetHandle, attachDoneTargetHandle;

                            bool res = DuplicateHandle(GetCurrentProcess(), attachStartingSourceHandle, hProcess, out attachStartingTargetHandle, 0, false, (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS);
                            if (!res) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                            res = DuplicateHandle(GetCurrentProcess(), attachDoneSourceHandle, hProcess, out attachDoneTargetHandle, 0, false, (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS);
                            if (!res) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }

                            viewStream.Write(BitConverter.GetBytes(attachStartingTargetHandle.ToInt64()), 0, 8);
                            viewStream.Write(BitConverter.GetBytes(attachDoneTargetHandle.ToInt64()), 0, 8);

                            var errorCodePosition = viewStream.Position;

                            viewStream.Write(new byte[8], 0, 8); // write null bytes for error code and version
                            string guid = debugId.ToString();
                            for (int i = 0; i < guid.Length; i++) {
                                viewStream.WriteByte((byte)guid[i]);
                            }

                            var ourLibName = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPath.Length, AllocationType.Commit, MemoryProtection.ReadWrite);
                            if (ourLibName == IntPtr.Zero) {
                                return new DebugAttach(ConnErrorMessages.OutOfMemory);
                            }

                            int bytesWritten;
                            if (!WriteProcessMemory(hProcess, ourLibName, dllPath, dllPath.Length * 2, out bytesWritten)) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }

                            var hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, GetProcAddress(curMod, "LoadLibraryW"), ourLibName, 0, IntPtr.Zero);
                            if (hThread == IntPtr.Zero) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }

                            viewStream.Position = errorCodePosition;
                            byte[] buffer = new byte[4];

                            if (!attachStarting.WaitOne(10000)) {
                                viewStream.Read(buffer, 0, 4);
                                var errorMsg = (ConnErrorMessages)BitConverter.ToInt32(buffer, 0);
                                return new DebugAttach(errorMsg != ConnErrorMessages.None ? errorMsg : ConnErrorMessages.TimeOut);
                            }

                            viewStream.Read(buffer, 0, 4);

                            byte[] versionBuffer = new byte[4];
                            viewStream.Read(versionBuffer, 0, 4);
                            int version = BitConverter.ToInt32(versionBuffer, 0);

                            return new DebugAttach(attachDone, (ConnErrorMessages)BitConverter.ToInt32(buffer, 0), version);
                        } finally {
                            attachStarting.Close();
                        }
                    }
                }
            } catch (IOException) {
                return new DebugAttach(ConnErrorMessages.CannotOpenProcess);
            }
        }

        private static bool Is64BitProcess(int pid) {
            if (!Environment.Is64BitOperatingSystem) {
                return false;
            }

            var hProcess = OpenProcess(ProcessAccessFlags.All, false, pid);
            try {
                if (hProcess != IntPtr.Zero) {
                    EnsureIsWow64();

                    bool res;
                    if (_isWow64 != null && _isWow64(hProcess, out res)) {
                        return !res;
                    }
                }
                return false;
            } finally {
                if (hProcess != IntPtr.Zero) {
                    CloseHandle(hProcess);
                }
            }
        }

        private static void EnsureIsWow64() {
            if (_isWow64 == null && !_checkedIsWow64) {
                _checkedIsWow64 = true;
                IntPtr kernel = LoadLibrary("kernel32.dll");
                if (kernel != IntPtr.Zero) {

                    var isWowProc = GetProcAddress(kernel, "IsWow64Process");
                    if (isWowProc != IntPtr.Zero) {
                        _isWow64 = (IsWow64Process)Marshal.GetDelegateForFunctionPointer(isWowProc, typeof(IsWow64Process));
                    }
                }
            }
        }

        #region Win32 APIs

        // IsWow64Process is only available on Vista and up, so we access it via a delegate.
        private delegate bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);

        // both kernel32 and psapi are in the known dlls list so all of these P/Invokes are safe
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess,
           IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [MarshalAs(UnmanagedType.LPWStr)]string lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr lphModule, int cb, [MarshalAs(UnmanagedType.U4)] out int lpcbNeeded);

        [DllImport("psapi.dll")]
        private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [Out]StringBuilder lpBaseName, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [Flags]
        private enum DuplicateOptions : uint {
            DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
            DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
        }

        [Flags]
        public enum AllocationType {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        [Flags]
        enum ProcessAccessFlags : uint {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        [Flags]
        private enum SnapshotFlags : uint {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            Inherit = 0x80000000,
            All = 0x0000001F
        }

        struct MODULEENTRY32 {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_MODULE_NAME32 + 1)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szExePath;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            if (_attachDone != null) {
                _attachDone.Dispose();
            }
        }

        #endregion
    }
}
