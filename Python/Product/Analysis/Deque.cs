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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Analysis {
    class Deque<T> : IEnumerable, ICollection {
        private T[] _data;
        private int _head, _tail;
        private int _itemCnt, _version;
        private static IEqualityComparer<T> _comparer = EqualityComparer<T>.Default;

        public Deque() {
#if DEBUG
            _scope = AnalysisQueue.Current;
#endif
            Clear();
        }

#if DEBUG
        private readonly AnalysisQueue _scope;
        private void AssertScope() {
            Debug.Assert(_scope == AnalysisQueue.Current);
        }
#else
        [Conditional("DEBUG")]
        private void AssertScope() { }
#endif


        #region core deque APIs

        public void Append(T x) {
            AssertScope();

            _version++;

            if (_itemCnt == _data.Length) {
                GrowArray();
            }

            _itemCnt++;
            _data[_tail++] = x;
            if (_tail == _data.Length) {
                _tail = 0;
            }
        }

        public void AppendLeft(T x) {
            AssertScope();

            _version++;

            if (_itemCnt == _data.Length) {
                GrowArray();
            }

            _itemCnt++;
            --_head;
            if (_head < 0) {
                _head = _data.Length - 1;
            }

            _data[_head] = x;
        }

        public void Clear() {
            AssertScope();

            _version++;

            _head = _tail = 0;
            _itemCnt = 0;
            _data = new T[8];
        }

        public T Pop() {
            AssertScope();

            if (_itemCnt == 0) {
                throw new InvalidOperationException("pop from an empty deque");
            }

            _version++;
            if (_tail != 0) {
                _tail--;
            } else {
                _tail = _data.Length - 1;
            }
            _itemCnt--;

            T res = _data[_tail];
            _data[_tail] = default(T);
            return res;
        }

        public T PopLeft() {
            AssertScope();

            if (_itemCnt == 0) {
                throw new InvalidOperationException("pop from an empty deque");
            }

            _version++;
            T res = _data[_head];
            _data[_head] = default(T);

            if (_head != _data.Length - 1) {
                _head++;
            } else {
                _head = 0;
            }
            _itemCnt--;
            return res;
        }

        public void Remove(T value) {
            AssertScope();

            int found = -1;
            int startVersion = _version;
            WalkDeque(delegate(int index) {
                if (_comparer.Equals(_data[index], value)) {
                    found = index;
                    return false;
                }
                return true;
            });
            if (_version != startVersion) {
                throw new InvalidOperationException("deque mutated during remove().");
            }

            if (found == _head) {
                PopLeft();
            } else if (found == (_tail > 0 ? _tail - 1 : _data.Length - 1)) {
                Pop();
            } else if (found == -1) {
                throw new ArgumentException("deque.remove(value): value not in deque");
            } else {
                // otherwise we're removing from the middle and need to slide the values over...
                _version++;

                int start;
                if (_head >= _tail) {
                    start = 0;
                } else {
                    start = _head;
                }

                bool finished = false;
                T copying = _tail != 0 ? _data[_tail - 1] : _data[_data.Length - 1];
                for (int i = _tail - 2; i >= start; i--) {
                    T tmp = _data[i];
                    _data[i] = copying;
                    if (i == found) {
                        finished = true;
                        break;
                    }
                    copying = tmp;
                }
                if (_head >= _tail && !finished) {
                    for (int i = _data.Length - 1; i >= _head; i--) {
                        T tmp = _data[i];
                        _data[i] = copying;
                        if (i == found) break;
                        copying = tmp;
                    }
                }

                // we're one smaller now
                _tail--;
                _itemCnt--;
                if (_tail < 0) {
                    // and tail just wrapped to the beginning
                    _tail = _data.Length - 1;
                }
            }

        }

        public T this[int index] {
            get {

                return _data[IndexToSlot(index)];
            }
            set {
                _version++;
                _data[IndexToSlot(index)] = value;
            }
        }
        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new DequeIterator(this);
        }

        private sealed class DequeIterator : IEnumerable, IEnumerator {
            private readonly Deque<T> _deque;
            private int _curIndex, _moveCnt, _version;

            public DequeIterator(Deque<T> d) {
                _deque = d;
                _curIndex = d._head - 1;
                _version = d._version;
            }

            #region IEnumerator Members

            object IEnumerator.Current {
                get {
                    return _deque._data[_curIndex];
                }
            }

            bool IEnumerator.MoveNext() {

                if (_version != _deque._version) {
                    throw new InvalidOperationException("deque mutated during iteration");
                }

                if (_moveCnt < _deque._itemCnt) {
                    _curIndex++;
                    _moveCnt++;
                    if (_curIndex == _deque._data.Length) {
                        _curIndex = 0;
                    }
                    return true;
                }
                return false;

            }

            void IEnumerator.Reset() {
                _moveCnt = 0;
                _curIndex = _deque._head - 1;
            }

            #endregion

            #region IEnumerable Members

            public IEnumerator GetEnumerator() {
                return this;
            }

            #endregion
        }

        #endregion


        #region private members

        private void GrowArray() {
            T[] newData = new T[_data.Length * 2];

            // make the array completely sequential again
            // by starting head back at 0.
            int cnt1, cnt2;
            if (_head >= _tail) {
                cnt1 = _data.Length - _head;
                cnt2 = _data.Length - cnt1;
            } else {
                cnt1 = _tail - _head;
                cnt2 = _data.Length - cnt1;
            }

            Array.Copy(_data, _head, newData, 0, cnt1);
            Array.Copy(_data, 0, newData, cnt1, cnt2);

            _head = 0;
            _tail = _data.Length;
            _data = newData;
        }

        private int IndexToSlot(int intIndex) {
            if (_itemCnt == 0) {
                throw new IndexOutOfRangeException("deque index out of range");
            }

            if (intIndex >= 0) {
                if (intIndex >= _itemCnt) {
                    throw new IndexOutOfRangeException("deque index out of range");
                }

                int realIndex = _head + intIndex;
                if (realIndex >= _data.Length) {
                    realIndex -= _data.Length;
                }

                return realIndex;
            } else {
                if ((intIndex * -1) > _itemCnt) {
                    throw new IndexOutOfRangeException("deque index out of range");
                }

                int realIndex = _tail + intIndex;
                if (realIndex < 0) {
                    realIndex += _data.Length;
                }

                return realIndex;
            }
        }

        private delegate bool DequeWalker(int curIndex);

        /// <summary>
        /// Walks the queue calling back to the specified delegate for
        /// each populated index in the queue.
        /// </summary>
        private void WalkDeque(DequeWalker walker) {
            if (_itemCnt != 0) {
                int end;
                if (_head >= _tail) {
                    end = _data.Length;
                } else {
                    end = _tail;
                }

                for (int i = _head; i < end; i++) {
                    if (!walker(i)) {
                        return;
                    }
                }
                if (_head >= _tail) {
                    for (int i = 0; i < _tail; i++) {
                        if (!walker(i)) {
                            return;
                        }
                    }
                }
            }
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) {
            int i = 0;
            foreach (object o in this) {
                array.SetValue(o, index + i++);
            }
        }

        public int Count {
            get { return this._itemCnt; }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return this; }
        }

        #endregion
    }
}
