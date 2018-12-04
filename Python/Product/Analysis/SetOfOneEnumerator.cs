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

namespace Microsoft.PythonTools.Analysis {

    class SetOfOneEnumerator<T> : IEnumerator<T> {
        private readonly T _value;
        private bool _enumerated;

        public SetOfOneEnumerator(T value) {
            _value = value;
        }

        #region IEnumerator<T> Members

        T IEnumerator<T>.Current {
            get { return _value; }
        }

        #endregion

        #region IDisposable Members

        void IDisposable.Dispose() {
        }

        #endregion

        #region IEnumerator Members

        object System.Collections.IEnumerator.Current {
            get { return _value; }
        }

        bool System.Collections.IEnumerator.MoveNext() {
            if (_enumerated) {
                return false;
            }
            _enumerated = true;
            return true;
        }

        void System.Collections.IEnumerator.Reset() {
            _enumerated = false;
        }

        #endregion
    }

}
