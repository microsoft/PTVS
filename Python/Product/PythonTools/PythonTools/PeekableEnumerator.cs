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

using System.Collections.Generic;

namespace Microsoft.PythonTools {
    /// <summary>
    /// An enumerator that allows one item either side of the current element to
    /// be previewed.
    /// </summary>
    sealed class PeekableEnumerator<T> : IEnumerator<T> {
        private readonly IEnumerable<T> _enumerable;
        private IEnumerator<T> _enumerator;

        public PeekableEnumerator(IEnumerable<T> enumerable) {
            _enumerable = enumerable;
            Reset();
        }

        public void Reset() {
            _enumerator = _enumerable.GetEnumerator();

            HasPrevious = false;
            Previous = default(T);

            HasCurrent = false;
            Current = default(T);

            HasNext = _enumerator.MoveNext();
            Next = HasNext ? _enumerator.Current : default(T);
        }

        public T Previous { get; private set; }
        public T Current { get; private set; }
        public T Next { get; private set; }

        public bool HasPrevious { get; private set; }
        public bool HasCurrent { get; private set; }
        public bool HasNext { get; private set; }

        public void Dispose() {
            _enumerator.Dispose();
        }

        object System.Collections.IEnumerator.Current {
            get { return (object)Current; }
        }

        public bool MoveNext() {
            HasPrevious = HasCurrent;
            Previous = Current;

            HasCurrent = HasNext;
            Current = Next;

            HasNext = HasCurrent && _enumerator.MoveNext();
            Next = HasNext ? _enumerator.Current : default(T);

            return HasCurrent;
        }
    }
}
