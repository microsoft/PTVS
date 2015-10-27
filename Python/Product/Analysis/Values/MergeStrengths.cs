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
