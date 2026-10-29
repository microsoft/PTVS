// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0 (the License).

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.PythonTools.Options {
    internal sealed class PythonUnifiedSetting {
        public static readonly IReadOnlyList<PythonUnifiedSetting> All = new[] {
            Boolean("Advanced", "ShowOutputWindowForVirtualEnvCreate", "general.pythonTools.showOutputWindowForVirtualEnvCreate"),
            Boolean("Advanced", "ShowOutputWindowForPackageInstallation", "general.pythonTools.showOutputWindowForPackageInstallation"),
            Boolean("Advanced", "PromptForEnvCreate", "general.pythonTools.promptForEnvCreate"),
            Boolean("Advanced", "PromptForPackageInstallation", "general.pythonTools.promptForPackageInstallation"),
            Boolean("Advanced", "PromptForTestFrameWorkInfoBar", "general.pythonTools.promptForTestFrameWorkInfoBar"),
            Boolean("Advanced", "ElevatePip", "general.pythonTools.elevatePip"),
            Boolean("Analysis", "AutoSearchPaths", "analysis.autoSearchPaths"),
            Text("Analysis", "DiagnosticMode", "analysis.diagnosticMode"),
            Text("Analysis", "LogLevel", "analysis.logLevel"),
            Text("Analysis", "StubPath", "analysis.stubPath"),
            Text("Analysis", "TypeCheckingMode", "analysis.typeCheckingMode"),
            Array("Analysis", "TypeshedPaths", "analysis.typeshedPaths"),
            Array("Analysis", "ExtraPaths", "analysis.extraPaths"),
            Boolean("Analysis", "Indexing", "analysis.indexing"),
            Text("Analysis", "ImportFormat", "analysis.importFormat"),
            Boolean("Analysis", "InlayHintsVariableTypes", "analysis.inlayHintsVariableTypes"),
            Boolean("Analysis", "InlayHintsFunctionReturnTypes", "analysis.inlayHintsFunctionReturnTypes"),
            Boolean("Advanced", "DontPromptBeforeRunningWithBuildError", "debugging.promptBeforeRunningWithBuildError", inverted: true),
            Boolean("Advanced", "WaitOnAbnormalExit", "debugging.waitOnAbnormalExit"),
            Boolean("Advanced", "WaitOnNormalExit", "debugging.waitOnNormalExit"),
            Boolean("Advanced", "TeeStandardOut", "debugging.teeStandardOutput"),
            Boolean("Advanced", "BreakOnSystemExitZero", "debugging.breakOnSystemExitZero"),
            Boolean("Advanced", "DebugStdLib", "debugging.debugStdLib"),
            Boolean("Advanced", "ShowReturnValue", "debugging.showFunctionReturnValue"),
            Text("Advanced", "VariablePresentationForClasses", "debugging.variablePresentationForClasses"),
            Text("Advanced", "VariablePresentationForFunctions", "debugging.variablePresentationForFunctions"),
            Text("Advanced", "VariablePresentationForProtected", "debugging.variablePresentationForProtected"),
            Text("Advanced", "VariablePresentationForSpecial", "debugging.variablePresentationForSpecial"),
            Text("Conda", "CustomCondaExecutablePath", "conda.customCondaExecutablePath"),
            Boolean("Interactive", "UseSmartHistory", "interactive.useSmartHistory"),
            Text("Interactive", "Scripts", "interactive.scripts"),
            Boolean("Advanced", "CompleteFunctionParens", "advanced.completeFunctionParens"),
            Boolean("Advanced", "AutoImportCompletions", "advanced.autoImportCompletions"),
            Boolean("Formatting", "PasteRemovesReplPrompts", "formatting.pasteRemovesReplPrompts"),
            Text("Formatting", "Formatter", "formatting.formatter", "black"),
        };

        private enum ValueKind { Boolean, Text, Array }
        private readonly ValueKind _kind;
        private readonly bool _inverted;
        private readonly string _emptyValue;

        private PythonUnifiedSetting(string category, string name, string suffix, ValueKind kind, bool inverted = false, string emptyValue = "") {
            Category = category;
            Name = name;
            Moniker = "languages.python." + suffix;
            _kind = kind;
            _inverted = inverted;
            _emptyValue = emptyValue;
        }

        public string Category { get; }
        public string Name { get; }
        public string Moniker { get; }
        public bool IsArray => _kind == ValueKind.Array;

        public string Read(UnifiedSettingsStore store) {
            switch (_kind) {
                case ValueKind.Boolean:
                    return (store.Read<bool>(Moniker) ^ _inverted).ToString();
                case ValueKind.Array:
                    return string.Join("\n", store.ReadArray<string>(Moniker));
                default:
                    return store.Read<string>(Moniker);
            }
        }

        public void Write(UnifiedSettingsStore store, string value) {
            store.WriteBatch(writer => Enqueue(writer, value), Strings.ProductTitle);
        }

        internal void Enqueue(ISettingsWriter writer, string value) {
            switch (_kind) {
                case ValueKind.Boolean:
                    if (!bool.TryParse(value, out var boolean)) {
                        throw new FormatException($"The legacy Python setting '{Moniker}' must be a Boolean.");
                    }
                    UnifiedSettingsStore.EnqueueChange(writer, Moniker, boolean ^ _inverted);
                    break;
                case ValueKind.Array:
                    UnifiedSettingsStore.EnqueueArrayChange(writer, Moniker, SplitLegacyPaths(value));
                    break;
                default:
                    UnifiedSettingsStore.EnqueueChange(writer, Moniker, value ?? _emptyValue);
                    break;
            }
        }

        internal static string[] SplitLegacyPaths(string value) {
            return value?.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) ?? System.Array.Empty<string>();
        }

        private static PythonUnifiedSetting Boolean(string category, string name, string suffix, bool inverted = false)
            => new PythonUnifiedSetting(category, name, suffix, ValueKind.Boolean, inverted);
        private static PythonUnifiedSetting Text(string category, string name, string suffix, string emptyValue = "")
            => new PythonUnifiedSetting(category, name, suffix, ValueKind.Text, emptyValue: emptyValue);
        private static PythonUnifiedSetting Array(string category, string name, string suffix)
            => new PythonUnifiedSetting(category, name, suffix, ValueKind.Array);
    }
}