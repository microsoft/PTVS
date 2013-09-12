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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace CanopyInterpreter {
    internal static class NativeMethods {
        [DllImport("kernel32", EntryPoint = "GetBinaryTypeW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        private static extern bool _GetBinaryType(string lpApplicationName, out GetBinaryTypeResult lpBinaryType);

        private enum GetBinaryTypeResult : uint {
            SCS_32BIT_BINARY = 0,
            SCS_DOS_BINARY = 1,
            SCS_WOW_BINARY = 2,
            SCS_PIF_BINARY = 3,
            SCS_POSIX_BINARY = 4,
            SCS_OS216_BINARY = 5,
            SCS_64BIT_BINARY = 6
        }

        public static ProcessorArchitecture GetBinaryType(string path) {
            GetBinaryTypeResult result;

            if (_GetBinaryType(path, out result)) {
                switch (result) {
                    case GetBinaryTypeResult.SCS_32BIT_BINARY:
                        return ProcessorArchitecture.X86;
                    case GetBinaryTypeResult.SCS_64BIT_BINARY:
                        return ProcessorArchitecture.Amd64;
                    case GetBinaryTypeResult.SCS_DOS_BINARY:
                    case GetBinaryTypeResult.SCS_WOW_BINARY:
                    case GetBinaryTypeResult.SCS_PIF_BINARY:
                    case GetBinaryTypeResult.SCS_POSIX_BINARY:
                    case GetBinaryTypeResult.SCS_OS216_BINARY:
                    default:
                        break;
                }
            }

            return ProcessorArchitecture.None;
        }

        public const int MAXIMUM_WAIT_OBJECTS = 64;

        [DllImport("advapi32", EntryPoint = "RegNotifyChangeKeyValue", CallingConvention = CallingConvention.Winapi)]
        private static extern int _RegNotifyChangeKeyValue(
            SafeHandle hKey,
            bool bWatchSubtree,
            RegNotifyChange dwNotifyFilter,
            SafeHandle hEvent,
            bool fAsynchronous
        );

        [Flags]
        public enum RegNotifyChange : uint {
            Name = 1,                   // REG_NOTIFY_CHANGE_NAME
            Attributes = 2,             // REG_NOTIFY_CHANGE_ATTRIBUTES
            Value = 4,                  // REG_NOTIFY_CHANGE_LAST_SET
            Security = 8,               // REG_NOTIFY_CHANGE_SECURITY
            ThreadAgnostic = 0x10000000 // REG_NOTIFY_THREAD_AGNOSTIC (Windows 8 only)
        }

        public static void RegNotifyChangeKeyValue(
            RegistryKey key,
            WaitHandle notifyEvent,
            bool recursive = false,
            RegNotifyChange filter = RegNotifyChange.Value
        ) {
            int error = _RegNotifyChangeKeyValue(
                key.Handle,
                recursive,
                filter,
                notifyEvent.SafeWaitHandle,
                true);

            if (error != 0) {
                throw new Win32Exception(error);
            }
        }
    }
}
