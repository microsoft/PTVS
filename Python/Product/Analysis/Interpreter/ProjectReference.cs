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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Encapsulates information about a project reference.
    /// 
    /// Project references consist of a name and a kind.  Based upon the kind
    /// you can decode the information in the name which is typically a filename.
    /// </summary>
    public class ProjectReference : IEquatable<ProjectReference> {
        private readonly string _referenceName;
        private readonly ProjectReferenceKind _kind;

        public ProjectReference(string referenceName, ProjectReferenceKind kind) {
            _referenceName = referenceName;
            _kind = kind;
        }

        public string Name {
            get {
                return _referenceName;
            }
        }

        public ProjectReferenceKind Kind {
            get {
                return _kind;
            }
        }

        public override int GetHashCode() {
            return _kind.GetHashCode() ^ _referenceName.GetHashCode();
        }

        public override bool Equals(object obj) {
            ProjectReference other = obj as ProjectReference;
            if (other != null) {
                return this.Equals(other);
            }
            return false;
        }

        #region IEquatable<ProjectReference> Members

        public virtual bool Equals(ProjectReference other) {
            if (other.Kind != Kind) {
                return false;
            }

            switch (Kind) {
                case ProjectReferenceKind.Assembly:
                case ProjectReferenceKind.ExtensionModule:
                    return String.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase);
                default:
                    return String.Equals(other.Name, Name, StringComparison.Ordinal);
            }
        }

        #endregion
    }
}
