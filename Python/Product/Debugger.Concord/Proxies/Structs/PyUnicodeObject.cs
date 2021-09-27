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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs
{
    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyUnicodeObject")]
    [PyType(MaxVersion = PythonLanguageVersion.V27, VariableName = "PyUnicode_Type")]
    internal class PyUnicodeObject27 : PyObject, IPyBaseStringObject
    {
        internal class Fields
        {
            public StructField<SSizeTProxy> length;
            public StructField<PointerProxy<ArrayProxy<UInt16Proxy>>> str;
        }

        private readonly Fields _fields;

        public PyUnicodeObject27(DkmProcess process, ulong address)
            : base(process, address)
        {
            InitializeStruct(this, out _fields);
            CheckPyType<PyUnicodeObject27>();
        }

        public static PyUnicodeObject27 Create(DkmProcess process, string value)
        {
            // Allocate string buffer together with the object itself in a single block.
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyUnicodeObject27>(value.Length * 2);
            result.length.Write(value.Length);

            var str = result.Address.OffsetBy(StructProxy.SizeOf<PyUnicodeObject27>(process));
            result.str.Raw.Write(str);

            var buf = Encoding.Unicode.GetBytes(value);
            process.WriteMemory(str, buf);

            return result;
        }

        public SSizeTProxy length
        {
            get { return GetFieldProxy(_fields.length); }
        }

        public PointerProxy<ArrayProxy<UInt16Proxy>> str
        {
            get { return GetFieldProxy(_fields.str); }
        }

        public override unsafe string ToString()
        {
            var length = this.length.Read();
            if (length == 0)
            {
                return "";
            }

            var buf = new byte[length * sizeof(char)];
            Process.ReadMemory(str.Raw.Read(), DkmReadMemoryFlags.None, buf);
            fixed (byte* p = buf)
            {
                return new string((char*)p, 0, (int)length);
            }
        }

        public override void Repr(ReprBuilder builder)
        {
            builder.AppendLiteral(ToString());
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions)
        {
            string s = ToString();

            yield return new PythonEvaluationResult(new ValueStore<long>(s.Length), "len()")
            {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (char c in s)
            {
                yield return new PythonEvaluationResult(new ValueStore<string>(c.ToString()));
            }
        }

        public static explicit operator string(PyUnicodeObject27 obj)
        {
            return (object)obj == null ? null : obj.ToString();
        }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V33, StructName = "PyUnicodeObject")]
    [PyType(MinVersion = PythonLanguageVersion.V33, VariableName = "PyUnicode_Type")]
    internal class PyUnicodeObject33 : PyVarObject, IPyBaseStringObject
    {
        private static readonly Encoding _latin1 = Encoding.GetEncoding("Latin1");

        private enum PyUnicode_Kind
        {
            PyUnicode_WCHAR_KIND = 0,
            PyUnicode_1BYTE_KIND = 1,
            PyUnicode_2BYTE_KIND = 2,
            PyUnicode_4BYTE_KIND = 4
        }

        private enum Interned
        {
            SSTATE_NOT_INTERNED = 0,
            SSTATE_INTERNED_MORTAL = 1,
            SSTATE_INTERNED_IMMORTAL = 2
        }

        private struct State
        {
            private static readonly BitVector32.Section
                internedSection = BitVector32.CreateSection(2),
                kindSection = BitVector32.CreateSection(4, internedSection),
                compactSection = BitVector32.CreateSection(1, kindSection),
                asciiSection = BitVector32.CreateSection(1, compactSection),
                readySection = BitVector32.CreateSection(1, asciiSection);

            private BitVector32 _state;

            private State(byte state)
            {
                _state = new BitVector32(state);
            }

            public static explicit operator State(byte state)
            {
                return new State(state);
            }

            public static explicit operator byte(State state)
            {
                return (byte)state._state.Data;
            }

            public Interned interned
            {
                get { return (Interned)_state[internedSection]; }
                set { _state[internedSection] = (int)value; }
            }

            public PyUnicode_Kind kind
            {
                get { return (PyUnicode_Kind)_state[kindSection]; }
                set { _state[kindSection] = (int)value; }
            }

            public bool compact
            {
                get { return _state[compactSection] != 0; }
                set { _state[compactSection] = value ? 1 : 0; }
            }

            public bool ascii
            {
                get { return _state[asciiSection] != 0; }
                set { _state[asciiSection] = value ? 1 : 0; }
            }

            public bool ready
            {
                get { return _state[readySection] != 0; }
                set { _state[readySection] = value ? 1 : 0; }
            }
        }

        public class Fields
        {
            public StructField<PointerProxy> data;
        }

        private readonly Fields _fields;
        private readonly PyASCIIObject _asciiObject;
        private readonly PyCompactUnicodeObject _compactObject;

        public PyUnicodeObject33(DkmProcess process, ulong address)
            : base(process, address)
        {
            InitializeStruct(this, out _fields);
            CheckPyType<PyUnicodeObject33>();

            _asciiObject = new PyASCIIObject(process, address);
            _compactObject = new PyCompactUnicodeObject(process, address);
        }

        public static PyUnicodeObject33 Create(DkmProcess process, string value)
        {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyUnicodeObject33>(value.Length * sizeof(char));

            result._asciiObject.hash.Write(-1);
            result._asciiObject.length.Write(value.Length);
            result._compactObject.wstr_length.Write(value.Length);

            var state = new State
            {
                interned = Interned.SSTATE_NOT_INTERNED,
                kind = PyUnicode_Kind.PyUnicode_2BYTE_KIND,
                compact = true,
                ascii = false,
                ready = true
            };
            result._asciiObject.state.Write((byte)state);

            ulong dataPtr = result.Address.OffsetBy(StructProxy.SizeOf<PyCompactUnicodeObject>(process));
            result._asciiObject.wstr.Write(dataPtr);
            process.WriteMemory(dataPtr, Encoding.Unicode.GetBytes(value));

            return result;
        }


        public override string ToString()
        {
            byte[] buf;

            State state = (State)_asciiObject.state.Read();
            if (state.ascii)
            {
                state.kind = PyUnicode_Kind.PyUnicode_1BYTE_KIND;
            }

            if (!state.ready)
            {
                ulong wstr = _asciiObject.wstr.Read();
                if (wstr == 0)
                {
                    return null;
                }

                uint wstr_length = checked((uint)_compactObject.wstr_length.Read());
                if (wstr_length == 0)
                {
                    return "";
                }

                buf = new byte[wstr_length * 2];
                Process.ReadMemory(wstr, DkmReadMemoryFlags.None, buf);
                return Encoding.Unicode.GetString(buf, 0, buf.Length);
            }

            int length = checked((int)_asciiObject.length.Read());
            if (length == 0)
            {
                return "";
            }

            ulong data;
            if (!state.compact)
            {
                data = GetFieldProxy(_fields.data).Read();
            }
            else if (state.ascii)
            {
                data = Address.OffsetBy(StructProxy.SizeOf<PyASCIIObject>(Process));
            }
            else
            {
                data = Address.OffsetBy(StructProxy.SizeOf<PyCompactUnicodeObject>(Process));
            }
            if (data == 0)
            {
                return null;
            }

            buf = new byte[length * (int)state.kind];
            Process.ReadMemory(data, DkmReadMemoryFlags.None, buf);
            Encoding enc;
            switch (state.kind)
            {
                case PyUnicode_Kind.PyUnicode_1BYTE_KIND:
                    enc = _latin1;
                    break;
                case PyUnicode_Kind.PyUnicode_2BYTE_KIND:
                    enc = Encoding.Unicode;
                    break;
                case PyUnicode_Kind.PyUnicode_4BYTE_KIND:
                    enc = Encoding.UTF32;
                    break;
                default:
                    Debug.Fail("Unsupported PyUnicode_Kind " + state.kind);
                    return null;
            }

            return enc.GetString(buf, 0, buf.Length);
        }

        public override void Repr(ReprBuilder builder)
        {
            builder.AppendLiteral(ToString());
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions)
        {
            string s = ToString();

            yield return new PythonEvaluationResult(new ValueStore<long>(s.Length), "len()")
            {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (char c in s)
            {
                yield return new PythonEvaluationResult(new ValueStore<string>(c.ToString()));
            }
        }

        public static explicit operator string(PyUnicodeObject33 obj)
        {
            return (object)obj == null ? null : obj.ToString();
        }
    }

    internal class PyASCIIObject : StructProxy
    {
        public class Fields
        {
            public StructField<SSizeTProxy> length;
            public StructField<SSizeTProxy> hash;
            public StructField<ByteProxy> state;
            public StructField<PointerProxy> wstr;
        }

        private readonly Fields _fields;

        public PyASCIIObject(DkmProcess process, ulong address)
            : base(process, address)
        {
            InitializeStruct(this, out _fields);
        }

        public SSizeTProxy length
        {
            get { return GetFieldProxy(_fields.length); }
        }

        public SSizeTProxy hash
        {
            get { return GetFieldProxy(_fields.hash); }
        }

        public ByteProxy state
        {
            get { return GetFieldProxy(_fields.state); }
        }

        public PointerProxy wstr
        {
            get { return GetFieldProxy(_fields.wstr); }
        }
    }

    internal class PyCompactUnicodeObject : StructProxy
    {
        public class Fields
        {
            public StructField<SSizeTProxy> wstr_length;
        }

        private readonly Fields _fields;

        public PyCompactUnicodeObject(DkmProcess process, ulong address)
            : base(process, address)
        {
            InitializeStruct(this, out _fields);
        }

        public SSizeTProxy wstr_length
        {
            get { return GetFieldProxy(_fields.wstr_length); }
        }
    }
}
