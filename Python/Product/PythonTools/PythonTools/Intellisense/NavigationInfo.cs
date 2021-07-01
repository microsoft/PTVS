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

namespace Microsoft.PythonTools.Intellisense
{
    class NavigationInfo
    {
        public readonly string Name;
        public readonly SnapshotSpan Span;
        public readonly NavigationInfo[] Children;
        public readonly NavigationKind Kind;

        public static readonly NavigationInfo Empty = new NavigationInfo(null, NavigationKind.None, new SnapshotSpan(), Array.Empty<NavigationInfo>());

        public NavigationInfo(string name, NavigationKind kind, SnapshotSpan span, NavigationInfo[] children)
        {
            Name = name;
            Kind = kind;
            Span = span;
            Children = children;
        }
    }

    enum NavigationKind
    {
        None,
        Class,
        Function,
        StaticMethod,
        ClassMethod,
        Property
    }
}
