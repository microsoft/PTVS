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
            return store.IsEnabled(ShowHelpMoniker) ? new CookiecutterSettings(new SettingsStore(store)) : null;
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

        internal void ImportLegacySettings(Func<string, bool> isComplete, Func<string, object> loadLegacy,
            Action<string> markComplete, Action<string, Exception> logFailure) {
            Import(ShowHelpMoniker, nameof(CookiecutterOptionPage.ShowHelp), value => ShowHelp = ReadLegacyBoolean(value));
            Import(FeedUrlMoniker, nameof(CookiecutterOptionPage.FeedUrl), value => FeedUrl = value as string
                ?? throw new FormatException("The legacy Cookiecutter feed URL must be a string."));
            Import(CheckForTemplateUpdateMoniker, nameof(CookiecutterOptionPage.CheckForTemplateUpdate),
                value => CheckForTemplateUpdate = ReadLegacyBoolean(value));

            void Import(string moniker, string propertyName, Action<object> write) {
                try {
                    if (isComplete(moniker)) {
                        return;
                    }
                    if (!_store.IsCustomized(moniker)) {
                        var value = loadLegacy(propertyName);
                        if (value != null) {
                            write(value);
                        }
                    }
                    // Absent and already-customized values are terminal skips, not synthetic writes.
                    markComplete(moniker);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    logFailure(moniker, ex);
                }
            }
        }

        private static bool ReadLegacyBoolean(object value) {
            if (value is bool boolean) {
                return boolean;
            }
            if (value is string text && bool.TryParse(text, out boolean)) {
                return boolean;
            }
            throw new FormatException("The legacy Cookiecutter preference must be a Boolean.");
        }

        private static string GetDescription(string resourceName) => Strings.ResourceManager.GetString(resourceName, Strings.Culture);

        internal sealed class LegacySettings {
            internal bool ShowHelp { get; set; } = true;
            internal string FeedUrl { get; set; } = UrlConstants.DefaultRecommendedFeed;
            internal bool CheckForTemplateUpdate { get; set; } = true;
        }

        private sealed class SettingsStore : ICookiecutterSettingsStore {
            private readonly UnifiedSettingsStore _store;

            internal SettingsStore(UnifiedSettingsStore store) {
                _store = store;
            }

            public T Read<T>(string moniker) => _store.Read<T>(moniker);
            public void Write<T>(string moniker, T value, string description) => _store.Write(moniker, value, description);
            public bool IsCustomized(string moniker) => _store.IsCustomized(moniker);
        }
    }
}
