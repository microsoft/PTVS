// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    public sealed partial class RegistrationAttributeBuilder {
        private readonly List<RegKey> _keys = new List<RegKey>();

        public RegKey Key(string key) {
            var regKey = new RegKey(key);
            _keys.Add(regKey);
            return regKey;
        }

        public void Register(RegistrationAttribute.RegistrationContext context) {
            foreach (var regKey in _keys) {
                using (var key = context.CreateKey(regKey.Key)) {
                    Register(context, key, regKey);
                }
            }
        }

        public void Unregister(RegistrationAttribute.RegistrationContext context) {
            foreach (var regKey in _keys) {
                Unregister(context, string.Empty, regKey);
            }
        }

        private void Register(RegistrationAttribute.RegistrationContext context, RegistrationAttribute.Key key, RegKey regKey) {
            foreach (var registrySubKey in regKey.SubKeys) {
                using (var subKey = key.CreateSubkey(registrySubKey.Key)) {
                    Register(context, subKey, registrySubKey);
                }
            }

            foreach (var value in regKey.Values) {
                key.SetValue(value.Key, value.Value);
            }

            if (regKey.Package != null) {
                key.SetValue(regKey.Package, context.ComponentType.GUID.ToString("B"));
            }
        }

        private void Unregister(RegistrationAttribute.RegistrationContext context, string prefix, RegKey regKey) {
            prefix += "\\" + regKey.Key;

            foreach (var registrySubKey in regKey.SubKeys) {
                Unregister(context, prefix, registrySubKey);
            }

            foreach (var value in regKey.Values) {
                context.RemoveValue(prefix, value.Key);
            }

            if (regKey.Package != null) {
                context.RemoveValue(prefix, regKey.Package);
            }
        }
    }
}