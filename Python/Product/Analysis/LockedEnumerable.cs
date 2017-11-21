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

using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.PythonTools.Analysis {
    static class LockedEnumerable {
        public static IEnumerable<T> AsLockedEnumerable<T>(this IEnumerable<T> source) {
            return new LockedEnumerableObject<T>(source, source);
        }

        public static IEnumerable<T> AsLockedEnumerable<T>(this IEnumerable<T> source, object lockObject) {
            return new LockedEnumerableObject<T>(source, lockObject);
        }

        class LockedEnumerableObject<T> : IEnumerable<T> {
            private readonly IEnumerable<T> _source;
            private readonly object _lockObject;

            public LockedEnumerableObject(IEnumerable<T> source, object lockObject) {
                _source = source;
                _lockObject = lockObject;
            }

            public IEnumerator<T> GetEnumerator() {
                bool lockTaken = false;
                bool enumeratorReturned = false;

                try {
                    if (_lockObject == null) {
                        lockTaken = false;
                    } else {
                        Monitor.Enter(_lockObject, ref lockTaken);
                    }

                    var enumerator = new LockedEnumeratorObject<T>(_source.GetEnumerator(), _lockObject);
                    enumeratorReturned = true;
                    return enumerator;
                } finally {
                    if (lockTaken && !enumeratorReturned) {
                        Monitor.Exit(_lockObject);
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        struct LockedEnumeratorObject<T> : IEnumerator<T> {
            private readonly IEnumerator<T> _source;
            private readonly object _lockObject;

            public LockedEnumeratorObject(IEnumerator<T> source, object lockObject) {
                _source = source;
                _lockObject = lockObject;
            }

            public T Current {
                get {
                    return _source.Current;
                }
            }

            object IEnumerator.Current {
                get {
                    return ((IEnumerator)_source).Current;
                }
            }

            public void Dispose() {
                try {
                    _source.Dispose();
                } finally {
                    if (_lockObject != null) {
                        Monitor.Exit(_lockObject);
                    }
                }
            }

            public bool MoveNext() {
                return _source.MoveNext();
            }

            public void Reset() {
                _source.Reset();
            }
        }
    }
}
