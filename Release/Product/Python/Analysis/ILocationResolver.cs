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

using Microsoft.PythonTools.Parsing;

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
        LocationInfo ResolveLocation(IProjectEntry project, object location);
    }
}
