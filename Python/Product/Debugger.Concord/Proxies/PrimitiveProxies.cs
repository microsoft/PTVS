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
using System.Diagnostics.CodeAnalysis;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies {
    [DebuggerDisplay("& {Read()}")]
    internal struct ByteProxy : IWritableDataProxy<Byte> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public ByteProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Byte); }
        }

        public unsafe Byte Read() {
            Byte result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Byte));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Byte value) {
            Process.WriteMemory(Address, new[] { value });
        }

        void IWritableDataProxy.Write(object value) {
            Write((Byte)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct SByteProxy : IWritableDataProxy<SByte> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public SByteProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(SByte); }
        }

        public unsafe SByte Read() {
            SByte result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(SByte));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(SByte value) {
            Process.WriteMemory(Address, new[] { (byte)value });
        }

        void IWritableDataProxy.Write(object value) {
            Write((SByte)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct Int16Proxy : IWritableDataProxy<Int16> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public Int16Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Int16); }
        }

        public unsafe Int16 Read() {
            Int16 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Int16));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Int16 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((Int16)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct UInt16Proxy : IWritableDataProxy<UInt16> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public UInt16Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(UInt16); }
        }

        public unsafe UInt16 Read() {
            UInt16 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(UInt16));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(UInt16 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((UInt16)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct Int32Proxy : IWritableDataProxy<Int32> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public Int32Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Int32); }
        }

        public unsafe Int32 Read() {
            Int32 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Int32));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Int32 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((Int32)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct UInt32Proxy : IWritableDataProxy<UInt32> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public UInt32Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(UInt32); }
        }

        public unsafe UInt32 Read() {
            UInt32 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(UInt32));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(UInt32 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((UInt32)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct Int32EnumProxy<TEnum> : IWritableDataProxy<TEnum> {
        public Int32Proxy UnderlyingProxy { get; private set; }

        [SuppressMessage("Microsoft.Usage", "CA2207:InitializeValueTypeStaticFieldsInline",
            Justification = ".cctor used for debug check, not initialization")]
        static Int32EnumProxy() {
            Debug.Assert(typeof(TEnum).IsSubclassOf(typeof(Enum)));
        }

        public Int32EnumProxy(DkmProcess process, ulong address)
            : this() {
            UnderlyingProxy = new Int32Proxy(process, address);
        }

        public DkmProcess Process {
            get { return UnderlyingProxy.Process; }
        }

        public ulong Address {
            get { return UnderlyingProxy.Address; }
        }

        public long ObjectSize {
            get { return UnderlyingProxy.ObjectSize; }
        }

        public unsafe TEnum Read() {
            return (TEnum)Enum.ToObject(typeof(TEnum), UnderlyingProxy.Read());
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(TEnum value) {
            UnderlyingProxy.Write(Convert.ToInt32(value));
        }

        void IWritableDataProxy.Write(object value) {
            Write((TEnum)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct Int64Proxy : IWritableDataProxy<Int64> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public Int64Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Int64); }
        }

        public unsafe Int64 Read() {
            Int64 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Int64));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Int64 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((Int64)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct UInt64Proxy : IWritableDataProxy<UInt64> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public UInt64Proxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(UInt64); }
        }

        public unsafe UInt64 Read() {
            UInt64 result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(UInt64));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(UInt64 value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((UInt64)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct SingleProxy : IWritableDataProxy<Single> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public SingleProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Single); }
        }

        public unsafe Single Read() {
            Single result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Single));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Single value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((Single)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct DoubleProxy : IWritableDataProxy<Double> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public DoubleProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(Double); }
        }

        public unsafe Double Read() {
            Double result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(Double));
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(Double value) {
            byte[] buf = BitConverter.GetBytes(value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((Double)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct SSizeTProxy : IWritableDataProxy<long> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public SSizeTProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return Process.GetPointerSize(); }
        }

        public unsafe long Read() {
            if (Process.Is64Bit()) {
                long result;
                Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(long));
                return result;
            } else {
                int result;
                Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, sizeof(int));
                return result;
            }
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(long value) {
            byte[] buf = Process.Is64Bit() ? BitConverter.GetBytes(value) : BitConverter.GetBytes((int)value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((long)value);
        }

        public void Increment(long amount = 1) {
            Write(Read() + amount);
        }

        public void Decrement(long amount = 1) {
            Increment(-amount);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct BoolProxy : IWritableDataProxy<bool> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public BoolProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(byte); }
        }

        public unsafe bool Read() {
            byte b;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &b, sizeof(byte));
            return b != 0;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(bool value) {
            Process.WriteMemory(Address, new[] { value ? (byte)1 : (byte)0 });
        }

        void IWritableDataProxy.Write(object value) {
            Write((bool)value);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct CharProxy : IDataProxy {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public CharProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return sizeof(byte); }
        }

        unsafe object IValueStore.Read() {
            byte b;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &b, sizeof(byte));
            string s = ((char)b).ToString();

            if (Process.GetPythonRuntimeInfo().LanguageVersion <= PythonLanguageVersion.V27) {
                return new AsciiString(new[] { b }, s);
            } else {
                return s;
            }
        }
    }
}
