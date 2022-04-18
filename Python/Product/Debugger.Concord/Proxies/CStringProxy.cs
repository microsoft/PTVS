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
using System.Diagnostics;
using System.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies {
    /// <summary>
    /// A data proxy for a C-style null-terminated string.
    /// </summary>
    [DebuggerDisplay("& {Read()}")]
    internal struct CStringProxy : IDataProxy {
        private static readonly Encoding _latin1 = Encoding.GetEncoding(28591);

        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public CStringProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return ReadBytes().Length; }
        }

        private Encoding Encoding {
            get {
                // Python 3.x consistently treats char* strings (T_STRING, tp_name etc) as UTF-8, so that's what we do here as well.
                //
                // In Python 2.x, the story is complicated, since char* is just mapped to non-Unicode Python strings, without any
                // implication of what encoding it may be in. If user then converts this string to Unicode, sys.getdefaultencoding()
                // comes into the picture (and this should normally always be ASCII); but such a conversion has to be explicit, and
                // more often than not never happens. It's fairly common for a native extension to return a string that's encoded in
                // the MBCS for the current locale, and for Python code receiving that string to just pass it through, e.g. printing
                // it to stdout, which also uses locale encoding - or, in general, to treat it as an opaque, 8-bit-clean non-Unicode
                // string. So treating all strings as ASCII or UTF-8 would be incorrect in most cases.
                //
                // So the best thing we can do here for 2.x is to preserve the 8-bit code points as is (which is what encoding using
                // Latin-1 will do, since all 8-bit characters map to exact same code points in Unicode). Then, when we display this
                // string to the user using ReplBuilder, we will use \x## escapes for the higher 128 chars, which will result in a
                // properly round-tripped, encoding-agnostic, and therefore guaranteed correct - if not always the most readable -
                // representation of the string.
                return (Process.GetPythonRuntimeInfo().LanguageVersion <= PythonLanguageVersion.V27) ? _latin1 : Encoding.UTF8;
            }
        }

        public unsafe byte[] ReadBytes() {
            byte[] buf = Process.ReadMemoryString(Address, DkmReadMemoryFlags.None, 1, 0x10000);
            if (buf.Length > 0 && buf[buf.Length - 1] == 0) {
                Array.Resize(ref buf, buf.Length - 1);
            }
            return buf;
        }

        public string ReadUnicode() {
            byte[] bytes = ReadBytes();
            return Encoding.GetString(bytes);
        }

        public AsciiString ReadAscii() {
            byte[] bytes = ReadBytes();
            string s = Encoding.GetString(bytes);
            return new AsciiString(bytes, s);
        }

        object IValueStore.Read() {
            return ReadUnicode();
        }
    }
}
