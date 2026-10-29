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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Options {
    class PythonToolsOptionsService : IPythonToolsOptionsService {
        private const string _optionsKey = "Options";
        private const string MigrationCategory = "UnifiedSettingsMigration\\v1";
        private const string ParameterInfoMoniker = "languages.python.general.showCompletionOnTyping.parameterInfo";
        private readonly WritableSettingsStore _settingsStore;
        private readonly UnifiedSettingsStore _unifiedSettings;

        public static object CreateService(IServiceContainer container, Type serviceType) 
            => serviceType.IsEquivalentTo(typeof(IPythonToolsOptionsService)) ? new PythonToolsOptionsService(container) : null;

        private PythonToolsOptionsService(IServiceProvider serviceProvider)
            : this(SettingsManagerCreator.GetSettingsManager(serviceProvider).GetWritableSettingsStore(SettingsScope.UserSettings),
                  UnifiedSettingsStore.Create(serviceProvider), () => LoadLegacyParameterInfo(serviceProvider)) {
        }

        internal PythonToolsOptionsService(WritableSettingsStore settingsStore, UnifiedSettingsStore unifiedSettings, Func<string> loadLegacyParameterInfo) {
            _settingsStore = settingsStore;
            if (unifiedSettings.IsEnabled(PythonUnifiedSetting.All[0].Moniker)) {
                _unifiedSettings = unifiedSettings;
                foreach (var setting in PythonUnifiedSetting.All) {
                    Import(setting.Moniker, () => LoadLegacyString(setting.Name, setting.Category), value => setting.Write(_unifiedSettings, value));
                }
                Import(ParameterInfoMoniker, loadLegacyParameterInfo, value => {
                    _unifiedSettings.Write(ParameterInfoMoniker, bool.Parse(value), Strings.ProductTitle);
                });
            }
        }

        public void SaveString(string name, string category, string value) {
            var setting = FindNativeSetting(name, category);
            if (setting != null) {
                setting.Write(_unifiedSettings, value);
                return;
            }
            var path = GetCollectionPath(category);
            if (value == null) {
                if (_settingsStore.CollectionExists(path)) {
                    _settingsStore.DeleteProperty(path, name);
                }
            } else {
                if (!_settingsStore.CollectionExists(path)) {
                    _settingsStore.CreateCollection(path);
                }
                _settingsStore.SetString(path, name, value);
            }
        }

        private static string GetCollectionPath(string category) {
            return PythonCoreConstants.BaseRegistryKey + "\\" + _optionsKey + "\\" + category;
        }

        public string LoadString(string name, string category) {
            var setting = FindNativeSetting(name, category);
            return setting != null ? setting.Read(_unifiedSettings) : LoadLegacyString(name, category);
        }

        private string LoadLegacyString(string name, string category) {
            var path = GetCollectionPath(category);
            if (!_settingsStore.CollectionExists(path)) {
                return null;
            }
            if (!_settingsStore.PropertyExists(path, name)) {
                return null;
            }
            return _settingsStore.GetString(path, name, "");
        }

        internal bool IsNativeArray(string name, string category) => FindNativeSetting(name, category)?.IsArray == true;

        internal string[] LoadArray(string name, string category) {
            return _unifiedSettings.ReadArray<string>(FindNativeSetting(name, category).Moniker).ToArray();
        }

        internal bool SaveArray(string name, string category, string[] value) {
            value = value ?? Array.Empty<string>();
            var setting = FindNativeSetting(name, category);
            if (_unifiedSettings.ReadArray<string>(setting.Moniker).SequenceEqual(value)) {
                return false;
            }
            _unifiedSettings.WriteArray(setting.Moniker, value, Strings.ProductTitle);
            return true;
        }

        internal IDisposable Subscribe(Action<string[]> changed) {
            return _unifiedSettings?.Subscribe(update => {
                var categories = PythonUnifiedSetting.All
                    .Where(setting => update.ChangedSettingMonikers.Contains(setting.Moniker))
                    .Select(setting => setting.Category).Distinct().ToArray();
                if (categories.Length != 0) {
                    changed(categories);
                }
            }, "languages.python.*");
        }

        private PythonUnifiedSetting FindNativeSetting(string name, string category) {
            return _unifiedSettings == null ? null : PythonUnifiedSetting.All.FirstOrDefault(setting => setting.Name == name && setting.Category == category);
        }

        private void Import(string moniker, Func<string> loadLegacyValue, Action<string> writeNativeValue) {
            try {
                var path = GetCollectionPath(MigrationCategory);
                if (_settingsStore.CollectionExists(path) && _settingsStore.PropertyExists(path, moniker)) {
                    return;
                }
                if (!_unifiedSettings.IsCustomized(moniker)) {
                    var value = loadLegacyValue();
                    if (value != null) {
                        writeNativeValue(value);
                    }
                }
                if (!_settingsStore.CollectionExists(path)) {
                    _settingsStore.CreateCollection(path);
                }
                _settingsStore.SetBoolean(path, moniker, true);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                CommonUtils.ActivityLogError(Strings.ProductTitle, $"Unable to import legacy setting '{moniker}'. The import will be retried on the next initialization. {ex}");
            }
        }

        private static string LoadLegacyParameterInfo(IServiceProvider provider) {
            var settings = provider.GetService(typeof(SVsSettingsPersistenceManager)) as Microsoft.VisualStudio.Settings.ISettingsManager;
            if (settings == null) {
                throw new InvalidOperationException("The legacy Visual Studio settings service is unavailable.");
            }
            var result = settings.TryGetValue<object>("TextEditor.Python.Auto List Params", out var value);
            if (result == GetValueResult.Missing) {
                return null;
            }
            if (result != GetValueResult.Success) {
                throw new InvalidOperationException($"Reading the legacy Python Parameter Information setting failed ({result}).");
            }
            var numericValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (numericValue != 0 && numericValue != 1) {
                throw new FormatException("The legacy Python Parameter Information setting must be 0 or 1.");
            }
            return (numericValue == 1).ToString();
        }

        public void DeleteCategory(string category) {
            var path = GetCollectionPath(category);
            try {
                _settingsStore.DeleteCollection(path);
            } catch (ArgumentException) {
                // Documentation is a lie - raises ArgumentException if the
                // collection does not exist.
            }
        }
    }
}
