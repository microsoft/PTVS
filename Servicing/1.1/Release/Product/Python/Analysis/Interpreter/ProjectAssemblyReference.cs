using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
