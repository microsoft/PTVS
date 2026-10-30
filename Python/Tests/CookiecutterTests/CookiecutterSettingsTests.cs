// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Microsoft.CookiecutterTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CookiecutterTests {
    [TestClass]
    public class CookiecutterSettingsTests {
        [TestMethod]
        public void ImportsLegacyValuesBeforeMarkingComplete() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = false;
            var completionWrites = 0;
            settings.ImportLegacySettings(() => complete, LoadLegacy, () => {
                Assert.AreEqual(3, store.WriteCount, "The completion flag must follow the whole import.");
                completionWrites++;
                complete = true;
            }, ex => Assert.Fail(ex.ToString()));
            Assert.IsTrue(complete);
            Assert.AreEqual(1, completionWrites);
            Assert.AreEqual(1, store.ImportCalls);
            Assert.IsFalse(settings.ShowHelp);
            Assert.AreEqual("https://example.test/legacy", settings.FeedUrl);
            Assert.IsFalse(settings.CheckForTemplateUpdate);

            settings.ImportLegacySettings(() => complete, propertyName => {
                Assert.Fail("Completed migration must not read legacy storage again.");
                return null;
            }, () => Assert.Fail("Completed migration must not write its flag again."),
                ex => Assert.Fail(ex.ToString()));
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(1, store.ImportCalls);
        }

        [TestMethod]
        public void LegacyImportPreservesOriginalPropertyNames() {
            var settings = new CookiecutterSettings(new TestStore());
            var paths = new List<string>();
            settings.ImportLegacySettings(() => false, propertyName => {
                paths.Add(CookiecutterSettings.LegacySettingsPrefix + propertyName);
                return null;
            }, () => Assert.Fail("No values were imported."), ex => Assert.Fail(ex.ToString()));

            CollectionAssert.AreEqual(new[] {
                "Microsoft.CookiecutterTools.CookiecutterOptionPage.ShowHelp",
                "Microsoft.CookiecutterTools.CookiecutterOptionPage.FeedUrl",
                "Microsoft.CookiecutterTools.CookiecutterOptionPage.CheckForTemplateUpdate"
            }, paths);
        }

        [TestMethod]
        public void NoLegacyValuesDoesNotMarkAnImportComplete() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = false;
            settings.ImportLegacySettings(() => complete, propertyName => null, () => complete = true,
                ex => Assert.Fail(ex.ToString()));
            Assert.IsFalse(complete);
            Assert.AreEqual(0, store.WriteCount);
            Assert.AreEqual(0, store.ImportCalls);
            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.ShowHelpMoniker));
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.FeedUrlMoniker));
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.CheckForTemplateUpdateMoniker));
        }

        [TestMethod]
        public void PresentDefaultValuesAreImportedAsCustomizations() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = false;
            settings.ImportLegacySettings(() => complete, propertyName => {
                return propertyName == nameof(CookiecutterSettings.FeedUrl)
                    ? (object)UrlConstants.DefaultRecommendedFeed : "True";
            }, () => complete = true, ex => Assert.Fail(ex.ToString()));
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(1, store.ImportCalls);
            Assert.IsTrue(complete);
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.ShowHelpMoniker));
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.FeedUrlMoniker));
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.CheckForTemplateUpdateMoniker));
        }

        [TestMethod]
        public void InvalidLegacyValueDoesNotImportOrMarkComplete() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = false;
            var errors = new List<Exception>();
            settings.ImportLegacySettings(() => complete, propertyName => {
                return propertyName == nameof(CookiecutterSettings.ShowHelp) ? "not a Boolean" : LoadLegacy(propertyName);
            }, () => complete = true, errors.Add);
            Assert.IsFalse(complete);
            Assert.AreEqual(1, errors.Count);
            Assert.IsInstanceOfType(errors[0], typeof(FormatException));
            StringAssert.Contains(errors[0].Message, nameof(CookiecutterSettings.ShowHelp));
            Assert.AreEqual(0, store.WriteCount);
            Assert.AreEqual(0, store.ImportCalls);
            Assert.IsTrue(settings.ShowHelp);
        }

        [TestMethod]
        public void AnyCustomizedDefaultSkipsTheWholeCookiecutterImport() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            settings.CheckForTemplateUpdate = true;
            var complete = false;
            var reads = 0;
            settings.ImportLegacySettings(() => complete, propertyName => {
                reads++;
                return LoadLegacy(propertyName);
            }, () => complete = true, ex => Assert.Fail(ex.ToString()));

            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);
            Assert.AreEqual(1, store.WriteCount);
            Assert.AreEqual(0, store.ImportCalls);
            Assert.IsTrue(complete);
            Assert.AreEqual(0, reads);
        }

        [TestMethod]
        public void FailedBatchDoesNotPartiallyImportOrMarkComplete() {
            var store = new TestStore { FailMoniker = CookiecutterSettings.FeedUrlMoniker };
            var settings = new CookiecutterSettings(store);
            var complete = false;
            var errors = new List<Exception>();
            settings.ImportLegacySettings(() => complete, LoadLegacy, () => complete = true, errors.Add);
            Assert.AreEqual(1, errors.Count);
            Assert.IsInstanceOfType(errors[0], typeof(InvalidOperationException));
            Assert.IsFalse(complete);
            Assert.AreEqual(0, store.WriteCount);
            Assert.AreEqual(1, store.ImportCalls);
            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);

            store.FailMoniker = null;
            settings.ImportLegacySettings(() => complete, LoadLegacy, () => complete = true, ex => Assert.Fail(ex.ToString()));
            Assert.IsTrue(complete);
            Assert.IsFalse(settings.ShowHelp);
            Assert.AreEqual("https://example.test/legacy", settings.FeedUrl);
            Assert.IsFalse(settings.CheckForTemplateUpdate);
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(2, store.ImportCalls);
        }

        [TestMethod]
        public void CompletionWriteFailureDoesNotCauseAnotherImport() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = false;
            var errors = new List<Exception>();
            settings.ImportLegacySettings(() => complete, LoadLegacy,
                () => { throw new InvalidOperationException("Completion storage unavailable"); }, errors.Add);
            Assert.AreEqual(1, errors.Count);
            Assert.IsFalse(complete);
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(1, store.ImportCalls);

            var reads = 0;
            settings.ImportLegacySettings(() => complete, propertyName => {
                reads++;
                return LoadLegacy(propertyName);
            }, () => complete = true, ex => Assert.Fail(ex.ToString()));
            Assert.IsTrue(complete);
            Assert.AreEqual(0, reads);
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(1, store.ImportCalls);
        }

        [TestMethod]
        public void CompletedFlagSkipsProvenanceAndLegacyReads() {
            var store = new TestStore { ProvenanceFailure = new InvalidOperationException("Unexpected provenance read") };
            var settings = new CookiecutterSettings(store);
            settings.ImportLegacySettings(() => true, propertyName => {
                Assert.Fail("Completed import must not read legacy settings.");
                return null;
            }, () => Assert.Fail("Completed import must not write its flag again."), ex => Assert.Fail(ex.ToString()));
            Assert.AreEqual(0, store.WriteCount);
            Assert.AreEqual(0, store.ImportCalls);
        }

        [TestMethod]
        public void UnknownProvenanceDoesNotImportOrMarkComplete() {
            var store = new TestStore { ProvenanceFailure = new InvalidOperationException("Unknown provenance") };
            var settings = new CookiecutterSettings(store);
            var complete = false;
            var errors = new List<Exception>();
            settings.ImportLegacySettings(() => complete, LoadLegacy, () => complete = true, errors.Add);
            Assert.IsFalse(complete);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(0, store.WriteCount);
            Assert.AreEqual(0, store.ImportCalls);
        }

        [TestMethod]
        public void ConsumersReadEffectiveValuesRatherThanCachedValues() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);

            store.Write(CookiecutterSettings.ShowHelpMoniker, false, "External update");
            store.Write(CookiecutterSettings.FeedUrlMoniker, "https://example.test/new", "External update");
            store.Write(CookiecutterSettings.CheckForTemplateUpdateMoniker, false, "External update");
            Assert.IsFalse(settings.ShowHelp);
            Assert.AreEqual("https://example.test/new", settings.FeedUrl);
            Assert.IsFalse(settings.CheckForTemplateUpdate);
        }

        private static object LoadLegacy(string propertyName) {
            switch (propertyName) {
                case nameof(CookiecutterSettings.ShowHelp): return false;
                case nameof(CookiecutterSettings.FeedUrl): return "https://example.test/legacy";
                case nameof(CookiecutterSettings.CheckForTemplateUpdate): return "False";
                default: throw new ArgumentOutOfRangeException(nameof(propertyName));
            }
        }

        private sealed class TestStore : ICookiecutterSettingsStore {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object> {
                { CookiecutterSettings.ShowHelpMoniker, true },
                { CookiecutterSettings.FeedUrlMoniker, UrlConstants.DefaultRecommendedFeed },
                { CookiecutterSettings.CheckForTemplateUpdateMoniker, true }
            };
            private readonly HashSet<string> _customized = new HashSet<string>();
            internal string FailMoniker;
            internal Exception ProvenanceFailure;
            internal int WriteCount;
            internal int ImportCalls;

            public T Read<T>(string moniker) => (T)_values[moniker];
            public bool IsCustomized(string moniker) {
                if (ProvenanceFailure != null) {
                    throw ProvenanceFailure;
                }
                return _customized.Contains(moniker);
            }
            public void Write<T>(string moniker, T value, string description) {
                if (moniker == FailMoniker) {
                    throw new InvalidOperationException("USX write unavailable");
                }
                _values[moniker] = value;
                _customized.Add(moniker);
                WriteCount++;
            }

            public void Import(bool? showHelp, string feedUrl, bool? checkForTemplateUpdate, string description) {
                ImportCalls++;
                var pending = new Dictionary<string, object>();
                if (showHelp.HasValue) {
                    pending.Add(CookiecutterSettings.ShowHelpMoniker, showHelp.Value);
                }
                if (feedUrl != null) {
                    pending.Add(CookiecutterSettings.FeedUrlMoniker, feedUrl);
                }
                if (checkForTemplateUpdate.HasValue) {
                    pending.Add(CookiecutterSettings.CheckForTemplateUpdateMoniker, checkForTemplateUpdate.Value);
                }
                if (FailMoniker != null && pending.ContainsKey(FailMoniker)) {
                    throw new InvalidOperationException("USX batch unavailable");
                }
                foreach (var pair in pending) {
                    _values[pair.Key] = pair.Value;
                    _customized.Add(pair.Key);
                    WriteCount++;
                }
            }
        }
    }
}
