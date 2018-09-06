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

using System.Collections.Generic;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    public sealed class AnalysisLimits {
        /// <summary>
        /// Returns a new set of limits, set to the defaults for analyzing user
        /// projects.
        /// </summary>
        public static AnalysisLimits GetDefaultLimits() {
            return new AnalysisLimits();
        }

        /// <summary>
        /// Returns a new set of limits, set to the default for analyzing a
        /// standard library.
        /// </summary>
        public static AnalysisLimits GetStandardLibraryLimits() {
            var limits = new AnalysisLimits();
            limits.CrossModule = 0;
            limits.CallDepth = 2;
            limits.DecreaseCallDepth = 20;
            limits.NormalArgumentTypes = 10;
            limits.ListArgumentTypes = 5;
            limits.DictArgumentTypes = 5;
            limits.ReturnTypes = 10;
            limits.YieldTypes = 10;
            limits.InstanceMembers = 5;
            limits.DictKeyTypes = 5;
            limits.DictValueTypes = 10;
            limits.IndexTypes = 5;
            limits.AssignedTypes = 20;
            limits.UnifyCallsToNew = true;
            limits.ProcessCustomDecorators = true;
            return limits;
        }

#if DESKTOP

#endif

        /// <summary>
        /// The key to use with ProjectEntry.Properties to override the call
        /// depth for functions in that module.
        /// </summary>
        internal static readonly object CallDepthKey = new object();

        public AnalysisLimits() {
            CrossModule = 0;
            CallDepth = 3;
            DecreaseCallDepth = 30;
            NormalArgumentTypes = 10;
            ListArgumentTypes = 6;
            DictArgumentTypes = 6;
            ReturnTypes = 10;
            YieldTypes = 5;
            InstanceMembers = 10;
            DictKeyTypes = 5;
            DictValueTypes = 10;
            IndexTypes = 6;
            AssignedTypes = 30;
            UnifyCallsToNew = true;
            ProcessCustomDecorators = true;
            UseTypeStubPackages = true;
            UseTypeStubPackagesExclusively = false;
        }

        /// <summary>
        /// The maximum number of files which will be used for cross module
        /// analysis.
        /// 
        /// If less than zero, cross module analysis will not be limited.
        /// Otherwise, cross module analysis will be disabled after the
        /// specified number of files have been loaded.
        /// </summary>
        public int CrossModule { get; set; }

        /// <summary>
        /// The initial call stack depth to compare for reusing function
        /// analysis units.
        /// 
        /// The minimum value (1) will produce one unit for each call site.
        /// Higher values take the callers of the function containing the call
        /// site into account. Calls outside of functions are unaffected.
        /// 
        /// This value cannot vary during analysis.
        /// </summary>
        public int CallDepth { get; set; }

        /// <summary>
        /// The number of calls to a function at which to decrement the depth
        /// used to distinguish calls to that function.
        /// 
        /// This value applies to all new calls following the last decrement.
        /// It is permitted to vary during analysis.
        /// </summary>
        public int DecreaseCallDepth { get; set; }

        /// <summary>
        /// The number of types in a normal argument at which to start
        /// combining similar types.
        /// </summary>
        public int NormalArgumentTypes { get; set; }

        /// <summary>
        /// The number of types in a list argument at which to start combining
        /// similar types.
        /// </summary>
        public int ListArgumentTypes { get; set; }

        /// <summary>
        /// The number of types in a dict argument at which to start combining
        /// similar types.
        /// </summary>
        public int DictArgumentTypes { get; set; }

        /// <summary>
        /// The number of types in a return value at which to start combining
        /// similar types.
        /// </summary>
        public int ReturnTypes { get; set; }

        /// <summary>
        /// The number of types in a yielded value at which to start combining
        /// similar types.
        /// </summary>
        public int YieldTypes { get; set; }

        /// <summary>
        /// The number of types in an instance attribute at which to start
        /// combining similar types.
        /// </summary>
        public int InstanceMembers { get; set; }

        /// <summary>
        /// True if calls to '__new__' should not be distinguished based on the
        /// call site. This applies to both implicit and explicit calls for
        /// user-defined '__new__' functions.
        /// </summary>
        public bool UnifyCallsToNew { get; set; }

        /// <summary>
        /// The number of keys in a dictionary at which to start combining
        /// similar types.
        /// </summary>
        public int DictKeyTypes { get; set; }

        /// <summary>
        /// The number of values in a dictionary entry at which to start
        /// combining similar types. Note that this applies to each value in a
        /// dictionary, not to all values at once.
        /// </summary>
        public int DictValueTypes { get; set; }

        /// <summary>
        /// The number of values in a collection at which to start combining
        /// similar types. This does not apply to dictionaries.
        /// </summary>
        public int IndexTypes { get; set; }

        /// <summary>
        /// The number of values in a normal variable at which to start
        /// combining similar types. This is only applied by assignment
        /// analysis.
        /// </summary>
        public int AssignedTypes { get; set; }

        /// <summary>
        /// True to evaluate custom decorators. If false, all decorators are
        /// assumed to return the original function unmodified.
        /// </summary>
        public bool ProcessCustomDecorators { get; set; }

        /// <summary>
        /// True to read information from type stub packages.
        /// </summary>
        public bool UseTypeStubPackages { get; set; }

        /// <summary>
        /// When both this value and <see cref="UseTypeStubPackages"/> are
        /// true, omits regular analysis when a matching type stub package is
        /// found.
        /// </summary>
        public bool UseTypeStubPackagesExclusively { get; set; }
    }
}
