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

        // We use string literals here rather than nameof() to ensure back-compat
        // (though we need to preserve the names of the properties as well for
        // the same reason, so just don't change anything :) )
        private const string CrossModuleId = "CrossModule";
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
        private const string UseTypeStubPackagesId = "UseTypeStubPackages";
        private const string UseTypeStubPackagesExclusivelyId = "UseTypeStubPackagesExclusively";

#if DESKTOP
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
        internal static AnalysisLimits LoadFromStorage(RegistryKey key, bool defaultToStdLib = false) {
            var limits = defaultToStdLib ? GetStandardLibraryLimits() : new AnalysisLimits();

            if (key != null) {
                limits.CrossModule = (key.GetValue(CrossModuleId) as int?) ?? limits.CrossModule;
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
                limits.UseTypeStubPackages = ((key.GetValue(UseTypeStubPackagesId) as int?) ?? (limits.UseTypeStubPackages ? 1 : 0)) != 0;
                limits.UseTypeStubPackagesExclusively = ((key.GetValue(UseTypeStubPackagesExclusivelyId) as int?) ?? (limits.UseTypeStubPackagesExclusively ? 1 : 0)) != 0;
            }

            return limits;
        }

        /// <summary>
        /// Saves the current instance's settings to the specified registry key.
        /// </summary>
        internal void SaveToStorage(RegistryKey key) {
            key.SetValue(CrossModuleId, CrossModule, RegistryValueKind.DWord);
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
            key.SetValue(UseTypeStubPackagesId, UseTypeStubPackages ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(UseTypeStubPackagesExclusivelyId, UseTypeStubPackagesExclusively ? 1 : 0, RegistryValueKind.DWord);
        }
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

        internal AnalysisLimits(Dictionary<string, int> limits) : this() {
            int i;
            if (limits.TryGetValue(CrossModuleId, out i)) CrossModule = i;
            if (limits.TryGetValue(CallDepthId, out i)) CallDepth = i;
            if (limits.TryGetValue(DecreaseCallDepthId, out i)) DecreaseCallDepth = i;
            if (limits.TryGetValue(NormalArgumentTypesId, out i)) NormalArgumentTypes = i;
            if (limits.TryGetValue(ListArgumentTypesId, out i)) ListArgumentTypes = i;
            if (limits.TryGetValue(DictArgumentTypesId, out i)) DictArgumentTypes = i;
            if (limits.TryGetValue(ReturnTypesId, out i)) ReturnTypes = i;
            if (limits.TryGetValue(YieldTypesId, out i)) YieldTypes = i;
            if (limits.TryGetValue(InstanceMembersId, out i)) InstanceMembers = i;
            if (limits.TryGetValue(DictKeyTypesId, out i)) DictKeyTypes = i;
            if (limits.TryGetValue(DictValueTypesId, out i)) DictValueTypes = i;
            if (limits.TryGetValue(IndexTypesId, out i)) IndexTypes = i;
            if (limits.TryGetValue(AssignedTypesId, out i)) AssignedTypes = i;
            if (limits.TryGetValue(UnifyCallsToNewId, out i)) UnifyCallsToNew = i != 0;
            if (limits.TryGetValue(ProcessCustomDecoratorsId, out i)) ProcessCustomDecorators = i != 0;
            if (limits.TryGetValue(UseTypeStubPackagesId, out i)) UseTypeStubPackages = i != 0;
            if (limits.TryGetValue(UseTypeStubPackagesExclusivelyId, out i)) UseTypeStubPackagesExclusively = i != 0;
        }

        internal Dictionary<string, int> ToDictionary() {
            return new Dictionary<string, int> {
                { CrossModuleId, CrossModule },
                { CallDepthId, CallDepth },
                { DecreaseCallDepthId, DecreaseCallDepth },
                { NormalArgumentTypesId, NormalArgumentTypes },
                { ListArgumentTypesId, ListArgumentTypes },
                { DictArgumentTypesId, DictArgumentTypes },
                { ReturnTypesId, ReturnTypes },
                { YieldTypesId, YieldTypes },
                { InstanceMembersId, InstanceMembers },
                { DictKeyTypesId, DictKeyTypes },
                { DictValueTypesId, DictValueTypes },
                { IndexTypesId, IndexTypes },
                { AssignedTypesId, AssignedTypes },
                { UnifyCallsToNewId, UnifyCallsToNew ? 1 : 0 },
                { ProcessCustomDecoratorsId, ProcessCustomDecorators ? 1 : 0 },
                { UseTypeStubPackagesId, UseTypeStubPackages ? 1 : 0 },
                { UseTypeStubPackagesExclusivelyId, UseTypeStubPackagesExclusively ? 1 : 0 }
            };
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
