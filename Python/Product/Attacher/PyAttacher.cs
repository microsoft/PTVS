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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
        private static readonly Regex _pythonModRegex = new Regex(
            @".*python(2[5-7]|3[0-4])(_d)?\.dll$",
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant
        );

        private DebugAttach(ConnErrorMessages error) {
            _error = error;
        }

        private DebugAttach(EventWaitHandle attachDone, ConnErrorMessages error, int langVersion) {
            _attachDone = attachDone;
            _error = error;
            _langVersion = langVersion;
        }

        public static int Main(string[] args) {
            if (args.Length < 1) {
                return Help();
            }

            switch (args[0]) {
                case "CHECK":
                    if (args.Length != 1) {
                        return Help();
                    }
                    return RunCheck();

                case "ATTACH_AD7": {
                        int pid, portNum;
                        Guid debugId;
                        if (args.Length != 6 ||
                            !Int32.TryParse(args[1], out pid) ||
                            !Int32.TryParse(args[2], out portNum) ||
                            !Guid.TryParse(args[3], out debugId)
                        ) {
                            return Help();
                        }

                        string debugOptions = args[4];

                        EventWaitHandle doneEvent;
                        try {
                            doneEvent = AutoResetEvent.OpenExisting(args[5]);
                        } catch {
                            Console.Error.WriteLine("Could not open event " + args[5]);
                            return Help(-2);
                        }

                        var res = AttachAD7Worker(pid, portNum, debugId, debugOptions, doneEvent);
                        return ((int)res._error) | (res._langVersion << 16);
                    };

                case "ATTACH_DKM": {
                        int pid;
                        if (args.Length != 2 || !Int32.TryParse(args[1], out pid)) {
                            return Help();
                        }

                        return (int)AttachDkmWorker(pid);
                    };

                default:
                    return Help();
            }
        }

        public static bool IsPythonProcess(int id) {
            bool isTarget64Bit = NativeMethods.Is64BitProcess(id);
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

        public static DebugAttach AttachAD7(int pid, int portNum, Guid debugId, string debugOptions) {
            bool isTarget64Bit = NativeMethods.Is64BitProcess(pid);
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
                    eventHandle = NativeMethods.CreateEvent(IntPtr.Zero, false, false, name);
                } while (eventHandle == IntPtr.Zero);

                try {
                    string args = String.Format("ATTACH_AD7 {0} {1} {2} \"{3}\" {4}", pid, portNum, debugId, debugOptions, name);
                    var process = isTarget64Bit ? Create64BitProcess(args) : Create32BitProcess(args);

                    var attachDoneEvent = AutoResetEvent.OpenExisting(name);

                    process.WaitForExit();
                    return new DebugAttach(attachDoneEvent, (ConnErrorMessages)(process.ExitCode & 0xffff), process.ExitCode >> 16);
                } finally {
                    NativeMethods.CloseHandle(eventHandle);
                }
            }

            return AttachAD7Worker(pid, portNum, debugId, debugOptions);
        }

        public static ConnErrorMessages AttachDkm(int pid) {
            bool isTarget64Bit = NativeMethods.Is64BitProcess(pid);
            bool isAttacher64Bit = Environment.Is64BitProcess;
            if (isAttacher64Bit == isTarget64Bit) {
                return AttachDkmWorker(pid);
            } else {
                string args = String.Format("ATTACH_DKM {0}", pid);
                var process = isTarget64Bit ? Create64BitProcess(args) : Create32BitProcess(args);
                process.WaitForExit();
                return (ConnErrorMessages)(process.ExitCode & 0xffff);
            }
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

        private static int Help(int exitCode = -1) {
            Console.Error.WriteLine("Usage: {0} {{CHECK | ATTACH_AD7 | ATTACH_DKM}} [<parameters>]", Assembly.GetEntryAssembly().ManifestModule.Name);
            Console.Error.WriteLine("Parameters for:");
            Console.Error.WriteLine("\tATTACH_AD7:\t<target pid> <port num> <debug id> <debug options> <event name>");
            Console.Error.WriteLine("\tATTACH_DKM:\t<target pid>");
            return exitCode;
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
            IntPtr h = NativeMethods.CreateToolhelp32Snapshot(SnapshotFlags.Module, (uint)processId);
            if (h != NativeMethods.INVALID_HANDLE_VALUE) {
                uint marshalSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                var me = new MODULEENTRY32();
                me.dwSize = (uint)marshalSize;
                if (NativeMethods.Module32First(h, ref me)) {
                    do {
                        if (IsPythonModule(me.szModule)) {
                            return true;
                        }

                        me.dwSize = marshalSize;
                    } while (NativeMethods.Module32Next(h, ref me));
                }
                NativeMethods.CloseHandle(h);
            }
            return false;
        }

        private static bool IsPythonModule(string filename) {
            return _pythonModRegex.IsMatch(filename);
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
            string exePath = PythonToolsInstallPath.GetFile(exeName);
            if (string.IsNullOrEmpty(exePath)) {
                return null;
            }

            return ConfigureAndStartProcess(new ProcessStartInfo(exePath, args));
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
        internal static DebugAttach AttachAD7Worker(int pid, int portNum, Guid debugId, string debugOptions, EventWaitHandle attachDoneEvent = null) {
            var hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.All, false, pid);
            if (hProcess != IntPtr.Zero) {
                string dllPath;
                if (IntPtr.Size == 4) {
                    dllPath = PythonToolsInstallPath.GetFile("PyDebugAttachX86.dll");
                } else {
                    dllPath = PythonToolsInstallPath.GetFile("PyDebugAttach.dll");
                }

                if (!File.Exists(dllPath)) {
                    return new DebugAttach(ConnErrorMessages.PyDebugAttachNotFound);
                }

                // load our code into the process...
                
                // http://msdn.microsoft.com/en-us/library/windows/desktop/ms682631(v=vs.85).aspx
                // If the module list in the target process is corrupted or not yet initialized, 
                // or if the module list changes during the function call as a result of DLLs 
                // being loaded or unloaded, EnumProcessModules may fail or return incorrect 
                // information.
                // So we'll retry a handful of times to get the attach...
                ConnErrorMessages error = ConnErrorMessages.None;
                for (int i = 0; i < 5; i++) {
                    IntPtr hKernel32;
                    if ((error = FindKernel32(hProcess, out hKernel32)) == ConnErrorMessages.None) {
                        return InjectDebugger(dllPath, hProcess, hKernel32, portNum, pid, debugId, debugOptions, attachDoneEvent);
                    }
                }

                return new DebugAttach(error);
            }

            return new DebugAttach(ConnErrorMessages.CannotOpenProcess);
        }

        internal static ConnErrorMessages AttachDkmWorker(int pid) {
            var hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.All, false, pid);
            if (hProcess == IntPtr.Zero) {
                return ConnErrorMessages.CannotOpenProcess;
            }

            string dllName = string.Format("Microsoft.PythonTools.Debugger.Helper.{0}.dll", IntPtr.Size == 4 ? "x86" : "x64");
            string dllPath = PythonToolsInstallPath.GetFile(dllName);
            if (!File.Exists(dllPath)) {
                return ConnErrorMessages.PyDebugAttachNotFound;
            }

            // http://msdn.microsoft.com/en-us/library/windows/desktop/ms682631(v=vs.85).aspx
            // If the module list in the target process is corrupted or not yet initialized, 
            // or if the module list changes during the function call as a result of DLLs 
            // being loaded or unloaded, EnumProcessModules may fail or return incorrect 
            // information.
            // So we'll retry a handful of times to get the attach...
            ConnErrorMessages error = ConnErrorMessages.None;
            for (int i = 0; i < 5; i++) {
                IntPtr hKernel32;
                if ((error = FindKernel32(hProcess, out hKernel32)) == ConnErrorMessages.None) {
                    return InjectDll(dllPath, hProcess, hKernel32);
                }
            }
            return error;
        }


        private static ConnErrorMessages FindKernel32(IntPtr hProcess, out IntPtr hKernel32) {
            hKernel32 = IntPtr.Zero;
            int modSize = IntPtr.Size * 1024;

            IntPtr hMods = Marshal.AllocHGlobal(modSize);
            if (hMods == IntPtr.Zero) {
                return ConnErrorMessages.OutOfMemory;
            }
            try {
                int modsNeeded;
                bool gotZeroMods = false;
                while (!NativeMethods.EnumProcessModules(hProcess, hMods, modSize, out modsNeeded) || modsNeeded == 0) {
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
                    modName.Capacity = NativeMethods.MAX_PATH;
                    var curMod = Marshal.ReadIntPtr(hMods, i * IntPtr.Size);

                    if (NativeMethods.GetModuleBaseName(hProcess, curMod, modName, NativeMethods.MAX_PATH) != 0) {
                        if (String.Equals(modName.ToString(), "kernel32.dll", StringComparison.OrdinalIgnoreCase)) {
                            // kernel module, we want to use this to inject ourselves
                            hKernel32 = curMod;
                            return ConnErrorMessages.None;
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

        private static ConnErrorMessages InjectDll(string dllPath, IntPtr hProcess, IntPtr hKernel32) {
            var ourLibName = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, new UIntPtr((uint)dllPath.Length), AllocationType.Commit, MemoryProtection.ReadWrite);
            if (ourLibName == IntPtr.Zero) {
                return ConnErrorMessages.OutOfMemory;
            }

            int bytesWritten;
            if (!NativeMethods.WriteProcessMemory(hProcess, ourLibName, dllPath, new IntPtr(dllPath.Length * 2), out bytesWritten)) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var hThread = NativeMethods.CreateRemoteThread(hProcess, IntPtr.Zero, UIntPtr.Zero, NativeMethods.GetProcAddress(hKernel32, "LoadLibraryW"), ourLibName, 0, IntPtr.Zero);
            if (hThread == IntPtr.Zero) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return ConnErrorMessages.None;
        }

        private static DebugAttach InjectDebugger(string dllPath, IntPtr hProcess, IntPtr hKernel32, int portNum, int pid, Guid debugId, string debugOptions, EventWaitHandle attachDoneEvent) {
            // create our our shared memory region so that we can read the port number from the other side and we can indicate when
            // the attach has completed
            try {
                using (MemoryMappedFile sharedMemoryComm = MemoryMappedFile.CreateNew("PythonDebuggerMemory" + pid, 1000, MemoryMappedFileAccess.ReadWrite)) {
                    using (var viewStream = sharedMemoryComm.CreateViewStream()) {
                        // write the information the process needs to communicate with us.  This includes:
                        //      the port number it should connect to for communicating with us
                        //      debug options flags
                        //      two auto reset events.  
                        //          The first signals with the attach is ready to start (the port is open)
                        //          The second signals when the attach is completely finished
                        //      space for reporting back an error and the version of the Python interpreter attached to.

                        viewStream.Write(BitConverter.GetBytes(portNum), 0, 4);
                        viewStream.Write(new byte[4], 0, 4); // padding


                        // write a handle for shared process communication
                        AutoResetEvent attachStarting = new AutoResetEvent(false);
                        EventWaitHandle attachDone = attachDoneEvent ?? new AutoResetEvent(false);
                        try {
#pragma warning disable 618
                            IntPtr attachStartingSourceHandle = attachStarting.Handle, attachDoneSourceHandle = attachDone.Handle;
#pragma warning restore 618
                            IntPtr attachStartingTargetHandle, attachDoneTargetHandle;

                            bool res = NativeMethods.DuplicateHandle(NativeMethods.GetCurrentProcess(), attachStartingSourceHandle, hProcess, out attachStartingTargetHandle, 0, false, (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS);
                            if (!res) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                            res = NativeMethods.DuplicateHandle(NativeMethods.GetCurrentProcess(), attachDoneSourceHandle, hProcess, out attachDoneTargetHandle, 0, false, (uint)DuplicateOptions.DUPLICATE_SAME_ACCESS);
                            if (!res) {
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }

                            viewStream.Write(BitConverter.GetBytes(attachStartingTargetHandle.ToInt64()), 0, 8);
                            viewStream.Write(BitConverter.GetBytes(attachDoneTargetHandle.ToInt64()), 0, 8);

                            var errorCodePosition = viewStream.Position;
                            viewStream.Write(new byte[8], 0, 8); // write null bytes for error code and version

                            byte[] szDebugId = Encoding.ASCII.GetBytes(debugId.ToString().PadRight(64, '\0'));
                            viewStream.Write(szDebugId, 0, szDebugId.Length);

                            byte[] szDebugOptions = Encoding.ASCII.GetBytes(debugOptions + '\0');
                            viewStream.Write(szDebugOptions, 0, szDebugOptions.Length);

                            var injectDllError = InjectDll(dllPath, hProcess, hKernel32);
                            if (injectDllError != ConnErrorMessages.None) {
                                return new DebugAttach(injectDllError);
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

        #region IDisposable Members

        public void Dispose() {
            if (_attachDone != null) {
                _attachDone.Dispose();
            }
        }

        #endregion
    }
}
