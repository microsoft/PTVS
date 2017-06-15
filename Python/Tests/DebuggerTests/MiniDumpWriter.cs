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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using TestUtilities;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DebuggerTests {
    /// <summary>
    /// Writes a crash dump of the designated process on dispose, unless canceled.
    /// </summary>
    internal sealed class MiniDumpWriter : IDisposable {
        // Full memory dumps are 3 orders of magnitude larger than stack dumps, so
        // they're not enabled by default, but can be switched on for debugging.
        private const MINIDUMP_TYPE minidumpType =
            MINIDUMP_TYPE.MiniDumpNormal;
            //MINIDUMP_TYPE.MiniDumpWithFullMemory;

        private Process _process;

        public MiniDumpWriter(Process process) {
            _process = process;
        }

        public void Cancel() {
            _process = null;
        }

        public void Dispose() {
            if (_process == null || _process.HasExited) {
                return;
            }

            IntPtr handle;
            uint id;
            try {
                handle = _process.Handle;
                id = (uint)_process.Id;
            } catch (InvalidOperationException) {
                return;
            }

            var dumpDir = TestData.GetTempPath(randomSubPath: true);
            var dumpPath = Path.Combine(dumpDir, "dump.dmp");
            Console.WriteLine("Writing minidump to {0}", dumpPath);

            using (var dump = new FileStream(dumpPath, FileMode.Create, FileAccess.Write)) {
                MiniDumpWriteDump(handle, id, dump.SafeFileHandle, minidumpType,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
        }

        [Flags]
        private enum MINIDUMP_TYPE {
            MiniDumpFilterMemory = 8,
            MiniDumpFilterModulePaths = 0x80,
            MiniDumpNormal = 0,
            MiniDumpScanMemory = 0x10,
            MiniDumpWithCodeSegs = 0x2000,
            MiniDumpWithDataSegs = 1,
            MiniDumpWithFullMemory = 2,
            MiniDumpWithFullMemoryInfo = 0x800,
            MiniDumpWithHandleData = 4,
            MiniDumpWithIndirectlyReferencedMemory = 0x40,
            MiniDumpWithoutManagedState = 0x4000,
            MiniDumpWithoutOptionalData = 0x400,
            MiniDumpWithPrivateReadWriteMemory = 0x200,
            MiniDumpWithProcessThreadData = 0x100,
            MiniDumpWithThreadInfo = 0x1000,
            MiniDumpWithUnloadedModules = 0x20
        }

        [DllImport("dbghelp.dll")]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, SafeFileHandle hFile, MINIDUMP_TYPE DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);
    }
}
