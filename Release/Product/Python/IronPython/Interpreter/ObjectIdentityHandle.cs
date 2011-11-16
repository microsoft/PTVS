using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;

namespace Microsoft.IronPythonTools.Interpreter {
    /// <summary>
    /// Represents an object in a remote domain whos identity has been captured.
    /// 
    /// Comparison of the handles compares object identity.  It is the responsibility
    /// of the consumer of the object identity handle to make sure that they are comparing
    /// only handles that came from the same source, otherwise the identities could bleed
    /// across sources and compare incorrectly.
    /// </summary>
    [Serializable]
    struct ObjectIdentityHandle : IEquatable<ObjectIdentityHandle> {
        private readonly int _identity;

        public ObjectIdentityHandle(int identity) {
            _identity = identity;
        }

        public bool IsNull {
            get {
                return _identity == 0;
            }
        }

        public int Id {
            get {
                return _identity;
            }
        }

        public override bool Equals(object obj) {
            if (obj is ObjectIdentityHandle) {
                return this.Equals((ObjectIdentityHandle)obj);
            }
            return false;
        }

        public override int GetHashCode() {
            return _identity;
        }

        #region IEquatable<ObjectIdentityHandle> Members

        public bool Equals(ObjectIdentityHandle other) {
            return other._identity == _identity;
        }

        #endregion
    }
}
