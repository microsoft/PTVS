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
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies {
    [DebuggerDisplay("& {Read()}")]
    internal struct PointerProxy : IWritableDataProxy<ulong> {
        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        public PointerProxy(DkmProcess process, ulong address)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
        }

        public long ObjectSize {
            get { return Process.GetPointerSize(); }
        }

        public bool IsNull {
            get { return Read() == 0; }
        }

        public unsafe ulong Read() {
            ulong result;
            Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, Process.GetPointerSize());
            return result;
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(ulong value) {
            byte[] buf = Process.Is64Bit() ? BitConverter.GetBytes(value) : BitConverter.GetBytes((uint)value);
            Process.WriteMemory(Address, buf);
        }

        void IWritableDataProxy.Write(object value) {
            Write((ulong)value);
        }

        public PointerProxy<TProxy> ReinterpretCast<TProxy>(bool polymorphic = true) 
            where TProxy : IDataProxy {
            return new PointerProxy<TProxy>(Process, Address, polymorphic);
        }
    }

    [DebuggerDisplay("& {Read()}")]
    internal struct PointerProxy<TProxy> : IWritableDataProxy<TProxy>
        where TProxy : IDataProxy {

        public DkmProcess Process { get; private set; }
        public ulong Address { get; private set; }

        private readonly bool _polymorphic;

        public PointerProxy(DkmProcess process, ulong address)
            : this(process, address, true) {
        }

        public PointerProxy(DkmProcess process, ulong address, bool polymorphic)
            : this() {
            Debug.Assert(process != null && address != 0);
            Process = process;
            Address = address;
            _polymorphic = polymorphic;
        }

        public long ObjectSize {
            get { return Process.GetPointerSize(); }
        }

        public bool IsNull {
            get { return Raw.IsNull; }
        }

        /// <summary>
        /// Returns an untyped <see cref="PointerProxy"/> for the same memory location.
        /// </summary>
        public PointerProxy Raw {
            get { return new PointerProxy(Process, Address); }
        }

        public TProxy Read() {
            if (IsNull) {
                Debug.Fail("Trying to dereference a null PointerProxy.");
                throw new InvalidOperationException();
            }

            var ptr = Raw.Read();
            return DataProxy.Create<TProxy>(Process, ptr, _polymorphic);
        }

        /// <summary>
        /// Like <see cref="Read"/>, but returns default(<see cref="TProxy"/>) if pointer is null.
        /// </summary>
        public TProxy TryRead() {
            if (IsNull) {
                return default(TProxy);
            }

            var ptr = Raw.Read();
            return DataProxy.Create<TProxy>(Process, ptr, _polymorphic);
        }

        object IValueStore.Read() {
            return Read();
        }

        public void Write(TProxy value) {
            Raw.Write(value.Address);
        }

        void IWritableDataProxy.Write(object value) {
            Write((TProxy)value);
        }

        public PointerProxy<TOtherProxy> ReinterpretCast<TOtherProxy>(bool polymorphic = true)
            where TOtherProxy : IDataProxy {
            return new PointerProxy<TOtherProxy>(Process, Address, polymorphic);
        }

#if DEBUG
        // This exists solely for convenience of debugging, to automatically show dereferenced values of struct types in expression windows.
        private TProxy _Pointee {
            get { return Read(); }
        }
#endif
    }
}
