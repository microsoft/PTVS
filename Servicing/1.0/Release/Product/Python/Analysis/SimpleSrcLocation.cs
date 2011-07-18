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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Simple structure used to track a position in code w/ line and column info.
    /// </summary>
    struct SimpleSrcLocation : IEquatable<SimpleSrcLocation> {
        public readonly int Line, Column;

        public SimpleSrcLocation(int line, int column) {
            Line = line;
            Column = column;
        }

        public SimpleSrcLocation(SourceSpan sourceSpan) {
            Line = sourceSpan.Start.Line;
            Column = sourceSpan.Start.Column;
        }

        public override int GetHashCode() {
            return Line ^ Column;
        }

        public override bool Equals(object obj) {
            if (obj is SimpleSrcLocation) {
                return Equals((SimpleSrcLocation)obj);
            }
            return false;
        }

        #region IEquatable<SimpleSrcLocation> Members

        public bool Equals(SimpleSrcLocation other) {
            return Line == other.Line && Column == other.Column;
        }

        #endregion
    }
}
