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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Simple implementation of ASCII encoding/decoding.  The default instance (PythonAsciiEncoding.Instance) is
    /// setup to always convert even values outside of the ASCII range.  The EncoderFallback/DecoderFallbacks can
    /// be replaced with versions that will throw exceptions instead though.
    /// </summary>
    [Serializable]
    sealed class PythonAsciiEncoding : Encoding {
        internal static readonly Encoding Instance = MakeNonThrowing();
        internal static readonly Encoding SourceEncoding = MakeSourceEncoding();
        internal static readonly Encoding SourceEncodingNoFallback = MakeSourceEncodingNoFallback();

        internal PythonAsciiEncoding()
            : base() {
        }

        internal static Encoding MakeNonThrowing() {
            // we need to Clone the new instance here so that the base class marks us as non-readonly
            Encoding enc = (Encoding)new PythonAsciiEncoding().Clone();
            enc.DecoderFallback = new NonStrictDecoderFallback();
            enc.EncoderFallback = new NonStrictEncoderFallback();
            return enc;
        }

        private static Encoding MakeSourceEncoding() {
            // we need to Clone the new instance here so that the base class marks us as non-readonly
            Encoding enc = (Encoding)new PythonAsciiEncoding().Clone();
            enc.DecoderFallback = new SourceNonStrictDecoderFallback();
            return enc;
        }

        private static Encoding MakeSourceEncodingNoFallback() {
            // we need to Clone the new instance here so that the base class marks us as non-readonly
            Encoding enc = (Encoding)new PythonAsciiEncoding().Clone();
            enc.DecoderFallback = new SourceNonStrictDecoderFallbackNoFallback();
            return enc;
        }

        public override int GetByteCount(char[] chars, int index, int count) {
            int byteCount = 0;
            int charEnd = index + count;
            while (index < charEnd) {
                char c = chars[index];
                if (c > 0x7f) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, index)) {
                        byteCount += efb.Remaining;
                    }
                } else {
                    byteCount++;
                }
                index++;
            }
            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            int charEnd = charIndex + charCount;
            int outputBytes = 0;
            while (charIndex < charEnd) {
                char c = chars[charIndex];
                if (c > 0x7f) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, charIndex)) {
                        while (efb.Remaining != 0) {
                            bytes[byteIndex++] = (byte)efb.GetNextChar();
                            outputBytes++;
                        }
                    }
                } else {
                    bytes[byteIndex++] = (byte)c;
                    outputBytes++;
                }
                charIndex++;
            }
            return outputBytes;
        }

        public override int GetCharCount(byte[] bytes, int index, int count) {
            int byteEnd = index + count;
            int outputChars = 0;
            while (index < byteEnd) {
                byte b = bytes[index];
                if (b > 0x7f) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new byte[] { b }, index)) {
                        outputChars += dfb.Remaining;
                    }
                } else {
                    outputChars++;
                }
                index++;
            }
            return outputChars;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            int byteEnd = byteIndex + byteCount;
            int outputChars = 0;
            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
                if (b > 0x7f) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new byte[] { b }, byteIndex)) {
                        while (dfb.Remaining != 0) {
                            chars[charIndex++] = dfb.GetNextChar();
                            outputChars++;
                        }
                    }
                } else {
                    chars[charIndex++] = (char)b;
                    outputChars++;
                }
                byteIndex++;
            }
            return outputChars;
        }

        public override int GetMaxByteCount(int charCount) {
            return charCount * 4;
        }

        public override int GetMaxCharCount(int byteCount) {
            return byteCount;
        }

        public override string WebName {
            get {
                return "ascii";
            }
        }

        public override string EncodingName {
            get {
                return "ascii";
            }
        }
    }

    class NonStrictEncoderFallback : EncoderFallback {
        public override EncoderFallbackBuffer CreateFallbackBuffer() {
            return new NonStrictEncoderFallbackBuffer();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    class NonStrictEncoderFallbackBuffer : EncoderFallbackBuffer {
        private List<char> _buffer = new List<char>();
        private int _index;

        public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index) {
            throw new EncoderFallbackException("'ascii' codec can't encode character '\\u{0:X}{1:04X}' in position {2}".FormatUI((int)charUnknownHigh, (int)charUnknownLow, index));
        }

        public override bool Fallback(char charUnknown, int index) {
            if (charUnknown > 0xff) {
                throw new EncoderFallbackException("'ascii' codec can't encode character '\\u{0:X}' in position {1}".FormatUI((int)charUnknown, index));
            }

            _buffer.Add(charUnknown);
            return true;
        }

        public override char GetNextChar() {
            return _buffer[_index++];
        }

        public override bool MovePrevious() {
            if (_index > 0) {
                _index--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _buffer.Count - _index; }
        }
    }

    class NonStrictDecoderFallback : DecoderFallback {
        public override DecoderFallbackBuffer CreateFallbackBuffer() {
            return new NonStrictDecoderFallbackBuffer();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    // no ctors on DecoderFallbackBuffer in Silverlight
    class NonStrictDecoderFallbackBuffer : DecoderFallbackBuffer {
        private List<byte> _bytes = new List<byte>();
        private int _index, _remaining = 1;

        public override bool Fallback(byte[] bytesUnknown, int index) {
            _bytes.AddRange(bytesUnknown);
            return true;
        }

        public override char GetNextChar() {
            if (_index == _bytes.Count) {
                return char.MinValue;
            }
            _remaining--;
            return (char)_bytes[_index++];
        }

        public override bool MovePrevious() {
            if (_index > 0) {
                _index--;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _remaining; }
        }
    }

    class SourceNonStrictDecoderFallback : DecoderFallback {
        public override DecoderFallbackBuffer CreateFallbackBuffer() {
            return new SourceNonStrictDecoderFallbackBuffer();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    // no ctors on DecoderFallbackBuffer in Silverlight
    class SourceNonStrictDecoderFallbackBuffer : DecoderFallbackBuffer {
        public override bool Fallback(byte[] bytesUnknown, int index) {
            throw new BadSourceException(bytesUnknown[0], index);
        }

        public override char GetNextChar() {
            throw new NotImplementedException();
        }

        public override bool MovePrevious() {
            throw new NotImplementedException();
        }

        public override int Remaining {
            get { throw new NotImplementedException(); }
        }
    }

    class SourceNonStrictDecoderFallbackNoFallback : DecoderFallback {
        public override DecoderFallbackBuffer CreateFallbackBuffer() {
            return new SourceNonStrictDecoderFallbackBufferNoFallback();
        }

        public override int MaxCharCount {
            get { return 1; }
        }
    }

    // no ctors on DecoderFallbackBuffer in Silverlight
    class SourceNonStrictDecoderFallbackBufferNoFallback : DecoderFallbackBuffer {
        private int _fallbackLen, _initialFallbackLen;     
        
        public override bool Fallback(byte[] bytesUnknown, int index) {
            _initialFallbackLen = _fallbackLen = bytesUnknown.Length;
            return true;
        }

        public override char GetNextChar() {
            _fallbackLen--;
            return '?';
        }

        public override bool MovePrevious() {
            if (_fallbackLen < _initialFallbackLen) {
                _fallbackLen++;
                return true;
            }
            return false;
        }

        public override int Remaining {
            get { return _fallbackLen; }
        }
    }


    [Serializable]
    public class BadSourceException : Exception {
        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors",
            Justification = "cannot seal class because of back compat")]
        public BadSourceException(byte b, int index) {
            Data["BadByte"] = b;
            Data["Index"] = index;
        }

        public BadSourceException() : base() { }
        public BadSourceException(string msg)
            : base(msg) {
        }
        public BadSourceException(string message, Exception innerException)
            : base(message, innerException) {
        }
        protected BadSourceException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public byte BadByte {
            get {
                if (Data.Contains("BadByte")) {
                    return (byte)Data["BadByte"];
                }
                return 0;
            }
        }

        public int Index {
            get {
                if (Data.Contains("Index")) {
                    return (int)Data["Index"];
                }
                return 0;
            }
        }
    }
}
