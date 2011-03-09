/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.TC.TestHostAdapters
{
    using DWORD = System.UInt32;
    using LONG = System.Int32;
    using Microsoft.VisualStudio.OLE.Interop;

    /// <summary>
    /// Unmanaged API wrappers.
    /// </summary>
    internal static class NativeMethods
    {
        #region Constants
        public const int INVALID_HANDLE_VALUE = -1;
        public const int TH32CS_SNAPPROCESS = 0x2;
        public const int PROCESS_QUERY_INFORMATION = 0x400;
        public const int MAX_PATH = 260;
        public const int ERROR_INVALID_PARAMETER = 87;    // winerror.h
        private const string KERNEL32 = "kernel32.dll";
        #endregion

        #region Types
        /// <summary>
        /// The structure used for Process32First/Next.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable")]   // Not native resources to manage.
        [Serializable]
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PROCESSENTRY32
        {
            public DWORD dwSize;                // DWORD
            public DWORD cntUsage;              // DWORD
            public DWORD th32ProcessID;         // DWORD
            [SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
            public IntPtr th32DefaultHeapID;    // ULONG_PTR
            public DWORD th32ModuleID;          // DWORD
            public DWORD cntThreads;            // DWORD
            public DWORD th32ParentProcessID;   // DWORD
            public LONG pcPriClassBase;         // LONG. This HAS to be managed 'int', not 'long' otherwise size mismatch as sizeof(LONG) = 4.
            public DWORD dwFlags;               // DWORD
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szExeFile;            // TSTR.
        }
        #endregion

        #region COM
        /// <summary>
        /// Get the ROT.
        /// </summary>
        /// <param name="reserved">Reserved.</param>
        /// <param name="prot">Pointer to running object table interface</param>
        /// <returns></returns>
        [DllImport("ole32.dll")]
        internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        /// <summary>
        /// Create a Bind context.
        /// </summary>
        /// <param name="reserved">Reserved.</param>
        /// <param name="ppbc">Bind context.</param>
        /// <returns>HRESULT</returns>
        [DllImport("ole32.dll")]
        internal static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        /// <summary>
        /// Register message filter for COM.
        /// </summary>
        /// <param name="lpMessageFilter">New filter to register.</param>
        /// <param name="lplpMessageFilter">Old filter. Save it if you need to restore it later.</param>
        [DllImport("ole32.dll")]
        internal static extern int CoRegisterMessageFilter(IMessageFilter lpMessageFilter, out IMessageFilter lplpMessageFilter);
        #endregion
    }
}
