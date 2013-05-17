/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
