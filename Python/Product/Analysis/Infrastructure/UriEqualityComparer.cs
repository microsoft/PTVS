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

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    sealed class UriEqualityComparer : IEqualityComparer<Uri> {
        private readonly UriComponents _components;

        public static readonly IEqualityComparer<Uri> Default = new UriEqualityComparer(UriComponents.SchemeAndServer | UriComponents.PathAndQuery);
        public static readonly IEqualityComparer<Uri> IncludeFragment = new UriEqualityComparer(UriComponents.SchemeAndServer | UriComponents.PathAndQuery | UriComponents.Fragment);

        public UriEqualityComparer(UriComponents components) {
            _components = components;
        }

        public bool Equals(Uri x, Uri y) {
            return x?.GetComponents(_components, UriFormat.Unescaped) ==
                y?.GetComponents(_components, UriFormat.Unescaped);
        }

        public int GetHashCode(Uri obj) {
            return obj?.GetComponents(_components, UriFormat.Unescaped).GetHashCode() ?? 0;
        }
    }
}
