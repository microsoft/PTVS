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

using Microsoft.Win32;

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
            limits.CallDepth = 2;
            limits.DecreaseCallDepth = 20;
            limits.NormalArgumentTypes = 10;
            limits.ListArgumentTypes = 5;
            limits.DictArgumentTypes = 5;
            limits.ReturnTypes = 10;
            limits.YieldTypes = 10;
            limits.InstanceMembers = 5;
            limits.DictKeyTypes = 5;
            limits.DictValueTypes = 20;
            limits.IndexTypes = 5;
            limits.AssignedTypes = 50;
            limits.UnifyCallsToNew = true;
            limits.ProcessCustomDecorators = true;
            return limits;
        }

        private const string CallDepthId = "CallDepth";
        private const string DecreaseCallDepthId = "DecreaseCallDepth";
        private const string NormalArgumentTypesId = "NormalArgumentTypes";
        private const string ListArgumentTypesId = "ListArgumentTypes";
        private const string DictArgumentTypesId = "DictArgumentTypes";
        private const string ReturnTypesId = "ReturnTypes";
        private const string YieldTypesId = "YieldTypes";
        private const string InstanceMembersId = "InstanceMembers";
        private const string DictKeyTypesId = "DictKeyTypes";
        private const string DictValueTypesId = "DictValueTypes";
        private const string IndexTypesId = "IndexTypes";
        private const string AssignedTypesId = "AssignedTypes";
        private const string UnifyCallsToNewId = "UnifyCallsToNew";
        private const string ProcessCustomDecoratorsId = "ProcessCustomDecorators";

        /// <summary>
        /// Loads a new instance from the specified registry key.
        /// </summary>
        /// <param name="key">
        /// The key to load settings from. Each setting is a DWORD value. If
        /// null, all settings are assumed to be unspecified and the default
        /// values are used.
        /// </param>
        /// <param name="defaultToStdLib">
        /// If True, unspecified settings are taken from the defaults for
        /// standard library analysis. Otherwise, they are taken from the
        /// usual defaults.
        /// </param>
        public static AnalysisLimits LoadFromStorage(RegistryKey key, bool defaultToStdLib = false) {
            var limits = defaultToStdLib ? GetStandardLibraryLimits() : new AnalysisLimits();

            if (key != null) {
                limits.CallDepth = (key.GetValue(CallDepthId) as int?) ?? limits.CallDepth;
                limits.DecreaseCallDepth = (key.GetValue(DecreaseCallDepthId) as int?) ?? limits.DecreaseCallDepth;
                limits.NormalArgumentTypes = (key.GetValue(NormalArgumentTypesId) as int?) ?? limits.NormalArgumentTypes;
                limits.ListArgumentTypes = (key.GetValue(ListArgumentTypesId) as int?) ?? limits.ListArgumentTypes;
                limits.DictArgumentTypes = (key.GetValue(DictArgumentTypesId) as int?) ?? limits.DictArgumentTypes;
                limits.ReturnTypes = (key.GetValue(ReturnTypesId) as int?) ?? limits.ReturnTypes;
                limits.YieldTypes = (key.GetValue(YieldTypesId) as int?) ?? limits.YieldTypes;
                limits.InstanceMembers = (key.GetValue(InstanceMembersId) as int?) ?? limits.InstanceMembers;
                limits.DictKeyTypes = (key.GetValue(DictKeyTypesId) as int?) ?? limits.DictKeyTypes;
                limits.DictValueTypes = (key.GetValue(DictValueTypesId) as int?) ?? limits.DictValueTypes;
                limits.IndexTypes = (key.GetValue(IndexTypesId) as int?) ?? limits.IndexTypes;
                limits.AssignedTypes = (key.GetValue(AssignedTypesId) as int?) ?? limits.AssignedTypes;
                limits.UnifyCallsToNew = ((key.GetValue(UnifyCallsToNewId) as int?) ?? (limits.UnifyCallsToNew ? 1 : 0)) != 0;
                limits.ProcessCustomDecorators = ((key.GetValue(ProcessCustomDecoratorsId) as int?) ?? (limits.ProcessCustomDecorators ? 1 : 0)) != 0;
            }

            return limits;
        }

        /// <summary>
        /// Saves the current instance's settings to the specified registry key.
        /// </summary>
        public void SaveToStorage(RegistryKey key) {
            key.SetValue(CallDepthId, CallDepth, RegistryValueKind.DWord);
            key.SetValue(DecreaseCallDepthId, DecreaseCallDepth, RegistryValueKind.DWord);
            key.SetValue(NormalArgumentTypesId, NormalArgumentTypes, RegistryValueKind.DWord);
            key.SetValue(ListArgumentTypesId, ListArgumentTypes, RegistryValueKind.DWord);
            key.SetValue(DictArgumentTypesId, DictArgumentTypes, RegistryValueKind.DWord);
            key.SetValue(ReturnTypesId, ReturnTypes, RegistryValueKind.DWord);
            key.SetValue(YieldTypesId, YieldTypes, RegistryValueKind.DWord);
            key.SetValue(InstanceMembersId, InstanceMembers, RegistryValueKind.DWord);
            key.SetValue(DictKeyTypesId, DictKeyTypes, RegistryValueKind.DWord);
            key.SetValue(DictValueTypesId, DictValueTypes, RegistryValueKind.DWord);
            key.SetValue(IndexTypesId, IndexTypes, RegistryValueKind.DWord);
            key.SetValue(AssignedTypesId, AssignedTypes, RegistryValueKind.DWord);
            key.SetValue(UnifyCallsToNewId, UnifyCallsToNew ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(ProcessCustomDecoratorsId, ProcessCustomDecorators ? 1 : 0, RegistryValueKind.DWord);
        }

        /// <summary>
        /// The key to use with ProjectEntry.Properties to override the call
        /// depth for functions in that module.
        /// </summary>
        public static readonly object CallDepthKey = new object();

        public AnalysisLimits() {
            CallDepth = 3;
            DecreaseCallDepth = 30;
            NormalArgumentTypes = 50;
            ListArgumentTypes = 20;
            DictArgumentTypes = 20;
            ReturnTypes = 20;
            YieldTypes = 20;
            InstanceMembers = 50;
            DictKeyTypes = 10;
            DictValueTypes = 30;
            IndexTypes = 30;
            AssignedTypes = 100;
            ProcessCustomDecorators = true;
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
    }
}
