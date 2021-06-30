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

using System.Collections.Generic;

namespace TestUtilities.Mocks
{
    internal sealed class KeyWeakReferenceComparer : IEqualityComparer<object>
    {
        public int GetHashCode(object obj) => obj is KeyWeakReference key ? key.HashCode : obj.GetHashCode();

        bool IEqualityComparer<object>.Equals(object x, object y)
        {
            var xIsAlive = IsAlive(x, out var xTarget);
            var yIsAlive = IsAlive(y, out var yTarget);

            return xIsAlive
                ? yIsAlive && Equals(xTarget, yTarget)
                : !yIsAlive && x == y;
        }

        private static bool IsAlive(object obj, out object target)
        {
            if (obj is KeyWeakReference key)
            {
                target = key.Target;
                return key.IsAlive;
            }

            target = obj;
            return false;
        }
    }
}