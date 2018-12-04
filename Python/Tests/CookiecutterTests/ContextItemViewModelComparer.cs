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
using System.Collections;
using Microsoft.CookiecutterTools.ViewModel;

namespace CookiecutterTests {
    class ContextItemViewModelComparer : IComparer {
        public int Compare(object x, object y) {
            if (x == y) {
                return 0;
            }

            var a = x as ContextItemViewModel;
            var b = y as ContextItemViewModel;

            if (a == null) {
                return -1;
            }

            if (b == null) {
                return -1;
            }

            int res;
            res = a.Name.CompareTo(b.Name);
            if (res != 0) {
                return res;
            }

            res = a.Val.CompareTo(b.Val);
            if (res != 0) {
                return res;
            }

            res = a.Default.CompareTo(b.Default);
            if (res != 0) {
                return res;
            }

            res = a.Selector.CompareTo(b.Selector);
            if (res != 0) {
                return res;
            }

            res = SafeCompare(a.Description, b.Description);
            if (res != 0) {
                return res;
            }

            res = SafeCompare(a.Visible, b.Visible);
            if (res != 0) {
                return res;
            }

            return 0;
        }

        private int SafeCompare(IComparable a, IComparable b) {
            if (a == null) {
                return b == null ? 0 : -1;
            }

            return a.CompareTo(b);
        }
    }
}
