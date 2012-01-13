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
    /// Simple structure used to track positions coming from multiple formats w/o having
    /// to resolve the location until much later.  We store a location resolver which can
    /// turn a location object back into a a line and column number.  Usually the resolver
    /// will be a PythonAst instance and the Location will be some Node.  The PythonAst
    /// then provides the location and we don't have to turn an index into line/column
    /// during the analysis.
    /// 
    /// But there's also the XAML analysis which doesn't have a PythonAst and Node, instead
    /// it just has line/column info.  So it uses a singleton instance and boxes the Location
    /// as a SourceLocation.  Because it's the uncommon case the extra overhead there isn't
    /// as important.
    /// 
    /// This ultimately lets us track the line/column info in the same space as just
    /// storing the line/column info directly while still allowing multiple schemes
    /// to be used.
    /// </summary>
    struct EncodedLocation : IEquatable<EncodedLocation> {
        public readonly ILocationResolver Resolver;
        public readonly object Location;

        public EncodedLocation(ILocationResolver resolver, object location) {
            Resolver = resolver;
            Location = location;
        }

        public override int GetHashCode() {
            if (Location != null) {
                return Resolver.GetHashCode() ^ Location.GetHashCode();
            }

            return Resolver.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj is EncodedLocation) {
                return Equals((EncodedLocation)obj);
            }
            return false;
        }

        #region IEquatable<EncodedLocation> Members

        public bool Equals(EncodedLocation other) {
            return Resolver == other.Resolver &&
                Location == other.Location;
        }

        #endregion

        public LocationInfo GetLocationInfo(IProjectEntry project) {
            return Resolver.ResolveLocation(project, Location);
        }
    }
}
