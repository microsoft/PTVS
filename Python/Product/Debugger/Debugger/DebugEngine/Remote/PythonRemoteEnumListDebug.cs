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
using System.Collections.Generic;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Debugger.Remote {
    internal class PythonRemoteEnumListDebug<T>
        where T : class {

        private readonly IList<T> _elements;
        private int _index;

        public PythonRemoteEnumListDebug(IList<T> elem) {
            _elements = elem;
            Reset();
        }

        public int GetCount(out uint pcelt) {
            pcelt = (uint)_elements.Count;
            return VSConstants.S_OK;
        }

        public int Next(uint celt, T[] rgelt, ref uint pceltFetched) {
            uint actual = (uint)Math.Max(0, Math.Min(celt, _elements.Count - _index));

            for (var i = 0; i < actual; i++) {
                rgelt[i] = _elements[_index];
                _index++;
            }

            // If successful, returns S_OK. Returns S_FALSE if fewer than
            // the requested number of elements could be returned;
            // otherwise, returns an error code. 
            pceltFetched = actual;
            return actual == celt ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int Reset() {
            _index = 0;
            return VSConstants.S_OK;
        }

        public int Skip(uint celt) {
            if (_index + celt <= _elements.Count) {
                _index += (int)celt;
                return VSConstants.S_OK;
            } else {
                // If celt specifies a value greater than the number of remaining
                // elements, the enumeration is set to the end and S_FALSE is returned. 
                _index = _elements.Count;
                return VSConstants.S_FALSE;
            }
        }
    }
}
