// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.CookiecutterTools {
    internal interface ICookiecutterSettingsStore {
        T Read<T>(string moniker);
        void Write<T>(string moniker, T value, string description);
        bool IsCustomized(string moniker);
        void Import(bool? showHelp, string feedUrl, bool? checkForTemplateUpdate, string description);
    }

    internal sealed class CookiecutterSettings {
        internal const string ShowHelpMoniker = "cookiecutter.general.showHelp";
        internal const string FeedUrlMoniker = "cookiecutter.general.feedUrl";
        internal const string CheckForTemplateUpdateMoniker = "cookiecutter.general.checkForTemplateUpdate";
        private readonly ICookiecutterSettingsStore _store;

        internal CookiecutterSettings(ICookiecutterSettingsStore store) {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        internal static CookiecutterSettings Create(IServiceProvider serviceProvider) {
            var store = UnifiedSettingsStore.Create(serviceProvider);
            return new CookiecutterSettings(new SettingsStore(store));
        }

        internal bool ShowHelp {
            get => _store.Read<bool>(ShowHelpMoniker);
            set => _store.Write(ShowHelpMoniker, value, GetDescription(SR.SettingsShowHelpName));
        }

        internal string FeedUrl {
            get => _store.Read<string>(FeedUrlMoniker);
            set => _store.Write(FeedUrlMoniker, value, GetDescription(SR.SettingsFeedUrlName));
        }

        internal bool CheckForTemplateUpdate {
            get => _store.Read<bool>(CheckForTemplateUpdateMoniker);
            set => _store.Write(CheckForTemplateUpdateMoniker, value, GetDescription(SR.SettingsCheckForTemplateUpdateName));
        }

        internal void ImportLegacySettings(Func<bool> isComplete, Func<string, object> loadLegacy,
            Action markComplete, Action<Exception> logFailure) {
            try {
                if (isComplete()) {
                    return;
                }
                if (_store.IsCustomized(ShowHelpMoniker) || _store.IsCustomized(FeedUrlMoniker)
                    || _store.IsCustomized(CheckForTemplateUpdateMoniker)) {
                    markComplete();
                    return;
                }

                var showHelp = loadLegacy(nameof(CookiecutterOptionPage.ShowHelp));
                var feedUrl = loadLegacy(nameof(CookiecutterOptionPage.FeedUrl));
                var checkForTemplateUpdate = loadLegacy(nameof(CookiecutterOptionPage.CheckForTemplateUpdate));
                if (showHelp == null && feedUrl == null && checkForTemplateUpdate == null) {
                    return;
                }

                _store.Import(
                    showHelp == null ? (bool?)null : ReadLegacyBoolean(showHelp, nameof(CookiecutterOptionPage.ShowHelp)),
                    feedUrl == null ? null : feedUrl as string
                        ?? throw new FormatException("The legacy Cookiecutter feed URL must be a string."),
                    checkForTemplateUpdate == null ? (bool?)null
                        : ReadLegacyBoolean(checkForTemplateUpdate, nameof(CookiecutterOptionPage.CheckForTemplateUpdate)),
                    GetDescription(SR.SettingsGeneralCategory));
                markComplete();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                logFailure(ex);
            }
        }

        private static bool ReadLegacyBoolean(object value, string propertyName) {
            if (value is bool boolean) {
                return boolean;
            }
            if (value is string text && bool.TryParse(text, out boolean)) {
                return boolean;
            }
            throw new FormatException($"The legacy Cookiecutter preference '{propertyName}' must be a Boolean.");
        }

        private static string GetDescription(string resourceName) => Strings.ResourceManager.GetString(resourceName, Strings.Culture);

        private sealed class SettingsStore : ICookiecutterSettingsStore {
            private readonly UnifiedSettingsStore _store;

            internal SettingsStore(UnifiedSettingsStore store) {
                _store = store;
            }

            public T Read<T>(string moniker) => _store.Read<T>(moniker);
            public void Write<T>(string moniker, T value, string description) => _store.Write(moniker, value, description);
            public bool IsCustomized(string moniker) => _store.IsCustomized(moniker);

            public void Import(bool? showHelp, string feedUrl, bool? checkForTemplateUpdate, string description) {
                _store.WriteBatch(writer => {
                    if (showHelp.HasValue) {
                        UnifiedSettingsStore.EnqueueChange(writer, ShowHelpMoniker, showHelp.Value);
                    }
                    if (feedUrl != null) {
                        UnifiedSettingsStore.EnqueueChange(writer, FeedUrlMoniker, feedUrl);
                    }
                    if (checkForTemplateUpdate.HasValue) {
                        UnifiedSettingsStore.EnqueueChange(writer, CheckForTemplateUpdateMoniker, checkForTemplateUpdate.Value);
                    }
                }, description);
            }
        }
    }
}
