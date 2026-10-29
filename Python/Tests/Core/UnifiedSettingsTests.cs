// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0 (the License).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Internal.VisualStudio.Services.UnifiedSettings;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using Newtonsoft.Json.Linq;
using TestUtilities.Mocks;
using TestUtilities.Python;
using NativeManager = Microsoft.VisualStudio.Utilities.UnifiedSettings.ISettingsManager;

namespace PythonToolsTests {
    [TestClass]
    public class UnifiedSettingsTests {
        private const string Prefix = "languages.python.";
        private const string Legacy = "PythonTools\\Options\\";
        private const string Checkpoints = Legacy + "UnifiedSettingsMigration\\v1";

        [TestMethod]
        public void ManifestAndBackendInventoryAgree() {
            var properties = (JObject)Manifest()["properties"];
            Assert.AreEqual(37, properties.Count);
            Assert.AreEqual(36, PythonUnifiedSetting.All.Count);
            Assert.AreEqual(36, PythonUnifiedSetting.All.Select(s => s.Moniker).Distinct().Count());
            Assert.AreEqual(36, PythonUnifiedSetting.All.Select(s => s.Category + "/" + s.Name).Distinct().Count());
            foreach (var setting in PythonUnifiedSetting.All) {
                Assert.IsNotNull(properties[setting.Moniker], setting.Moniker);
                Assert.AreEqual(setting.IsArray, properties[setting.Moniker]["type"].Value<string>() == "array", setting.Moniker);
                Assert.IsFalse(properties[setting.Moniker]["transientScopesSupported"].Value<bool>(), setting.Moniker);
            }
            CollectionAssert.AreEqual(new[] { "black", "autopep8", "yapf" }, properties[Prefix + "formatting.formatter"]["enum"].Values<string>().ToArray());
            var bridge = properties[Prefix + "general.showCompletionOnTyping.parameterInfo"]["migration"]["custom"];
            Assert.AreEqual("apiOnly", bridge["mode"].Value<string>());
            Assert.AreEqual("TextEditor.Python.Auto List Params", bridge["inputs"][0]["path"].Value<string>());
            Assert.AreEqual(0, bridge["map"][0]["matches"][0].Value<int>());
            Assert.IsFalse(bridge["map"][0]["result"].Value<bool>());
            Assert.AreEqual(1, bridge["map"][1]["matches"][0].Value<int>());
            Assert.IsTrue(bridge["map"][1]["result"].Value<bool>());
            Assert.AreEqual("off", properties[Prefix + "analysis.typeCheckingMode"]["default"].Value<string>());
            Assert.IsTrue(properties[Prefix + "debugging.promptBeforeRunningWithBuildError"]["default"].Value<bool>());
        }

        [TestMethod]
        public void FreshProfileAndResetUseApprovedDefaults() {
            var service = PythonToolsTestUtilities.CreatePythonService();
            Assert.AreEqual("off", service.AnalysisOptions.TypeCheckingMode);
            Assert.IsTrue(service.DebuggerOptions.PromptBeforeRunningWithBuildError);
            Assert.AreEqual("black", service.FormattingOptions.Formatter);
            service.AnalysisOptions.TypeCheckingMode = "strict";
            service.AnalysisOptions.ExtraPaths = new[] { "extra" };
            service.AnalysisOptions.TypeshedPaths = new[] { "types" };
            service.DebuggerOptions.PromptBeforeRunningWithBuildError = false;
            service.FormattingOptions.Formatter = "yapf";
            service.AnalysisOptions.Reset();
            service.DebuggerOptions.Reset();
            service.FormattingOptions.Reset();
            Assert.AreEqual("off", service.AnalysisOptions.TypeCheckingMode);
            CollectionAssert.AreEqual(Array.Empty<string>(), service.AnalysisOptions.ExtraPaths);
            CollectionAssert.AreEqual(Array.Empty<string>(), service.AnalysisOptions.TypeshedPaths);
            Assert.IsTrue(service.DebuggerOptions.PromptBeforeRunningWithBuildError);
            Assert.AreEqual("black", service.FormattingOptions.Formatter);
        }

        [TestMethod]
        public void ImportConvertsLegacyValuesAndRetainsSource() {
            var native = new NativeSettings();
            var legacy = CreateLegacyStore();
            legacy.CreateCollection(Legacy + "Advanced");
            legacy.SetString(Legacy + "Advanced", "DontPromptBeforeRunningWithBuildError", "True");
            legacy.CreateCollection(Legacy + "Analysis");
            legacy.SetString(Legacy + "Analysis", "ExtraPaths", "first;second\r\nthird\n");
            var backend = new PythonToolsOptionsService(legacy, native.Store, () => "False");
            Assert.IsFalse(native.Store.Read<bool>(Prefix + "debugging.promptBeforeRunningWithBuildError"));
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, backend.LoadArray("ExtraPaths", "Analysis"));
            Assert.IsFalse(native.Store.Read<bool>(Prefix + "general.showCompletionOnTyping.parameterInfo"));
            Assert.AreEqual("True", legacy.GetString(Legacy + "Advanced", "DontPromptBeforeRunningWithBuildError"));
            Assert.AreEqual(1, legacy.GetInt32(Checkpoints, Prefix + "analysis.extraPaths"));
        }

        [TestMethod]
        public void UnsupportedVersionPromptStaysInLegacyStorage() {
            VerifyHiddenPreferenceStaysInLegacyStorage("Advanced", "PromptForPythonVersionNotSupportedInfoBarSetting",
                "general.promptForPythonVersionNotSupported", "False", "True");
        }

        [TestMethod]
        public void DebugInteractiveSmartHistoryStaysInLegacyStorage() {
            VerifyHiddenPreferenceStaysInLegacyStorage("Debug Interactive Window", "UseSmartHistory",
                "debugInteractive.useSmartHistory", "False", "True");
        }

        [TestMethod]
        public void DebugInteractiveScriptsStayInLegacyStorage() {
            VerifyHiddenPreferenceStaysInLegacyStorage("Debug Interactive Window", "Scripts",
                "debugInteractive.scripts", "C:\\legacy\\debug-scripts", "C:\\updated\\debug-scripts");
        }

        private static void VerifyHiddenPreferenceStaysInLegacyStorage(string category, string name, string suffix, string original, string updated) {
            var moniker = Prefix + suffix;
            Assert.IsNull(Manifest()["properties"][moniker], moniker);
            var native = new NativeSettings();
            var legacy = CreateLegacyStore();
            var path = Legacy + category;
            legacy.CreateCollection(path);
            legacy.SetString(path, name, original);

            var backend = new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.AreEqual(original, backend.LoadString(name, category));
            Assert.AreEqual(original, legacy.GetString(path, name));
            Assert.IsFalse(legacy.PropertyExists(Checkpoints, moniker));
            Assert.AreEqual(0, native.CommitCalls);

            backend.SaveString(name, category, updated);
            Assert.AreEqual(updated, backend.LoadString(name, category));
            Assert.AreEqual(updated, legacy.GetString(path, name));
            Assert.IsFalse(legacy.PropertyExists(Checkpoints, moniker));
            Assert.AreEqual(0, native.CommitCalls);
        }

        [TestMethod]
        public void ExistingCustomizedDefaultWinsAndMissingValuesAreTerminal() {
            var native = new NativeSettings();
            var moniker = Prefix + "general.elevatePip";
            native.Customized.Add(moniker);
            var legacy = CreateLegacyStore();
            legacy.CreateCollection(Legacy + "Advanced");
            legacy.SetString(Legacy + "Advanced", "ElevatePip", "True");
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.IsFalse(native.Store.Read<bool>(moniker));
            Assert.AreEqual(0, native.CommitCalls);
            Assert.AreEqual(1, legacy.GetInt32(Checkpoints, moniker));
            legacy.SetString(Legacy + "Advanced", "WaitOnNormalExit", "False");
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.IsTrue(native.Store.Read<bool>(Prefix + "debugging.waitOnNormalExit"));
            Assert.AreEqual(0, native.CommitCalls);
        }

        [TestMethod]
        public void FailedImportRetriesWithoutReplayingSuccessfulSettings() {
            var native = new NativeSettings();
            var failed = Prefix + "general.elevatePip";
            native.ChangeFailures[failed] = SettingChangeOutcome.Denied;
            var legacy = CreateLegacyStore();
            legacy.CreateCollection(Legacy + "Advanced");
            legacy.SetString(Legacy + "Advanced", "ElevatePip", "True");
            legacy.SetString(Legacy + "Advanced", "WaitOnNormalExit", "False");
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.IsFalse(legacy.PropertyExists(Checkpoints, failed));
            Assert.IsFalse(native.Store.Read<bool>(Prefix + "debugging.waitOnNormalExit"));
            Assert.AreEqual(1, native.CommitCalls);
            native.ChangeFailures.Clear();
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.AreEqual(1, legacy.GetInt32(Checkpoints, failed));
            Assert.IsTrue(native.Store.Read<bool>(failed));
            Assert.AreEqual(2, native.CommitCalls);
        }

        [TestMethod]
        public void PendingCommitIsNotCheckpointed() {
            var native = new NativeSettings { CommitOutcome = SettingCommitOutcome.PendingApproval };
            var legacy = CreateLegacyStore();
            legacy.CreateCollection(Legacy + "Formatting");
            legacy.SetString(Legacy + "Formatting", "Formatter", "yapf");
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.IsFalse(legacy.PropertyExists(Checkpoints, Prefix + "formatting.formatter"));
            Assert.AreEqual("black", native.Store.Read<string>(Prefix + "formatting.formatter"));
            native.CommitOutcome = SettingCommitOutcome.Success;
            new PythonToolsOptionsService(legacy, native.Store, () => null);
            Assert.AreEqual("yapf", native.Store.Read<string>(Prefix + "formatting.formatter"));
            Assert.AreEqual(1, legacy.GetInt32(Checkpoints, Prefix + "formatting.formatter"));
        }

        [TestMethod]
        public void NativeArraysPreserveEmbeddedSeparators() {
            var native = new NativeSettings();
            var backend = new PythonToolsOptionsService(CreateLegacyStore(), native.Store, () => null);
            var paths = new[] { "C:\\folder;with-semicolon", "C:\\other" };
            Assert.IsTrue(backend.SaveArray("ExtraPaths", "Analysis", paths));
            CollectionAssert.AreEqual(paths, backend.LoadArray("ExtraPaths", "Analysis"));
            Assert.IsFalse(backend.SaveArray("ExtraPaths", "Analysis", paths));
            Assert.AreEqual(1, native.CommitCalls);
        }

        [TestMethod]
        public void ScalarWritesPreserveDefaultCustomizations() {
            var native = new NativeSettings();
            var moniker = Prefix + "general.elevatePip";
            native.Store.Write(moniker, false, "test");
            Assert.AreEqual(SettingWriteOptions.CustomizeUnconditionally, native.LastWriteOptions);
            Assert.IsTrue(native.Store.IsCustomized(moniker));
            Assert.IsFalse(native.Store.Read<bool>(moniker));
        }

        [TestMethod]
        public void NotificationsIgnoreOwnWritesAndDisposeSubscription() {
            var native = new NativeSettings();
            var events = new List<string[]>();
            var backend = new PythonToolsOptionsService(CreateLegacyStore(), native.Store, () => null);
            var subscription = backend.Subscribe(events.Add);
            backend.SaveString("ElevatePip", "Advanced", "True");
            Assert.AreEqual(0, events.Count);
            native.Raise(Prefix + "analysis.indexing", Guid.Empty);
            Assert.AreEqual(1, events.Count);
            CollectionAssert.AreEqual(new[] { "Analysis" }, events[0]);
            subscription.Dispose();
            native.Raise(Prefix + "analysis.indexing", Guid.Empty);
            Assert.AreEqual(1, events.Count);
        }

        [TestMethod]
        public void ClassicModeDoesNotImportOrCheckpoint() {
            var native = new NativeSettings { ReadOutcome = SettingRetrievalOutcome.NotSupportedInClassicMode };
            var legacy = CreateLegacyStore();
            legacy.CreateCollection(Legacy + "Advanced");
            legacy.SetString(Legacy + "Advanced", "ElevatePip", "True");
            var backend = new PythonToolsOptionsService(legacy, native.Store, () => { Assert.Fail("Classic mode must not import Parameter Information."); return null; });
            Assert.AreEqual("True", backend.LoadString("ElevatePip", "Advanced"));
            Assert.IsFalse(legacy.CollectionExists(Checkpoints));
            Assert.AreEqual(0, native.CommitCalls);
        }

        [TestMethod]
        public void InvalidNativeReadAndUnknownProvenanceAreNotSilentlyDefaulted() {
            var native = new NativeSettings { ReadOutcome = SettingRetrievalOutcome.FailedValidation };
            ExpectFailure(() => native.Store.Read<bool>(Prefix + "analysis.indexing"), "FailedValidation");
            native.ProvenanceOverride = SettingValueSource.Unknown;
            ExpectFailure(() => native.Store.IsCustomized(Prefix + "analysis.indexing"), "source");
        }

        private static void ExpectFailure(Action action, string messagePart) {
            try {
                action();
            } catch (InvalidOperationException ex) {
                StringAssert.Contains(ex.Message, messagePart);
                return;
            }
            Assert.Fail("Expected a reported settings failure.");
        }

        private static JObject Manifest() {
            using (var stream = typeof(UnifiedSettingsTests).Assembly.GetManifestResourceStream("PythonToolsTests.PythonTools.registration.json"))
            using (var reader = new StreamReader(stream)) {
                return JObject.Parse(reader.ReadToEnd());
            }
        }

        private static WritableSettingsStore CreateLegacyStore() {
            var provider = new MockServiceProvider();
            var manager = new MockSettingsManager();
            manager.Store.AllowEmptyCollections = true;
            provider.AddService(typeof(SVsSettingsManager), manager);
            return new ShellSettingsManager(provider).GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        private sealed class NativeSettings : NativeManager, ISettingsWriter {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
            private readonly Dictionary<string, object> _queued = new Dictionary<string, object>();
            private event Action<SettingsUpdate> Changed;
            private Guid _eventSource;
            public readonly HashSet<string> Customized = new HashSet<string>();
            public readonly Dictionary<string, SettingChangeOutcome> ChangeFailures = new Dictionary<string, SettingChangeOutcome>();
            public SettingCommitOutcome CommitOutcome = SettingCommitOutcome.Success;
            public SettingRetrievalOutcome ReadOutcome = SettingRetrievalOutcome.Success;
            public SettingValueSource? ProvenanceOverride;
            public SettingWriteOptions LastWriteOptions;
            public int CommitCalls;
            public UnifiedSettingsStore Store { get; }

            public NativeSettings() {
                foreach (var property in ((JObject)Manifest()["properties"]).Properties()) {
                    var value = property.Value["default"];
                    _values[property.Name] = value is JArray array ? (object)array.Values<string>().ToArray() : ((JValue)value).Value;
                }
                Store = new UnifiedSettingsStore(this, moniker => new SettingProvenance(
                    ProvenanceOverride ?? (Customized.Contains(moniker) ? SettingValueSource.UserCustomization : SettingValueSource.RegisteredDefault),
                    Customized.Contains(moniker) ? "user" : null, Customized.Contains(moniker)));
            }

            public ISettingsReader GetReader() => this;
            public ISettingsWriter GetWriter(string callerName) => GetWriter(callerName, Guid.Empty);
            public ISettingsWriter GetWriter(string callerName, Guid eventSource) { _queued.Clear(); _eventSource = eventSource; return this; }
            public SettingRetrieval<T> GetValue<T>(string moniker, SettingReadOptions options) => new SettingRetrieval<T>(ReadOutcome, "test", (T)_values[moniker]);
            public SettingRetrieval<object> GetValue(string moniker, Type targetType, SettingReadOptions options) => GetValue<object>(moniker, options);
            public SettingRetrieval<IReadOnlyList<T>> GetArray<T>(string moniker, SettingReadOptions options) => new SettingRetrieval<IReadOnlyList<T>>(ReadOutcome, "test", (IReadOnlyList<T>)_values[moniker]);
            public T GetValueOrThrow<T>(string moniker) => (T)_values[moniker];
            public object GetValueOrThrow(string moniker, Type targetType) => _values[moniker];
            public IReadOnlyList<T> GetArrayOrThrow<T>(string moniker) => (IReadOnlyList<T>)_values[moniker];
            public SettingChangeResult EnqueueChange<T>(string moniker, T value) => EnqueueChange(moniker, value, SettingWriteOptions.None);
            public SettingChangeResult EnqueueArrayChange<T>(string moniker, IReadOnlyList<T> value) => EnqueueChange(moniker, value, SettingWriteOptions.None);
            public SettingChangeResult EnqueueChange<T>(string moniker, T value, SettingWriteOptions options) {
                LastWriteOptions = options;
                var outcome = ChangeFailures.TryGetValue(moniker, out var failure) ? failure : SettingChangeOutcome.PendingCommit;
                if (outcome == SettingChangeOutcome.PendingCommit) { _queued[moniker] = value; }
                return new SettingChangeResult(outcome, true, "test");
            }
            public SettingCommitResult RequestCommit(string description) {
                CommitCalls++;
                if (CommitOutcome == SettingCommitOutcome.Success) {
                    foreach (var pair in _queued) { _values[pair.Key] = pair.Value; Customized.Add(pair.Key); Raise(pair.Key, _eventSource); }
                    _queued.Clear();
                }
                return new SettingCommitResult(CommitOutcome, "test");
            }
            public SettingCommitResult Commit(string description) => RequestCommit(description);
            public void Raise(string moniker, Guid eventSource) => Changed?.Invoke(new SettingsUpdate(eventSource, new HashSet<string> { moniker }));
            public IDisposable SubscribeToChanges(Action<SettingsUpdate> handler, params string[] patterns) {
                Changed += handler;
                return new Subscription(() => Changed -= handler);
            }
        }

        private sealed class Subscription : IDisposable {
            private readonly Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose() => _dispose();
        }
    }
}