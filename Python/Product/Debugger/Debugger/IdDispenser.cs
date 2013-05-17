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

namespace Microsoft.PythonTools.Debugger {
    class IdDispenser {
        private readonly List<int> _freedInts = new List<int>();
        private int _curValue;

        public int Allocate() {
            lock (this) {
                if (_freedInts.Count > 0) {
                    int res = _freedInts[_freedInts.Count - 1];
                    _freedInts.Remove(_freedInts.Count - 1);
                    return res;
                } else {
                    int res = _curValue++;
                    return res;
                }
            }
        }

        public void Free(int id) {
            lock (this) {
                if (id + 1 == _curValue) {
                    _curValue--;
                } else {
                    _freedInts.Add(id);
                }
            }
        }
    }
}
