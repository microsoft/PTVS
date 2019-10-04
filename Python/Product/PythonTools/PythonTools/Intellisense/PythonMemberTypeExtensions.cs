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

using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Intellisense {
    internal static class PythonMemberTypeExtensions {
        public static StandardGlyphGroup ToGlyphGroup(this PythonMemberType objectType) {
            StandardGlyphGroup group;
            switch (objectType) {
                case PythonMemberType.Class: group = StandardGlyphGroup.GlyphGroupClass; break;
                case PythonMemberType.Module: group = StandardGlyphGroup.GlyphGroupModule; break;
                case PythonMemberType.Property: group = StandardGlyphGroup.GlyphGroupProperty; break;
                case PythonMemberType.Instance: group = StandardGlyphGroup.GlyphGroupVariable; break;
                case PythonMemberType.Union: group = StandardGlyphGroup.GlyphGroupUnion; break;
                case PythonMemberType.Variable: group = StandardGlyphGroup.GlyphGroupVariable; break;
                case PythonMemberType.Generic: group = StandardGlyphGroup.GlyphGroupTemplate; break;
                case PythonMemberType.Unknown: group = StandardGlyphGroup.GlyphGroupUnknown; break;
                case PythonMemberType.Function:
                case PythonMemberType.Method:
                default:
                    group = StandardGlyphGroup.GlyphGroupMethod;
                    break;
            }
            return group;
        }
    }
}
