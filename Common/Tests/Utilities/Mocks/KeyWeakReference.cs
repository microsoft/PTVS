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

namespace TestUtilities.Mocks
{
    internal sealed class KeyWeakReference : WeakReference
    {
        public int HashCode { get; }

        public KeyWeakReference(object target) : base(target)
        {
            HashCode = target.GetHashCode();
        }

        public override int GetHashCode() => HashCode;

        public override bool Equals(object obj) => Equals(obj as KeyWeakReference);

        public bool Equals(KeyWeakReference other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (IsAlive && other.IsAlive)
            {
                return Equals(Target, other.Target);
            }

            return !IsAlive && !other.IsAlive && HashCode == other.HashCode;
        }
    }
}