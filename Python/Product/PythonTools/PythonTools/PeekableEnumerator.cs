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
