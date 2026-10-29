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
        public void ImportsLegacyValuesBeforeCheckpoint() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            settings.ImportLegacySettings(complete.Contains, LoadLegacy, moniker => {
                Assert.IsTrue(store.IsCustomized(moniker), "Checkpoint must follow the native write.");
                complete.Add(moniker);
            }, (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, complete.Count);
            Assert.IsFalse(settings.ShowHelp);
            Assert.AreEqual("https://example.test/legacy", settings.FeedUrl);
            Assert.IsFalse(settings.CheckForTemplateUpdate);

            settings.ImportLegacySettings(complete.Contains, propertyName => {
                Assert.Fail("Completed migration must not read legacy storage again.");
                return null;
            }, moniker => Assert.Fail("Completed migration must not write its checkpoint again."),
                (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, store.WriteCount);
        }

        [TestMethod]
        public void AbsentLegacyValuesAreTerminalWithoutSyntheticCustomizations() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            settings.ImportLegacySettings(complete.Contains, propertyName => null, moniker => complete.Add(moniker),
                (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, complete.Count);
            Assert.AreEqual(0, store.WriteCount);
            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.ShowHelpMoniker));
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.FeedUrlMoniker));
            Assert.IsFalse(store.IsCustomized(CookiecutterSettings.CheckForTemplateUpdateMoniker));

            var reads = 0;
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                reads++;
                return LoadLegacy(propertyName);
            }, moniker => Assert.Fail("Absent values were already checkpointed."),
                (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(0, reads);
            Assert.AreEqual(0, store.WriteCount);
        }

        [TestMethod]
        public void PresentDefaultValuesAreImportedAsCustomizations() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                return propertyName == nameof(CookiecutterOptionPage.FeedUrl)
                    ? (object)UrlConstants.DefaultRecommendedFeed : "True";
            }, moniker => complete.Add(moniker), (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(3, complete.Count);
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.ShowHelpMoniker));
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.FeedUrlMoniker));
            Assert.IsTrue(store.IsCustomized(CookiecutterSettings.CheckForTemplateUpdateMoniker));
        }

        [TestMethod]
        public void InvalidLegacyValueLeavesOnlyItsCheckpointUnset() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            var errors = new Dictionary<string, Exception>();
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                return propertyName == nameof(CookiecutterOptionPage.ShowHelp) ? "not a Boolean" : LoadLegacy(propertyName);
            }, moniker => complete.Add(moniker), (moniker, ex) => errors.Add(moniker, ex));
            Assert.AreEqual(2, complete.Count);
            Assert.IsFalse(complete.Contains(CookiecutterSettings.ShowHelpMoniker));
            Assert.AreEqual(1, errors.Count);
            Assert.IsInstanceOfType(errors[CookiecutterSettings.ShowHelpMoniker], typeof(FormatException));
            Assert.AreEqual(2, store.WriteCount);
            Assert.IsTrue(settings.ShowHelp);
        }

        [TestMethod]
        public void PreservesExistingCustomizationsIncludingDefaultValues() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            settings.ShowHelp = true;
            settings.FeedUrl = UrlConstants.DefaultRecommendedFeed;
            settings.CheckForTemplateUpdate = true;
            var complete = new HashSet<string>();
            var reads = 0;
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                reads++;
                return LoadLegacy(propertyName);
            }, moniker => complete.Add(moniker), (moniker, ex) => Assert.Fail(moniker + ": " + ex));

            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual(UrlConstants.DefaultRecommendedFeed, settings.FeedUrl);
            Assert.IsTrue(settings.CheckForTemplateUpdate);
            Assert.AreEqual(3, store.WriteCount);
            Assert.AreEqual(3, complete.Count);
            Assert.AreEqual(0, reads);
        }

        [TestMethod]
        public void FailedImportRetriesWithoutReplayingCompletedSettings() {
            var store = new TestStore { FailMoniker = CookiecutterSettings.FeedUrlMoniker };
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            var errors = new Dictionary<string, Exception>();
            settings.ImportLegacySettings(complete.Contains, LoadLegacy, moniker => complete.Add(moniker),
                (moniker, ex) => errors.Add(moniker, ex));
            Assert.AreEqual(1, errors.Count);
            Assert.IsTrue(errors.ContainsKey(CookiecutterSettings.FeedUrlMoniker));
            Assert.AreEqual(2, complete.Count);
            Assert.IsTrue(complete.Contains(CookiecutterSettings.ShowHelpMoniker));
            Assert.IsTrue(complete.Contains(CookiecutterSettings.CheckForTemplateUpdateMoniker));
            Assert.IsFalse(complete.Contains(CookiecutterSettings.FeedUrlMoniker));
            Assert.AreEqual(2, store.WriteCount);

            settings.ShowHelp = true;
            store.FailMoniker = null;
            var reads = new List<string>();
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                reads.Add(propertyName);
                return LoadLegacy(propertyName);
            }, moniker => complete.Add(moniker), (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, complete.Count);
            CollectionAssert.AreEqual(new[] { nameof(CookiecutterOptionPage.FeedUrl) }, reads);
            Assert.IsTrue(settings.ShowHelp);
            Assert.AreEqual("https://example.test/legacy", settings.FeedUrl);
            Assert.IsFalse(settings.CheckForTemplateUpdate);
            Assert.AreEqual(4, store.WriteCount);
        }

        [TestMethod]
        public void CheckpointFailureCanBeRetriedWithoutRewritingCommittedValues() {
            var store = new TestStore();
            var settings = new CookiecutterSettings(store);
            var complete = new HashSet<string>();
            var errors = new Dictionary<string, Exception>();
            settings.ImportLegacySettings(complete.Contains, LoadLegacy, moniker => {
                if (moniker == CookiecutterSettings.FeedUrlMoniker) {
                    throw new InvalidOperationException("Checkpoint unavailable");
                }
                complete.Add(moniker);
            }, (moniker, ex) => errors.Add(moniker, ex));
            Assert.AreEqual(1, errors.Count);
            Assert.IsTrue(errors.ContainsKey(CookiecutterSettings.FeedUrlMoniker));
            Assert.AreEqual(2, complete.Count);
            Assert.AreEqual(3, store.WriteCount);

            var reads = 0;
            settings.ImportLegacySettings(complete.Contains, propertyName => {
                reads++;
                return LoadLegacy(propertyName);
            }, moniker => complete.Add(moniker), (moniker, ex) => Assert.Fail(moniker + ": " + ex));
            Assert.AreEqual(3, complete.Count);
            Assert.AreEqual(0, reads);
            Assert.AreEqual(3, store.WriteCount);
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
                case nameof(CookiecutterOptionPage.ShowHelp): return false;
                case nameof(CookiecutterOptionPage.FeedUrl): return "https://example.test/legacy";
                case nameof(CookiecutterOptionPage.CheckForTemplateUpdate): return "False";
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
            internal int WriteCount;

            public T Read<T>(string moniker) => (T)_values[moniker];
            public bool IsCustomized(string moniker) => _customized.Contains(moniker);
            public void Write<T>(string moniker, T value, string description) {
                if (moniker == FailMoniker) {
                    throw new InvalidOperationException("USX write unavailable");
                }
                _values[moniker] = value;
                _customized.Add(moniker);
                WriteCount++;
            }
        }
    }
}
