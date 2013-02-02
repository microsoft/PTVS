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

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteEnumDebug<T>
        where T : class {

        private readonly T _elem;
        private bool _done;

        public PythonRemoteEnumDebug(T elem = null) {
            this._elem = elem;
            Reset();
        }

        protected T Element {
            get { return _elem; }
        }

        public int GetCount(out uint pcelt) {
            pcelt = (_elem == null) ? 0u : 1u;
            return 0;
        }

        public int Next(uint celt, T[] rgelt, ref uint pceltFetched) {
            if (_done) {
                pceltFetched = 0;
                return 1;
            } else {
                pceltFetched = 1;
                rgelt[0] = _elem;
                _done = true;
                return 0;
            }
        }

        public int Reset() {
            _done = (_elem == null);
            return 0;
        }

        public int Skip(uint celt) {
            if (celt == 0) {
                return 0;
            } else if (_done) {
                return 1;
            } else {
                _done = true;
                return celt > 1 ? 1 : 0;
            }
        }
    }
}
