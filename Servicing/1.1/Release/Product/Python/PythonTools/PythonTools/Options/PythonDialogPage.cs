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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Base class used for saving/loading of settings.  The settings are stored in VSRegistryRoot\PythonTools\Options\Category\SettingName
    /// where Category is provided in the constructor and SettingName is provided to each call of the Save*/Load* APIs.
    /// x = 42
    /// 
    /// The primary purpose of this class is so that we can be in control of providing reasonable default values.
    /// </summary>
    [ComVisible(true)]
    public class PythonDialogPage : DialogPage {
        private readonly string _category;
        private const string _optionsKey = "Options";

        internal PythonDialogPage(string category) {
            _category = category;
        }

        internal void SaveBool(string name, bool value) {
            SaveString(name, value.ToString());
        }

        internal void SaveInt(string name, int value) {
            SaveString(name, value.ToString());
        }

        internal void SaveString(string name, string value) {
            using (var pythonKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, true).CreateSubKey(PythonCoreConstants.BaseRegistryKey)) {
                using (var optionsKey = pythonKey.CreateSubKey(_optionsKey)) {
                    using (var categoryKey = optionsKey.CreateSubKey(_category)) {
                        categoryKey.SetValue(name, value, Win32.RegistryValueKind.String);
                    }
                }
            }
        }

        internal void SaveEnum<T>(string name, T value) where T : struct {
            SaveString(name, value.ToString());
        }

        internal int? LoadInt(string name) {
            string res = LoadString(name);
            if (res == null) {
                return null;
            }
            return Convert.ToInt32(res);
        }

        internal bool? LoadBool(string name) {
            string res = LoadString(name);
            if (res == null) {
                return null;
            }
            return Convert.ToBoolean(res);
        }

        internal string LoadString(string name) {
            using (var pythonKey = VSRegistry.RegistryRoot(__VsLocalRegistryType.RegType_UserSettings, true).CreateSubKey(PythonCoreConstants.BaseRegistryKey)) {
                using (var optionsKey = pythonKey.CreateSubKey(_optionsKey)) {
                    using (var categoryKey = optionsKey.CreateSubKey(_category)) {
                        return categoryKey.GetValue(name) as string;
                    }
                }
            }
        }

        internal T? LoadEnum<T>(string name) where T : struct {
            string res = LoadString(name);
            if (res == null) {
                return null;
            }

            T enumRes;
            if (Enum.TryParse<T>(res, out enumRes)) {
                return enumRes;
            }
            return null;
        }
    }
}
