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

namespace Microsoft.CookiecutterTools.Infrastructure {
    static partial class NativeMethods {
        public const int OLECMDERR_E_NOTSUPPORTED = unchecked((int)0x80040100);
        public const int OLECMDERR_E_CANCELED = -2147221245;
        public const int OLECMDERR_E_UNKNOWNGROUP = unchecked((int)0x80040104);

#pragma warning disable 0649
        struct _OLECMDTEXT {
            public uint cmdtextf;
            public uint cwActual;
            public uint cwBuf;
            public IntPtr rgwz;
        }
#pragma warning restore 0649

        public struct OLECMDTEXT {
            private readonly IntPtr _ptr;
            private _OLECMDTEXT _value;

            public OLECMDTEXT(IntPtr ptr) {
                _ptr = ptr;
                if (_ptr != IntPtr.Zero) {
                    _value = Marshal.PtrToStructure<_OLECMDTEXT>(_ptr);
                } else {
                    _value = default(_OLECMDTEXT);
                }
            }

            private void Write() {
                Marshal.PtrToStructure(_ptr, _value);
            }

            public string Text {
                get {
                    if (_ptr == IntPtr.Zero || _value.rgwz == IntPtr.Zero) {
                        return null;
                    }
                    return Marshal.PtrToStringUni(_value.rgwz, (int)_value.cwActual - 1);
                }
                set {
                    if (_ptr == IntPtr.Zero || _value.rgwz == IntPtr.Zero) {
                        if (!string.IsNullOrEmpty(value)) {
                            throw new ArgumentException("Can only set Text when it is initially set");
                        }
                        return;
                    }

                    char[] menuText = (value + "\0").ToCharArray();

                    int maxChars = Math.Min((int)_value.cwBuf, menuText.Length);

                    Marshal.Copy(menuText, 0, _value.rgwz, maxChars);
                    _value.cwActual = (uint)maxChars;
                    Write();
                }
            }

            public bool IsName {
                get { return _ptr != IntPtr.Zero && (_value.cmdtextf & (uint)OLECMDTEXTF.OLECMDTEXTF_NAME) != 0; }
            }

            public bool IsStatus {
                get { return _ptr != IntPtr.Zero && (_value.cmdtextf & (uint)OLECMDTEXTF.OLECMDTEXTF_STATUS) != 0; }
            }

            enum OLECMDTEXTF {
                /// <summary>No flag</summary>
                OLECMDTEXTF_NONE = 0,
                /// <summary>The name of the command is required.</summary>
                OLECMDTEXTF_NAME = 1,
                /// <summary>A description of the status is required.</summary>
                OLECMDTEXTF_STATUS = 2
            }
        }
    }
}
