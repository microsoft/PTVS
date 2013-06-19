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
