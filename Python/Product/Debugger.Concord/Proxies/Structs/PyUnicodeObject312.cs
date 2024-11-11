using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.OS;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(MinVersion = PythonLanguageVersion.V312, StructName = "PyUnicodeObject")]
    [PyType(MinVersion = PythonLanguageVersion.V312, VariableName = "PyUnicode_Type")]
    internal class PyUnicodeObject312 : PyUnicodeObject {
        private enum PyUnicode_Kind {
            PyUnicode_1BYTE_KIND = 1,
            PyUnicode_2BYTE_KIND = 2,
            PyUnicode_4BYTE_KIND = 4
        }

        private enum Interned {
            SSTATE_NOT_INTERNED = 0,
            SSTATE_INTERNED_MORTAL = 1,
            SSTATE_INTERNED_IMMORTAL = 2
        }

        private struct State {
            private static readonly BitVector32.Section
                internedSection = BitVector32.CreateSection(2),
                kindSection = BitVector32.CreateSection(4, internedSection),
                compactSection = BitVector32.CreateSection(1, kindSection),
                asciiSection = BitVector32.CreateSection(1, compactSection),
                readySection = BitVector32.CreateSection(1, asciiSection);

            private BitVector32 _state;

            private State(byte state) {
                _state = new BitVector32(state);
            }

            public static explicit operator State(byte state) {
                return new State(state);
            }

            public static explicit operator byte(State state) {
                return (byte)state._state.Data;
            }

            public Interned interned {
                get { return (Interned)_state[internedSection]; }
                set { _state[internedSection] = (int)value; }
            }

            public PyUnicode_Kind kind {
                get { return (PyUnicode_Kind)_state[kindSection]; }
                set { _state[kindSection] = (int)value; }
            }

            public bool compact {
                get { return _state[compactSection] != 0; }
                set { _state[compactSection] = value ? 1 : 0; }
            }

            public bool ascii {
                get { return _state[asciiSection] != 0; }
                set { _state[asciiSection] = value ? 1 : 0; }
            }

            public bool ready {
                get { return _state[readySection] != 0; }
                set { _state[readySection] = value ? 1 : 0; }
            }
        }

        public class Fields {
            public StructField<PointerProxy> data;
        }

        private readonly Fields _fields;
        private readonly PyASCIIObject312 _asciiObject;
        private readonly PyCompactUnicodeObject312 _compactObject;

        public PyUnicodeObject312(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyUnicodeObject312>();

            _asciiObject = new PyASCIIObject312(process, address);
            _compactObject = new PyCompactUnicodeObject312(process, address);
        }

        public static PyUnicodeObject312 Create312(DkmProcess process, string value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyUnicodeObject312>(value.Length * sizeof(char));

            result._asciiObject.hash.Write(-1);
            result._asciiObject.length.Write(value.Length);
            result._compactObject.utf8_length.Write(value.Length);
            var kind = value.Length != Encoding.UTF8.GetByteCount(value) ? PyUnicode_Kind.PyUnicode_2BYTE_KIND : PyUnicode_Kind.PyUnicode_1BYTE_KIND;

            var state = new State {
                interned = Interned.SSTATE_NOT_INTERNED,
                kind = kind,
                compact = true,
                ascii = false,
                ready = true
            };
            result._asciiObject.state.Write((byte)state);

            ulong dataPtr = result.Address.OffsetBy(StructProxy.SizeOf<PyCompactUnicodeObject312>(process));
            result._compactObject.utf8.Write(dataPtr);
            process.WriteMemory(dataPtr, Encoding.UTF8.GetBytes(value));

            return result;
        }

        public override string ToString() {
            byte[] buf;

            State state = (State)_asciiObject.state.Read();
            if (state.ascii) {
                state.kind = PyUnicode_Kind.PyUnicode_1BYTE_KIND;
            }

            int length = checked((int)_asciiObject.length.Read());
            if (length == 0) {
                return "";
            }

            ulong data;
            if (!state.compact) {
                data = GetFieldProxy(_fields.data).Read();
            } else if (state.ascii) {
                data = Address.OffsetBy(StructProxy.SizeOf<PyASCIIObject312>(Process));
            } else {
                data = Address.OffsetBy(StructProxy.SizeOf<PyCompactUnicodeObject312>(Process));
            }
            if (data == 0) {
                return null;
            }

            buf = new byte[length * (int)state.kind];
            Process.ReadMemory(data, DkmReadMemoryFlags.None, buf);
            Encoding enc;
            switch (state.kind) {
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
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V312, StructName = "PyASCIIObject")]
    [PyType(MinVersion = PythonLanguageVersion.V312, VariableName = "PyASCII_Type")]
    internal class PyASCIIObject312 : StructProxy {
        public class Fields {
            public StructField<SSizeTProxy> length;
            public StructField<SSizeTProxy> hash;
            public StructField<ByteProxy> state;
        }

        private readonly Fields _fields;

        public PyASCIIObject312(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public SSizeTProxy length {
            get { return GetFieldProxy(_fields.length); }
        }

        public SSizeTProxy hash {
            get { return GetFieldProxy(_fields.hash); }
        }

        public ByteProxy state {
            get { return GetFieldProxy(_fields.state); }
        }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V312, StructName = "PyCompactUnicodeObject")]
    [PyType(MinVersion = PythonLanguageVersion.V312, VariableName = "PyCompactUnicode_Type")]
    internal class PyCompactUnicodeObject312 : StructProxy {
        public class Fields {
            public StructField<SSizeTProxy> utf8_length;
            public StructField<PointerProxy> utf8;
        }

        private readonly Fields _fields;

        public PyCompactUnicodeObject312(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public SSizeTProxy utf8_length {
            get { return GetFieldProxy(_fields.utf8_length); }
        }

        public PointerProxy utf8 {
            get { return GetFieldProxy(_fields.utf8); }
        }
    }
}
