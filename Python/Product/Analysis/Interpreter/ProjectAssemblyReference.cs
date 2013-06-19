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
using System.Reflection;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class ProjectAssemblyReference : ProjectReference, IEquatable<ProjectAssemblyReference> {
        private readonly AssemblyName _asmName;

        public ProjectAssemblyReference(AssemblyName assemblyName, string filename)
            : base(filename, ProjectReferenceKind.Assembly) {
                _asmName = assemblyName;
        }

        public AssemblyName AssemblyName {
            get {
                return _asmName;
            }
        }

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
