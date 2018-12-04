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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Infrastructure {
    static class COMEnumerable {
        public delegate int NextMethod<T>(uint count, T[] result, out uint fetched);
        public delegate int IntPtrNextMethod(uint count, IntPtr[] result, out uint fetched);
        public delegate int ResetMethod();

        public static List<T> ToList<T>(NextMethod<T> next) {
            var res = new List<T>();
            using (var e = new COMEnumerator<T>(next, null)) {
                while (e.MoveNext()) {
                    res.Add(e.Current);
                }
            }
            return res;
        }

        public static List<T> ToList<T>(IntPtrNextMethod next) {
            var res = new List<T>();
            using (var e = new COMEnumerator<T>(next, null)) {
                while (e.MoveNext()) {
                    res.Add(e.Current);
                }
            }
            return res;
        }
    }

    sealed class COMEnumerator<T> : IEnumerator<T> {
        private T _current;
        private readonly Queue<T> _buffer = new Queue<T>();
        private readonly COMEnumerable.IntPtrNextMethod _nextPtr;
        private readonly COMEnumerable.NextMethod<T> _next;
        private readonly COMEnumerable.ResetMethod _reset;

        public COMEnumerator(COMEnumerable.IntPtrNextMethod next, COMEnumerable.ResetMethod reset) {
            _nextPtr = next;
            _reset = reset;
        }

        public COMEnumerator(COMEnumerable.NextMethod<T> next, COMEnumerable.ResetMethod reset) {
            _next = next;
            _reset = reset;
        }

        public void Dispose() { }

        private void FillStructBufferFromPtr() {
            var data = new IntPtr[4];
            uint fetched;

            try {
                for (int i = 0; i < data.Length; ++i) {
                    data[i] = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(T)));
                }

                Marshal.ThrowExceptionForHR(_nextPtr((uint)data.Length, data, out fetched));

                for (uint i = 0; i < fetched; ++i) {
                    T obj = (T)Marshal.PtrToStructure(data[i], typeof(T));
                    _buffer.Enqueue(obj);
                }
            } finally {
                for (int i = 0; i < data.Length; ++i) {
                    if (data[i] != IntPtr.Zero) {
                        Marshal.FreeCoTaskMem(data[i]);
                    }
                }
            }
        }

        private void FillBufferFromPtr() {
            var data = new IntPtr[4];
            uint fetched;
            Marshal.ThrowExceptionForHR(_nextPtr((uint)data.Length, data, out fetched));

            for (uint i = 0; i < fetched; ++i) {
                var obj = (T)Marshal.GetObjectForIUnknown(data[i]);
                _buffer.Enqueue(obj);
            }
        }

        private void FillBuffer() {
            if (_nextPtr != null) {
                if (typeof(T).IsValueType) {
                    FillStructBufferFromPtr();
                } else {
                    FillBufferFromPtr();
                }
                return;
            }

            var data = new T[16];
            uint fetched;
            Marshal.ThrowExceptionForHR(_next((uint)data.Length, data, out fetched));

            for (uint i = 0; i < fetched; ++i) {
                _buffer.Enqueue(data[i]);
            }
        }

        public bool MoveNext() {
            lock (_buffer) {
                if (_buffer.Count == 0) {
                    FillBuffer();
                }
                if (_buffer.Count >= 1) {
                    _current = _buffer.Dequeue();
                    return true;
                }

                _current = default(T);
                return false;
            }
        }

        public void Reset() {
            if (_reset != null) {
                Marshal.ThrowExceptionForHR(_reset());
            } else {
                throw new NotSupportedException();
            }
        }

        public T Current => _current;
        object IEnumerator.Current => _current;
    }
}
