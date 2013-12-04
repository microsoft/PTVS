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

namespace Microsoft.PythonTools.Django.TemplateParsing {
    struct TemplateToken : IEquatable<TemplateToken> {
        internal readonly TemplateTokenKind Kind;
        internal readonly int Start, End;
        internal readonly bool IsClosed;

        public TemplateToken(TemplateTokenKind kind, int start, int end, bool isClosed = true) {
            Kind = kind;
            Start = start;
            End = end;
            IsClosed = isClosed;
        }

        public override bool Equals(object obj) {
            if (obj is TemplateToken) {
                return Equals((TemplateToken)obj);
            }
            return false;
        }

        public override int GetHashCode() {
            return Kind.GetHashCode() ^ Start ^ End ^ IsClosed.GetHashCode();
        }

        #region IEquatable<TemplateToken> Members

        public bool Equals(TemplateToken other) {
            return Kind == other.Kind &&
                Start == other.Start &&
                End == other.End &&
                IsClosed == other.IsClosed;
        }

        #endregion
    }
}
