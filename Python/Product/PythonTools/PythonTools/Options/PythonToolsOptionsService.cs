/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Options {
    class PythonToolsOptionsService : IPythonToolsOptionsService {
        private const string _optionsKey = "Options";
        private readonly WritableSettingsStore _settingsStore;

        public PythonToolsOptionsService(IServiceProvider serviceProvider) {
            var settingsManager = PythonToolsPackage.GetSettings(serviceProvider);
            _settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        public void SaveString(string name, string category, string value) {
            var path = GetCollectionPath(category);
            if (!_settingsStore.CollectionExists(path)) {
                _settingsStore.CreateCollection(path);
            }
            _settingsStore.SetString(path, name, value);
        }

        private static string GetCollectionPath(string category) {
            return PythonCoreConstants.BaseRegistryKey + "\\" + _optionsKey + "\\" + category;
        }

        public string LoadString(string name, string category) {
            var path = GetCollectionPath(category);
            if (!_settingsStore.CollectionExists(path)) {
                return null;
            }
            if (!_settingsStore.PropertyExists(path, name)) {
                return null;
            }
            return _settingsStore.GetString(path, name, "");
        }
    }
}
