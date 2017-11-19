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


namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Resolves a location object into the LocationInfo which we expose to the consumer
    /// of the analysis APIs.  This enables an efficient mechanism to track references
    /// during analysis which doesn't involve actually tracking all of the line number
    /// information directly.  Instead we can support different resolvers and location
    /// objects and only lazily turn them back into real line information.
    /// 
    /// See EncodedLocation for more information.
    /// </summary>
    interface ILocationResolver {
        LocationInfo ResolveLocation(object location);

        /// <summary>
        /// Returns an alternate resolver, or <c>null</c> if this is the
        /// best resolver to use.
        /// </summary>
        ILocationResolver GetAlternateResolver();
    }
}
