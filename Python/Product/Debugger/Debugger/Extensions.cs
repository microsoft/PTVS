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
using System.IO;
using System.Text;

namespace Microsoft.PythonTools.Debugger {
    static class Extensions {
        /// <summary>
        /// Reads a string from the socket which is encoded as:
        ///     U, byte count, bytes 
        ///     A, byte count, ASCII
        ///     
        /// Which supports either UTF-8 or ASCII strings.
        /// </summary>
        internal static string ReadString(this Stream stream) {
            int type = stream.ReadByte();
            if (type < 0) {
                Debug.Assert(false, "Socket.ReadString failed to read string type");
                throw new IOException();
            }

            bool isUnicode;
            switch ((char)type) {
                case 'N': // null string
                    return null;
                case 'U':
                    isUnicode = true;
                    break;
                case 'A':
                    isUnicode = false;
                    break;
                default:
                    Debug.Assert(false, "Socket.ReadString failed to parse unknown string type " + (char)type);
                    throw new IOException();
            }

            int len = stream.ReadInt32();
            byte[] buffer = new byte[len];
            stream.ReadToFill(buffer);

            if (isUnicode) {
                return Encoding.UTF8.GetString(buffer);
            } else {
                char[] chars = new char[buffer.Length];
                for (int i = 0; i < buffer.Length; i++) {
                    chars[i] = (char)buffer[i];
                }
                return new string(chars);
            }
        }

        internal static int ReadInt32(this Stream stream) {
            return (int)stream.ReadInt64();
        }

        internal static long ReadInt64(this Stream stream) {
            byte[] buf = new byte[8];
            stream.ReadToFill(buf);

            // Can't use BitConverter because we need to convert big-endian to little-endian here,
            // and BitConverter.IsLittleEndian is platform-dependent (and usually true).
            ulong hi = (ulong)(uint)((buf[0] << 0x18) | (buf[1] << 0x10) | (buf[2] << 0x08) | (buf[3] << 0x00));
            ulong lo = (ulong)(uint)((buf[4] << 0x18) | (buf[5] << 0x10) | (buf[6] << 0x08) | (buf[7] << 0x00));
            return (long)((hi << 0x20) | lo);
        }

        internal static string ReadAsciiString(this Stream stream, int length) {
            var buf = new byte[length];
            stream.ReadToFill(buf);
            return Encoding.ASCII.GetString(buf, 0, buf.Length);
        }

        internal static void ReadToFill(this Stream stream, byte[] b) {
            int count = stream.ReadBytes(b, b.Length);
            if (count != b.Length) {
                throw new EndOfStreamException();
            }
        }

        internal static int ReadBytes(this Stream stream, byte[] b, int count) {
            int i = 0;
            while (i < count) {
                int read = stream.Read(b, i, count - i);
                if (read == 0) {
                    break;
                }
                i += read;
            }
            return i;
        }

        internal static void WriteInt32(this Stream stream, int x) {
            stream.WriteInt64(x);
        }

        internal static void WriteInt64(this Stream stream, long x) {
            // Can't use BitConverter because we need to convert big-endian to little-endian here,
            // and BitConverter.IsLittleEndian is platform-dependent (and usually true).
            uint hi = (uint)((ulong)x >> 0x20);
            uint lo = (uint)((ulong)x & 0xFFFFFFFFu);
            byte[] buf = {
                (byte)((hi >> 0x18) & 0xFFu),
                (byte)((hi >> 0x10) & 0xFFu),
                (byte)((hi >> 0x08) & 0xFFu),
                (byte)((hi >> 0x00) & 0xFFu),
                (byte)((lo >> 0x18) & 0xFFu),
                (byte)((lo >> 0x10) & 0xFFu),
                (byte)((lo >> 0x08) & 0xFFu),
                (byte)((lo >> 0x00) & 0xFFu)
            };
            stream.Write(buf, 0, buf.Length);
        }

        internal static void WriteString(this Stream stream, string str) {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            stream.WriteInt32(bytes.Length);
            if (bytes.Length > 0) {
                stream.Write(bytes);
            }
        }

        internal static void Write(this Stream stream, byte[] b) {
            stream.Write(b, 0, b.Length);
        }

        /// <summary>
        /// Replaces \uxxxx with the actual unicode char for a prettier display in local variables.
        /// </summary>
        public static string FixupEscapedUnicodeChars(this string text) {
            StringBuilder buf = null;
            int i = 0;
            int l = text.Length;
            int val;
            while (i < l) {
                char ch = text[i++];
                if (ch == '\\') {
                    if (buf == null) {
                        buf = new StringBuilder(text.Length);
                        buf.Append(text, 0, i - 1);
                    }

                    if (i >= l) {
                        return text;
                    }
                    ch = text[i++];

                    if (ch == 'u' || ch == 'U') {
                        int len = (ch == 'u') ? 4 : 8;
                        int max = 16;
                        if (TryParseInt(text, i, len, max, out val)) {
                            buf.Append((char)val);
                            i += len;
                        } else {
                            return text;
                        }
                    } else {
                        buf.Append("\\");
                        buf.Append(ch);
                    }
                } else if (buf != null) {
                    buf.Append(ch);
                }
            }

            if (buf != null) {
                return buf.ToString();
            }
            return text;
        }


        private static bool TryParseInt(string text, int start, int length, int b, out int value) {
            value = 0;
            if (start + length > text.Length) {
                return false;
            }
            for (int i = start, end = start + length; i < end; i++) {
                int onechar;
                if (HexValue(text[i], out onechar) && onechar < b) {
                    value = value * b + onechar;
                } else {
                    return false;
                }
            }
            return true;
        }

        private static int HexValue(char ch) {
            int value;
            if (!HexValue(ch, out value)) {
                throw new ArgumentException(Strings.InvalidHexValue.FormatUI(ch), nameof(ch));
            }
            return value;
        }

        private static bool HexValue(char ch, out int value) {
            switch (ch) {
                case '0':
                case '\x660': value = 0; break;
                case '1':
                case '\x661': value = 1; break;
                case '2':
                case '\x662': value = 2; break;
                case '3':
                case '\x663': value = 3; break;
                case '4':
                case '\x664': value = 4; break;
                case '5':
                case '\x665': value = 5; break;
                case '6':
                case '\x666': value = 6; break;
                case '7':
                case '\x667': value = 7; break;
                case '8':
                case '\x668': value = 8; break;
                case '9':
                case '\x669': value = 9; break;
                default:
                    if (ch >= 'a' && ch <= 'z') {
                        value = ch - 'a' + 10;
                    } else if (ch >= 'A' && ch <= 'Z') {
                        value = ch - 'A' + 10;
                    } else {
                        value = -1;
                        return false;
                    }
                    break;
            }
            return true;
        }
    }
}
