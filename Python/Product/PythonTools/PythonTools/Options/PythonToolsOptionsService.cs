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

namespace Microsoft.PythonTools.Options
{
    class PythonToolsOptionsService : IPythonToolsOptionsService
    {
        private const string _optionsKey = "Options";
        private readonly WritableSettingsStore _settingsStore;

        public static object CreateService(IServiceContainer container, Type serviceType)
            => serviceType.IsEquivalentTo(typeof(IPythonToolsOptionsService)) ? new PythonToolsOptionsService(container) : null;

        private PythonToolsOptionsService(IServiceProvider serviceProvider)
        {
            var settingsManager = SettingsManagerCreator.GetSettingsManager(serviceProvider);
            _settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        public void SaveString(string name, string category, string value)
        {
            var path = GetCollectionPath(category);
            if (value == null)
            {
                if (_settingsStore.CollectionExists(path))
                {
                    _settingsStore.DeleteProperty(path, name);
                }
            }
            else
            {
                if (!_settingsStore.CollectionExists(path))
                {
                    _settingsStore.CreateCollection(path);
                }
                _settingsStore.SetString(path, name, value);
            }
        }

        private static string GetCollectionPath(string category)
        {
            return PythonCoreConstants.BaseRegistryKey + "\\" + _optionsKey + "\\" + category;
        }

        public string LoadString(string name, string category)
        {
            var path = GetCollectionPath(category);
            if (!_settingsStore.CollectionExists(path))
            {
                return null;
            }
            if (!_settingsStore.PropertyExists(path, name))
            {
                return null;
            }
            return _settingsStore.GetString(path, name, "");
        }

        public void DeleteCategory(string category)
        {
            var path = GetCollectionPath(category);
            try
            {
                _settingsStore.DeleteCollection(path);
            }
            catch (ArgumentException)
            {
                // Documentation is a lie - raises ArgumentException if the
                // collection does not exist.
            }
        }
    }
}
