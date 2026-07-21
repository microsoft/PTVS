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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    /// <summary>
    /// Managed port of CPython's location-table (PEP 626) decoder used to map a bytecode
    /// offset to a source line without executing any code in the debuggee. The
    /// <c>co_linetable</c> format is shared by CPython 3.11, 3.12 and 3.13.
    /// See CPython <c>Objects/codeobject.c</c> (<c>PyCode_Addr2Line</c> / <c>advance</c>).
    /// </summary>
    internal static class PyLineTable {
        // Location info codes stored in the high nibble of an entry's first byte.
        private const int PY_CODE_LOCATION_INFO_ONE_LINE0 = 10;
        private const int PY_CODE_LOCATION_INFO_ONE_LINE1 = 11;
        private const int PY_CODE_LOCATION_INFO_ONE_LINE2 = 12;
        private const int PY_CODE_LOCATION_INFO_NO_COLUMNS = 13;
        private const int PY_CODE_LOCATION_INFO_LONG = 14;
        private const int PY_CODE_LOCATION_INFO_NONE = 15;

        /// <summary>
        /// Returns the source line for the bytecode byte offset <paramref name="addrq"/>, or
        /// -1 if the offset maps to a range with no source location (a NONE marker) or lies
        /// past the end of the table. A negative offset returns <paramref name="firstLineNo"/>,
        /// matching CPython's behavior for an entry frame whose instruction pointer sits just
        /// before the first instruction.
        /// </summary>
        public static int Addr2Line(byte[] lineTable, int firstLineNo, int addrq) {
            if (lineTable == null) {
                return -1;
            }

            // Mirrors PyCode_Addr2Line: a negative query maps to the code's first line.
            if (addrq < 0) {
                return firstLineNo;
            }

            int loNext = 0;
            int limit = lineTable.Length;
            int arEnd = 0;
            int computedLine = firstLineNo;
            int arLine = -1;

            while (arEnd <= addrq) {
                if (loNext >= limit) {
                    return -1;
                }

                byte first = lineTable[loNext];
                computedLine += GetLineDelta(lineTable, loNext);
                arLine = IsNoLineMarker(first) ? -1 : computedLine;
                arEnd += ((first & 7) + 1) * sizeof(ushort); // sizeof(_Py_CODEUNIT) == 2

                // Skip to the next entry; only the first byte of an entry has bit 7 set.
                do {
                    loNext++;
                } while (loNext < limit && (lineTable[loNext] & 128) == 0);
            }

            return arLine;
        }

        private static bool IsNoLineMarker(byte b) {
            return (b >> 3) == 0x1f;
        }

        private static int GetLineDelta(byte[] lineTable, int index) {
            int code = (lineTable[index] >> 3) & 15;
            switch (code) {
                case PY_CODE_LOCATION_INFO_NONE:
                    return 0;
                case PY_CODE_LOCATION_INFO_NO_COLUMNS:
                case PY_CODE_LOCATION_INFO_LONG:
                    int p = index + 1;
                    return ReadSignedVarint(lineTable, ref p);
                case PY_CODE_LOCATION_INFO_ONE_LINE0:
                    return 0;
                case PY_CODE_LOCATION_INFO_ONE_LINE1:
                    return 1;
                case PY_CODE_LOCATION_INFO_ONE_LINE2:
                    return 2;
                default: // short forms (same line)
                    return 0;
            }
        }

        private static int ReadVarint(byte[] data, ref int p) {
            uint read = data[p++];
            uint val = read & 63;
            int shift = 0;
            while ((read & 64) != 0 && p < data.Length) {
                read = data[p++];
                shift += 6;
                val |= (read & 63) << shift;
            }
            return (int)val;
        }

        private static int ReadSignedVarint(byte[] data, ref int p) {
            uint uval = (uint)ReadVarint(data, ref p);
            return ((uval & 1) != 0) ? -(int)(uval >> 1) : (int)(uval >> 1);
        }
    }
}
