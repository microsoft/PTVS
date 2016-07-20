// Visual Studio Shared Project
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

using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudioTools.Project {
    internal class EnumBSTR : IVsEnumBSTR {
        private readonly IEnumerable<string> _enumerable;
        private IEnumerator<string> _enumerator;

        public EnumBSTR(IEnumerable<string> items) {
            _enumerable = items;
            _enumerator = _enumerable.GetEnumerator();
        }

        public int Clone(out IVsEnumBSTR ppenum) {
            ppenum = new EnumBSTR(_enumerable);
            return VSConstants.S_OK;
        }

        public int GetCount(out uint pceltCount) {
            var coll = _enumerable as ICollection<string>;
            if (coll != null) {
                pceltCount = checked((uint)coll.Count);
                return VSConstants.S_OK;
            } else {
                pceltCount = 0;
                return VSConstants.E_NOTIMPL;
            }
        }

        public int Next(uint celt, string[] rgelt, out uint pceltFetched) {
            pceltFetched = 0;

            int i = 0;
            for (; i < celt && _enumerator.MoveNext(); ++i) {
                ++pceltFetched;
                rgelt[i] = _enumerator.Current;
            }
            for (; i < celt; ++i) {
                rgelt[i] = null;
            }

            return pceltFetched > 0 ? VSConstants.S_OK : VSConstants.S_FALSE;
        }

        public int Reset() {
            _enumerator = _enumerable.GetEnumerator();
            return VSConstants.S_OK;
        }

        public int Skip(uint celt) {
            while (celt != 0 && _enumerator.MoveNext()) {
                celt--;
            }
            return VSConstants.S_OK;
        }
    }
}
