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
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class ProjectAssemblyReference : ProjectReference, IEquatable<ProjectAssemblyReference> {
        private readonly AssemblyName _asmName;

        public ProjectAssemblyReference(AssemblyName assemblyName, string filename)
            : base(filename, ProjectReferenceKind.Assembly) {
                _asmName = assemblyName;
        }

        public AssemblyName AssemblyName => _asmName;

        public override int GetHashCode() {
            return base.GetHashCode() ^ _asmName.GetHashCode();
        }

        public override bool Equals(object obj) {
            ProjectAssemblyReference asmRef = obj as ProjectAssemblyReference;
            if (asmRef != null) {
                return Equals(asmRef);
            }
            return false;
        }

        public override bool Equals(ProjectReference other) {
            ProjectAssemblyReference asmRef = other as ProjectAssemblyReference;
            if (asmRef != null) {
                return Equals(asmRef);
            }
            return false;
        }

        #region IEquatable<ProjectAssemblyReference> Members

        public bool Equals(ProjectAssemblyReference other) {
            if (base.Equals(other)) {
                return other._asmName == this._asmName;
            }
            return false;
        }

        #endregion
    }
}
