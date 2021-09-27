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

namespace Microsoft.VisualStudioTools.MockVsTests
{
    public static class CommandTargetExtensions
    {
        public static void Type(this IOleCommandTarget target, string text)
        {
            var guid = VSConstants.VSStd2K;
            var variantMem = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VARIANT)));
            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    switch (text[i])
                    {
                        case '\r': target.Enter(); break;
                        case '\t': target.Tab(); break;
                        case '\x08': target.Backspace(); break;
                        default:
                            Marshal.GetNativeVariantForObject((ushort)text[i], variantMem);
                            target.Exec(
                                ref guid,
                                (int)VSConstants.VSStd2KCmdID.TYPECHAR,
                                0,
                                variantMem,
                                IntPtr.Zero
                            );
                            break;
                    }

                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(variantMem);
            }
        }

        public static void Enter(this IOleCommandTarget target)
        {
            var guid = VSConstants.VSStd2K;
            target.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.RETURN, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public static void Tab(this IOleCommandTarget target)
        {
            var guid = VSConstants.VSStd2K;
            target.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.TAB, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public static void Backspace(this IOleCommandTarget target)
        {
            var guid = VSConstants.VSStd2K;
            target.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.BACKSPACE, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public static void MemberList(this IOleCommandTarget target)
        {
            var guid = VSConstants.VSStd2K;
            ErrorHandler.ThrowOnFailure(target.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.SHOWMEMBERLIST, 0, IntPtr.Zero, IntPtr.Zero));
        }

        public static void ParamInfo(this IOleCommandTarget target)
        {
            var guid = VSConstants.VSStd2K;
            ErrorHandler.ThrowOnFailure(target.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.PARAMINFO, 0, IntPtr.Zero, IntPtr.Zero));
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct VARIANT
        {
            [FieldOffset(0)]
            public ushort vt;
            [FieldOffset(8)]
            public IntPtr pdispVal;
            [FieldOffset(8)]
            public byte ui1;
            [FieldOffset(8)]
            public ushort ui2;
            [FieldOffset(8)]
            public uint ui4;
            [FieldOffset(8)]
            public ulong ui8;
            [FieldOffset(8)]
            public float r4;
            [FieldOffset(8)]
            public double r8;
        }

    }
}
