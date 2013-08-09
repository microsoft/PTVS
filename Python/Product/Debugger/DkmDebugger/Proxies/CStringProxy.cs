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
using System.Diagnostics;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies {
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
