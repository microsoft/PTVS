using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Intellisense {
    public static class AnalysisLimitsConverter {
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
            var limits = defaultToStdLib ? AnalysisLimits.GetStandardLibraryLimits() : new AnalysisLimits();

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

        public static AnalysisLimits FromDictionary(Dictionary<string, int> limits) {
            var analysisLimits = new AnalysisLimits();
            int i;
            if (limits.TryGetValue(CrossModuleId, out i)) analysisLimits.CrossModule = i;
            if (limits.TryGetValue(CallDepthId, out i)) analysisLimits.CallDepth = i;
            if (limits.TryGetValue(DecreaseCallDepthId, out i)) analysisLimits.DecreaseCallDepth = i;
            if (limits.TryGetValue(NormalArgumentTypesId, out i)) analysisLimits.NormalArgumentTypes = i;
            if (limits.TryGetValue(ListArgumentTypesId, out i)) analysisLimits.ListArgumentTypes = i;
            if (limits.TryGetValue(DictArgumentTypesId, out i)) analysisLimits.DictArgumentTypes = i;
            if (limits.TryGetValue(ReturnTypesId, out i)) analysisLimits.ReturnTypes = i;
            if (limits.TryGetValue(YieldTypesId, out i)) analysisLimits.YieldTypes = i;
            if (limits.TryGetValue(InstanceMembersId, out i)) analysisLimits.InstanceMembers = i;
            if (limits.TryGetValue(DictKeyTypesId, out i)) analysisLimits.DictKeyTypes = i;
            if (limits.TryGetValue(DictValueTypesId, out i)) analysisLimits.DictValueTypes = i;
            if (limits.TryGetValue(IndexTypesId, out i)) analysisLimits.IndexTypes = i;
            if (limits.TryGetValue(AssignedTypesId, out i)) analysisLimits.AssignedTypes = i;
            if (limits.TryGetValue(UnifyCallsToNewId, out i)) analysisLimits.UnifyCallsToNew = i != 0;
            if (limits.TryGetValue(ProcessCustomDecoratorsId, out i)) analysisLimits.ProcessCustomDecorators = i != 0;
            if (limits.TryGetValue(UseTypeStubPackagesId, out i)) analysisLimits.UseTypeStubPackages = i != 0;
            if (limits.TryGetValue(UseTypeStubPackagesExclusivelyId, out i)) analysisLimits.UseTypeStubPackagesExclusively = i != 0;

            return analysisLimits;
        }

        public static Dictionary<string, int> ToDictionary(this AnalysisLimits analysisLimits) {
            return new Dictionary<string, int> {
                { CrossModuleId, analysisLimits.CrossModule },
                { CallDepthId, analysisLimits.CallDepth },
                { DecreaseCallDepthId, analysisLimits.DecreaseCallDepth },
                { NormalArgumentTypesId, analysisLimits.NormalArgumentTypes },
                { ListArgumentTypesId, analysisLimits.ListArgumentTypes },
                { DictArgumentTypesId, analysisLimits.DictArgumentTypes },
                { ReturnTypesId, analysisLimits.ReturnTypes },
                { YieldTypesId, analysisLimits.YieldTypes },
                { InstanceMembersId, analysisLimits.InstanceMembers },
                { DictKeyTypesId, analysisLimits.DictKeyTypes },
                { DictValueTypesId, analysisLimits.DictValueTypes },
                { IndexTypesId, analysisLimits.IndexTypes },
                { AssignedTypesId, analysisLimits.AssignedTypes },
                { UnifyCallsToNewId, analysisLimits.UnifyCallsToNew ? 1 : 0 },
                { ProcessCustomDecoratorsId, analysisLimits.ProcessCustomDecorators ? 1 : 0 },
                { UseTypeStubPackagesId, analysisLimits.UseTypeStubPackages ? 1 : 0 },
                { UseTypeStubPackagesExclusivelyId, analysisLimits.UseTypeStubPackagesExclusively ? 1 : 0 }
            };
        }
    }
}