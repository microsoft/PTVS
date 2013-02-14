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

namespace Microsoft.PythonTools.Analysis {
    public class AnalysisLimits {
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
            limits.CallDepth = 1;
            limits.NormalArgumentTypes = 10;
            limits.ListArgumentTypes = 5;
            limits.DictArgumentTypes = 5;
            limits.ReturnTypes = 10;
            limits.YieldTypes = 10;
            limits.InstanceMembers = 5;
            limits.UnifyCallsToNew = true;
            return limits;
        }

        public AnalysisLimits() {
            CallDepth = 3;
            NormalArgumentTypes = 50;
            ListArgumentTypes = 20;
            DictArgumentTypes = 20;
            ReturnTypes = 20;
            YieldTypes = 20;
            InstanceMembers = 50;
        }

        /// <summary>
        /// The maximum number of files which will be used for cross module
        /// analysis.
        /// 
        /// If null, cross module analysis will not be limited. Otherwise, a
        /// value will cause cross module analysis to be disabled after that
        /// number of files have been loaded.
        /// </summary>
        public int? CrossModule { get; set; }

        /// <summary>
        /// The call stack depth to compare for reusing function analysis
        /// units.
        /// 
        /// The minimum value (1) will produce one unit for each call site.
        /// Higher values take the callers of the function containing the call
        /// site into account. Calls outside of functions are unaffected.
        /// </summary>
        public int CallDepth { get; set; }

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
    }
}
