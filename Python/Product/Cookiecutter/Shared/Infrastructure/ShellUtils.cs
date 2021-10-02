// Visual Studio Shared Project
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
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Microsoft.CookiecutterTools.Infrastructure {
    public static class ShellUtils {
        public static void DeleteDirectory(string path) {
            NativeMethods.RecursivelyDeleteDirectory(path, silent: true);
        }

        /// <summary>
        /// Unmanaged API wrappers.
        /// </summary>
        static class NativeMethods {
            /// <summary>
            /// Recursively deletes a directory using the shell API which can 
            /// handle long file names
            /// </summary>
            /// <param name="dir"></param>
            public static void RecursivelyDeleteDirectory(string dir, bool silent = false) {
                SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT();
                fileOp.pFrom = dir + '\0';  // pFrom must be double null terminated
                fileOp.wFunc = FO_Func.FO_DELETE;
                fileOp.fFlags = FILEOP_FLAGS_ENUM.FOF_NOCONFIRMATION |
                    FILEOP_FLAGS_ENUM.FOF_NOERRORUI;
                if (silent) {
                    fileOp.fFlags |= FILEOP_FLAGS_ENUM.FOF_SILENT;
                }
                int res = SHFileOperation(ref fileOp);
                if (res != 0) {
                    throw new IOException("Failed to delete dir " + res);
                }
            }

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            static extern int SHFileOperation([In, Out] ref SHFILEOPSTRUCT lpFileOp);


            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            struct SHFILEOPSTRUCT {
                public IntPtr hwnd;
                public FO_Func wFunc;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pFrom;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string pTo;
                public FILEOP_FLAGS_ENUM fFlags;
                [MarshalAs(UnmanagedType.Bool)]
                public bool fAnyOperationsAborted;
                public IntPtr hNameMappings;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string lpszProgressTitle;

            }

            [Flags]
            private enum FILEOP_FLAGS_ENUM : ushort {
                FOF_MULTIDESTFILES = 0x0001,
                FOF_CONFIRMMOUSE = 0x0002,
                FOF_SILENT = 0x0004,  // don't create progress/report
                FOF_RENAMEONCOLLISION = 0x0008,
                FOF_NOCONFIRMATION = 0x0010,  // Don't prompt the user.
                FOF_WANTMAPPINGHANDLE = 0x0020,  // Fill in SHFILEOPSTRUCT.hNameMappings
                                                 // Must be freed using SHFreeNameMappings
                FOF_ALLOWUNDO = 0x0040,
                FOF_FILESONLY = 0x0080,  // on *.*, do only files
                FOF_SIMPLEPROGRESS = 0x0100,  // means don't show names of files
                FOF_NOCONFIRMMKDIR = 0x0200,  // don't confirm making any needed dirs
                FOF_NOERRORUI = 0x0400,  // don't put up error UI
                FOF_NOCOPYSECURITYATTRIBS = 0x0800,  // dont copy NT file Security Attributes
                FOF_NORECURSION = 0x1000,  // don't recurse into directories.
                FOF_NO_CONNECTED_ELEMENTS = 0x2000,  // don't operate on connected elements.
                FOF_WANTNUKEWARNING = 0x4000,  // during delete operation, warn if deleting instead of recycling (partially overrides FOF_NOCONFIRMATION)
                FOF_NORECURSEREPARSE = 0x8000,  // treat reparse points as objects, not containers
            }

            public enum FO_Func : uint {
                FO_MOVE = 0x0001,
                FO_COPY = 0x0002,
                FO_DELETE = 0x0003,
                FO_RENAME = 0x0004,
            }

            public const int MAX_PATH = 260;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);

            /// <summary>
            /// Use caution when using this directly as the errors it gives when you are not running as elevated are not
            //  very good.  Please use the Wrapper method.
            /// </summary>
            /// <param name="lpSymlinkFileName">Path to the symbolic link you wish to create.</param>
            /// <param name="lpTargetFileName">Path to the file/directory that the symbolic link should be pointing to.</param>
            /// <param name="dwFlags">Flag specifying either file or directory.</param>
            /// <returns></returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

            internal enum SymbolicLink {
                File = 0,
                Directory = 1
            }

            /// <summary>
            /// Wrapper for extern method CreateSymbolicLink.  This handles some error cases and provides better information
            /// in error cases.
            /// </summary>
            /// <param name="symlinkPath">Path to the symbolic link you wish to create.</param>
            /// <param name="targetPath">Path to the file/directory that the symbolic link should be pointing to.</param>
            public static void CreateSymbolicLink(string symlinkPath, string targetPath) {
                // Pre-checks.
                if (!(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))) {
                    throw new UnauthorizedAccessException("Process must be run in elevated permissions in order to create symbolic link.");
                } else if (Directory.Exists(symlinkPath) || File.Exists(symlinkPath)) {
                    throw new IOException("Path Already Exists.  We cannot create a symbolic link here");
                }

                // Create the correct symbolic link.
                bool result;
                if (File.Exists(targetPath)) {
                    result = CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.File);
                } else if (Directory.Exists(targetPath)) {
                    result = CreateSymbolicLink(symlinkPath, targetPath, NativeMethods.SymbolicLink.Directory);
                } else {
                    throw new FileNotFoundException("Target File/Directory was not found.  Cannot make a symbolic link.");
                }

                // Validate that we created a symbolic link.
                // If we failed and the symlink doesn't exist throw an exception here.
                if (!result) {
                    if (!Directory.Exists(symlinkPath) && !File.Exists(symlinkPath)) {
                        throw new FileNotFoundException("Unable to find symbolic link after creation.");
                    } else {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                }
            }
        }
    }
}
