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
