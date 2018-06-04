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
using System.Diagnostics;

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
    struct EncodedLocation : IEquatable<EncodedLocation>, ICanExpire {
        public readonly ILocationResolver Resolver;
        public readonly object Location;

        public EncodedLocation(ILocationResolver resolver, object location) {
            if (resolver == null && !(location is LocationInfo)) {
                throw new ArgumentNullException(nameof(resolver));
            }

            for (var r = resolver; r != null; r = r.GetAlternateResolver()) {
                resolver = r;
            }
            Debug.Assert(resolver != null);

            Resolver = resolver;
            Location = location;
        }

        public bool IsAlive => (Resolver as ICanExpire)?.IsAlive ?? true;

        public override int GetHashCode() {
            return (Resolver?.GetHashCode() ?? 0) ^ (Location?.GetHashCode() ?? 0);
        }

        public override bool Equals(object obj) {
            if (obj is EncodedLocation) {
                return Equals((EncodedLocation)obj);
            }
            return false;
        }

        #region IEquatable<EncodedLocation> Members

        public bool Equals(EncodedLocation other) {
            return Location == other.Location;
        }

        #endregion

        public LocationInfo GetLocationInfo() {
            if (Resolver == null) {
                return Location as LocationInfo;
            }
            return Resolver.ResolveLocation(Location);
        }
    }
}
