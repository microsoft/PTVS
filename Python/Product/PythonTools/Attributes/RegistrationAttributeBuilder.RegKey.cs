// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using static System.FormattableString;

namespace Microsoft.PythonTools {
    public sealed partial class RegistrationAttributeBuilder {
        public class RegKey {
            public string Key { get; }
            public string Package { get; private set; }
            public List<RegKey> SubKeys { get; } = new List<RegKey>();
            public Dictionary<string, object> Values = new Dictionary<string, object>();

            public RegKey(string key) {
                Key = key;
            }

            public RegKey GuidSubKey(Type key) {
                return SubKey(key?.GUID.ToString("B"));
            }

            public RegKey GuidSubKey(string key) {
                return SubKey(key != null ? new Guid(key).ToString("B") : null);
            }

            public RegKey SubKey(string key) {
                var regKey = new RegKey(key);
                SubKeys.Add(regKey);
                return regKey;
            }

            public RegKey PackageGuidValue(string name) {
                Package = name;
                return this;
            }

            public RegKey StringValue(string name, string data) {
                if (data != null) {
                    Values[name] = data;
                }
                return this;
            }

            public RegKey IntValue(string name, int data) {
                Values[name] = data;
                return this;
            }

            public RegKey GuidValue(string name, string data) {
                if (data != null) {
                    Values[name] = new Guid(data).ToString("B");
                }
                return this;
            }

            public RegKey GuidArrayValue(string name, string[] data, string separator = ";") {
                if (data != null && data.Length > 0) {
                    Values[name] = string.Join(separator, data.Select(d => new Guid(d).ToString("B")));
                }
                return this;
            }

            public RegKey BoolValue(string name, bool? data) {
                if (data.HasValue) {
                    Values[name] = data.Value ? 1 : 0;
                }
                return this;
            }

            public RegKey ResourceIdValue(string name, int? data) {
                if (data.HasValue) {
                    Values[name] = Invariant($"#{data.Value}");
                }
                return this;
            }
        }
    }
}