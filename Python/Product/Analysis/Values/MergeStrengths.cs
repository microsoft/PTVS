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

namespace Microsoft.PythonTools.Analysis.Values {
    static class MergeStrength {
        /// <summary>
        /// Override normal BII handling when below this strength. This allows
        /// iterable types from being merged 
        /// </summary>
        public const int IgnoreIterableNode = 1;

        /// <summary>
        /// <para>CI + CI => first common MRO entry that is not BCI(object)</para>
        /// <para>CI + BCI => BCI if in the CI's MRO and is not BCI(object)</para>
        /// <para>BCI + CI => BCI if in the CI's MRO and is not BCI(object)</para>
        /// <para>BCI + BCI => KnownTypes[TypeId] if type IDs match</para>
        /// <para>II + II => instance of CI+CI merge result</para>
        /// <para>II + BII => instance of CI+BII merge result</para>
        /// <para>BII + II => instance of BCI+CI merge result</para>
        /// <para>BII + BII => instance of BCI+BCI merge result</para>
        /// </summary>
        public const int ToBaseClass = 1;

        /// <summary>
        /// <para>CI + CI => BII(type)</para>
        /// <para>CI + BCI => BII(type)</para>
        /// <para>BCI + BCI => BII(type)</para>
        /// <para>CI + BII(type) => BII(type)</para>
        /// <para>BCI + BII(type) => BII(type)</para>
        ///
        /// <para>II + II => BII(object)</para>
        /// <para>II + BII => BII(object)</para>
        /// <para>BII + II => BII(object)</para>
        /// <para>BII + BII => BII(object)</para>
        /// <para>II + BII(None) => do not merge</para>
        /// <para>BII + BII(None) => do not merge</para>
        ///
        /// <para>FI + FI => BII(function)</para>
        /// <para>FI + BII(function) => BII(function)</para>
        /// </summary>
        public const int ToObject = 3;
    }
}
