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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies
{
	[DebuggerDisplay("& {Read()}")]
	internal struct PointerProxy : IWritableDataProxy<ulong>
	{
		public DkmProcess Process { get; private set; }
		public ulong Address { get; private set; }

		public PointerProxy(DkmProcess process, ulong address)
			: this()
		{
			Debug.Assert(process != null && address != 0);
			Process = process;
			Address = address;
		}

		public long ObjectSize => Process.GetPointerSize();

		public bool IsNull => Read() == 0;

		public unsafe ulong Read()
		{
			ulong result;
			Process.ReadMemory(Address, DkmReadMemoryFlags.None, &result, Process.GetPointerSize());
			return result;
		}

		object IValueStore.Read()
		{
			return Read();
		}

		public void Write(ulong value)
		{
			byte[] buf = Process.Is64Bit() ? BitConverter.GetBytes(value) : BitConverter.GetBytes((uint)value);
			Process.WriteMemory(Address, buf);
		}

		void IWritableDataProxy.Write(object value)
		{
			Write((ulong)value);
		}

		public PointerProxy<TProxy> ReinterpretCast<TProxy>(bool polymorphic = true)
			where TProxy : IDataProxy
		{
			return new PointerProxy<TProxy>(Process, Address, polymorphic);
		}
	}

	[DebuggerDisplay("& {TryRead()}")]
	internal struct PointerProxy<TProxy> : IWritableDataProxy<TProxy>
		where TProxy : IDataProxy
	{

		public DkmProcess Process { get; private set; }
		public ulong Address { get; private set; }

		private readonly bool _polymorphic;

		public PointerProxy(DkmProcess process, ulong address)
			: this(process, address, true)
		{
		}

		public PointerProxy(DkmProcess process, ulong address, bool polymorphic)
			: this()
		{
			Debug.Assert(process != null && address != 0);
			Process = process;
			Address = address;
			_polymorphic = polymorphic;
		}

		public long ObjectSize => Process.GetPointerSize();

		public bool IsNull => Raw.IsNull;

		/// <summary>
		/// Returns an untyped <see cref="PointerProxy"/> for the same memory location.
		/// </summary>
		public PointerProxy Raw => new PointerProxy(Process, Address);

		public TProxy Read()
		{
			if (IsNull)
			{
				Debug.Fail("Trying to dereference a null PointerProxy.");
				throw new InvalidOperationException();
			}

			var ptr = Raw.Read();
			return DataProxy.Create<TProxy>(Process, ptr, _polymorphic);
		}

		/// <summary>
		/// Like <see cref="Read"/>, but returns default(<see cref="TProxy"/>) if pointer is null.
		/// </summary>
		public TProxy TryRead()
		{
			if (IsNull)
			{
				return default(TProxy);
			}

			var ptr = Raw.Read();
			return DataProxy.Create<TProxy>(Process, ptr, _polymorphic);
		}

		object IValueStore.Read()
		{
			return Read();
		}

		public void Write(TProxy value)
		{
			Raw.Write(value.Address);
		}

		void IWritableDataProxy.Write(object value)
		{
			Write((TProxy)value);
		}

		public PointerProxy<TOtherProxy> ReinterpretCast<TOtherProxy>(bool polymorphic = true)
			where TOtherProxy : IDataProxy
		{
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
