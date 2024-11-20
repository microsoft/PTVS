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
using System.Numerics;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal class PyLongObject : PyVarObject {
        private class Fields {
            [FieldProxy(MaxVersion = Common.Parsing.PythonLanguageVersion.V311)]
            public StructField<ByteProxy> ob_digit; // this is actually either uint16 or uint32, depending on Python bitness
            [FieldProxy(MinVersion = PythonLanguageVersion.V312)]
            public StructField<_PyLongValue> long_value;
        }

        private readonly Fields _fields;

        public PyLongObject(DkmProcess process, ulong address)
            : this(process, address, true) {
        }

        protected PyLongObject(DkmProcess process, ulong address, bool checkType)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            if (checkType) {
                CheckPyType<PyLongObject>();
            }
        }

        public static PyLongObject Create(DkmProcess process, BigInteger value) {
            // Use two different methods. PyLongObjects changed in 3.12. Instead of inheritance, we'll use an if statement so 
            // that we don't have to change classes derived from PyLongObject.
            if (process.GetPythonRuntimeInfo().LanguageVersion < PythonLanguageVersion.V312) {
                return Create11(process, value);
            } else {
                return Create12(process, value);
            }
        }

        private static PyLongObject Create11(DkmProcess process, BigInteger value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var bitsInDigit = process.Is64Bit() ? 30 : 15;
            var bytesInDigit = process.Is64Bit() ? 4 : 2;

            var absValue = BigInteger.Abs(value);
            long numDigits = 0;
            for (var t = absValue; t != 0; ) {
                ++numDigits;
                t >>= bitsInDigit;
            }

            var result = allocator.Allocate<PyLongObject>(numDigits * bytesInDigit);

            if (value == 0) {
                result.ob_size.Write(0);
            } else if (value > 0) {
                result.ob_size.Write(numDigits);
            } else if (value < 0) {
                result.ob_size.Write(-numDigits);
            }

            if (bitsInDigit == 15) {
                for (var digitPtr = new UInt16Proxy(process, result.ob_digit.Address); absValue != 0; digitPtr = digitPtr.GetAdjacentProxy(1)) {
                    digitPtr.Write((ushort)(absValue % (1 << bitsInDigit)));
                    absValue >>= bitsInDigit;
                }
            } else {
                for (var digitPtr = new UInt32Proxy(process, result.ob_digit.Address); absValue != 0; digitPtr = digitPtr.GetAdjacentProxy(1)) {
                    digitPtr.Write((uint)(absValue % (1 << bitsInDigit)));
                    absValue >>= bitsInDigit;
                }
            }

            return result;
        }
        
        private static PyLongObject Create12(DkmProcess process, BigInteger value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var bitsInDigit = process.Is64Bit() ? 30 : 15;
            var bytesInDigit = process.Is64Bit() ? 4 : 2;

            var absValue = BigInteger.Abs(value);
            long numDigits = 0;
            for (var t = absValue; t != 0;) {
                ++numDigits;
                t >>= bitsInDigit;
            }

            var result = allocator.Allocate<PyLongObject>(numDigits * bytesInDigit);

            // Size comes from here:
            // https://github.com/python/cpython/blob/a0866f4c81ecc057d4521e8e7a02f4e1fff175a1/Objects/longobject.c#L158
            var fields = StructProxy.GetStructFields<_PyLongValue, _PyLongValue.Fields>(process);
            long ob_size = numDigits * bytesInDigit + fields.ob_digit.Offset;
            result.ob_size.Write(ob_size);

            // Digits are stored in the long_value.lv_data field in 3.12
            result.long_value.WriteTag((ulong)numDigits, value == 0, value < 0);

            // Then we write the data out one digit at a time
            if (bitsInDigit == 15) {
                for (var digitPtr = new UInt16Proxy(process, result.ob_digit.Address); absValue != 0; digitPtr = digitPtr.GetAdjacentProxy(1)) {
                    digitPtr.Write((ushort)(absValue % (1 << bitsInDigit)));
                    absValue >>= bitsInDigit;
                }
            } else {
                for (var digitPtr = new UInt32Proxy(process, result.ob_digit.Address); absValue != 0; digitPtr = digitPtr.GetAdjacentProxy(1)) {
                    digitPtr.Write((uint)(absValue % (1 << bitsInDigit)));
                    absValue >>= bitsInDigit;
                }
            }

            return result;
        }

        private ByteProxy ob_digit {
            get {
                if (_fields.ob_digit.Process != null) {
                    return GetFieldProxy(_fields.ob_digit);
                } else {
                    return GetFieldProxy(_fields.long_value).ob_digit;
                }
            }
        }

        public BigInteger ToBigInteger() {
            if (Process.GetPythonRuntimeInfo().LanguageVersion < PythonLanguageVersion.V312) {
                return ToBigInteger11();
            } else {
                return ToBigInteger12();
            }
        }

        public _PyLongValue long_value => GetFieldProxy(_fields.long_value);

        private BigInteger ToBigInteger11() {
            var bitsInDigit = Process.Is64Bit() ? 30 : 15;

            long ob_size = this.ob_size.Read();
            if (ob_size == 0) {
                return 0;
            } 
            long count = Math.Abs(ob_size);

            // Read and parse digits in reverse, starting from the most significant ones.
            var result = new BigInteger(0);
            if (bitsInDigit == 15) {
                var digitPtr = new UInt16Proxy(Process, ob_digit.Address).GetAdjacentProxy(count);
                for (long i = 0; i != count; ++i) {
                    digitPtr = digitPtr.GetAdjacentProxy(-1);
                    result <<= bitsInDigit;
                    result += digitPtr.Read();
                }
            } else {
                var digitPtr = new UInt32Proxy(Process, ob_digit.Address).GetAdjacentProxy(count);
                for (long i = 0; i != count; ++i) {
                    digitPtr = digitPtr.GetAdjacentProxy(-1);
                    result <<= bitsInDigit;
                    result += digitPtr.Read();
                }
            }

            return ob_size > 0 ? result : -result;
        }

        public BigInteger ToBigInteger12() {
            var bitsInDigit = Process.Is64Bit() ? 30 : 15;
            var long_value = GetFieldProxy(_fields.long_value);
            long count = long_value.digit_count;

            // Read and parse digits in reverse, starting from the most significant ones.
            var result = new BigInteger(0);
            if (bitsInDigit == 15) {
                var digitPtr = new UInt16Proxy(Process, ob_digit.Address).GetAdjacentProxy(count);
                for (long i = 0; i != count; ++i) {
                    digitPtr = digitPtr.GetAdjacentProxy(-1);
                    result <<= bitsInDigit;
                    result += digitPtr.Read();
                }
            } else {
                var digitPtr = new UInt32Proxy(Process, ob_digit.Address).GetAdjacentProxy(count);
                for (long i = 0; i != count; ++i) {
                    digitPtr = digitPtr.GetAdjacentProxy(-1);
                    result <<= bitsInDigit;
                    result += digitPtr.Read();
                }
            }

            return long_value.is_negative ? -result : result;
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendLiteral(ToBigInteger());
        }

        [StructProxy(MinVersion = PythonLanguageVersion.V312, StructName = "_PyLongValue")]
        public class _PyLongValue : StructProxy {
            public class Fields {
                public StructField<UInt64Proxy> lv_tag;
                public StructField<ByteProxy> ob_digit;
            }

            private const int SIGN_ZERO = 1;
            private const int SIGN_NEGATIVE = 2;
            private const int NON_SIZE_BITS = 3;
            private const int SIGN_MASK = 3;

            private readonly Fields _fields;

            public _PyLongValue(DkmProcess process, ulong address)
                : base(process, address) {
                InitializeStruct(this, out _fields);
            }

            public ByteProxy ob_digit => GetFieldProxy(_fields.ob_digit);

            public UInt64Proxy lv_tag => GetFieldProxy(_fields.lv_tag);

            public void WriteTag(ulong numberDigits, bool isZero, bool isNegative) {
                ulong lv_tag = numberDigits << NON_SIZE_BITS;
                if (isZero) {
                    lv_tag |= SIGN_ZERO;
                } else if (isNegative) {
                    lv_tag |= SIGN_NEGATIVE;
                }
                this.lv_tag.Write(lv_tag);
            }

            public uint digit_count {
                get {
                    return (uint)(lv_tag.Read() >> NON_SIZE_BITS);
                }

                set {
                    lv_tag.Write((value << NON_SIZE_BITS) | (lv_tag.Read() & SIGN_MASK));
                }
            }

            public bool is_negative {
                get {
                    return (lv_tag.Read() & SIGN_MASK) == SIGN_NEGATIVE;
                }
            }
        }
    }


}
