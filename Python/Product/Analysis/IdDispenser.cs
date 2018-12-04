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
using System.Collections.Generic;
using SRC = System.Runtime.CompilerServices;

namespace Microsoft.PythonTools.Analysis {
    static class IdDispenser {
        // The one and only comparer instance.
        private static readonly IEqualityComparer<object> _comparer = new WrapperComparer();
        private static Dictionary<object, object> _hashtable = new Dictionary<object, object>(_comparer);
        private static readonly Object _synchObject = new Object();  // The one and only global lock instance.
        // We do not need to worry about duplicates that to using long for unique Id.
        // It takes more than 100 years to overflow long on year 2005 hardware.
        private static long _currentId = 0; // Last unique Id we have given out.

        // cleanupId and cleanupGC are used for efficient scheduling of hashtable cleanups
        private static long _cleanupId; // currentId at the time of last cleanup
        private static int _cleanupGC; // GC.CollectionCount(0) at the time of last cleanup


        public static void Clear() {
            lock (_synchObject) {
                _hashtable.Clear();
                _currentId = 0;
            }
        }

        /// <summary>
        /// Given an ID returns the object associated with that ID.
        /// </summary>
        public static object GetObject(long id) {
            lock (_synchObject) {
                foreach (Wrapper w in _hashtable.Keys) {
                    if (w.Target != null) {
                        if (w.Id == id) return w.Target;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets a unique ID for an object if it has been assigned one.
        /// </summary>
        public static bool TryGetId(Object o, out long id) {
            if (o == null) {
                id = 0;
                return true;
            }

            lock (_synchObject) {
                // If the object exists then return its existing ID.
                object res;
                if (_hashtable.TryGetValue(o, out res)) {
                    id = ((Wrapper)res).Id;
                    return true;
                }
            }

            id = 0;
            return false;
        }

        /// <summary>
        /// Gets a unique ID for an object
        /// </summary>
        public static long GetId(Object o) {
            if (o == null)
                return 0;

            lock (_synchObject) {
                // If the object exists then return its existing ID.
                object res;
                if (_hashtable.TryGetValue(o, out res)) {
                    return ((Wrapper)res).Id;
                }

                long uniqueId = checked(++_currentId);

                long change = uniqueId - _cleanupId;

                // Cleanup the table if it is a while since we have done it last time.
                // Take the size of the table into account.
                if (change > 1234 + _hashtable.Count / 2) {
                    // It makes sense to do the cleanup only if a GC has happened in the meantime.
                    // WeakReferences can become zero only during the GC.
                    int currentGC = GC.CollectionCount(0);
                    if (currentGC != _cleanupGC) {
                        Cleanup();

                        _cleanupId = uniqueId;
                        _cleanupGC = currentGC;
                    } else {
                        _cleanupId += 1234;
                    }
                }
                Wrapper w = new Wrapper(o, uniqueId);
                _hashtable[w] = w;

                return uniqueId;
            }
        }

        /// <summary>
        /// Goes over the hashtable and removes empty entries 
        /// </summary>
        private static void Cleanup() {
            int liveCount = 0;
            int emptyCount = 0;

            foreach (Wrapper w in _hashtable.Keys) {
                if (w.Target != null)
                    liveCount++;
                else
                    emptyCount++;
            }

            // Rehash the table if there is a significant number of empty slots
            if (emptyCount > liveCount / 4) {
                Dictionary<object, object> newtable = new Dictionary<object, object>(liveCount + liveCount / 4, _comparer);

                foreach (Wrapper w in _hashtable.Keys) {
                    if (w.Target != null)
                        newtable[w] = w;
                }

                _hashtable = newtable;
            }
        }

        /// <summary>
        /// Weak-ref wrapper caches the weak reference, our hash code, and the object ID.
        /// </summary>
        private sealed class Wrapper {
            private WeakReference _weakReference;
            private int _hashCode;
            private long _id;

            public Wrapper(Object obj, long uniqueId) {
                _weakReference = new WeakReference(obj, true);

                _hashCode = (obj == null) ? 0 : SRC.RuntimeHelpers.GetHashCode(obj);
                _id = uniqueId;
            }

            public long Id {
                get {
                    return _id;
                }
            }

            public Object Target {
                get {
                    return _weakReference.Target;
                }
            }

            public override int GetHashCode() {
                return _hashCode;
            }
        }

        /// <summary>
        /// WrapperComparer treats Wrapper as transparent envelope 
        /// </summary>
        private sealed class WrapperComparer : IEqualityComparer<object> {
            bool IEqualityComparer<object>.Equals(Object x, Object y) {

                Wrapper wx = x as Wrapper;
                if (wx != null)
                    x = wx.Target;

                Wrapper wy = y as Wrapper;
                if (wy != null)
                    y = wy.Target;

                return Object.ReferenceEquals(x, y);
            }

            int IEqualityComparer<object>.GetHashCode(Object obj) {

                Wrapper wobj = obj as Wrapper;
                if (wobj != null)
                    return wobj.GetHashCode();

                return GetHashCodeWorker(obj);
            }

            private static int GetHashCodeWorker(object o) {
                if (o == null) return 0;
                return SRC.RuntimeHelpers.GetHashCode(o);
            }
        }
    }
}
