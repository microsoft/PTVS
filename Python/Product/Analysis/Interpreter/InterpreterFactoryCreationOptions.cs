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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Specifies creation options for an interpreter factory.
    /// </summary>
    public sealed class InterpreterFactoryCreationOptions {
        public InterpreterFactoryCreationOptions() {
#if DEBUG
            TraceLevel = TraceLevel.Verbose;
#endif
        }

        public InterpreterFactoryCreationOptions Clone() {
            return (InterpreterFactoryCreationOptions)MemberwiseClone();
        }

        public bool WatchFileSystem { get; set; }

        public string DatabasePath { get; set; }

        public bool UseExistingCache { get; set; } = true;

        public TraceLevel TraceLevel { get; set; } = TraceLevel.Info;

        #region Dictionary serialization

        public static InterpreterFactoryCreationOptions FromDictionary(Dictionary<string, object> properties) {
            object o;
            TraceLevel level;
            var opts = new InterpreterFactoryCreationOptions {
                DatabasePath = properties.TryGetValue("DatabasePath", out o) ? (o as string) : null,
                UseExistingCache = ReadBool(properties, nameof(UseExistingCache)) ?? true,
                WatchFileSystem = ReadBool(properties, nameof(WatchFileSystem)) ?? false
            };

            if (properties.TryGetValue(nameof(TraceLevel), out o) && Enum.TryParse(o as string, true, out level)) {
                opts.TraceLevel = level;
            }

            return opts;
        }

        public Dictionary<string, object> ToDictionary(bool suppressFileWatching = true) {
            var d = new Dictionary<string, object> {
                { nameof(TraceLevel), TraceLevel }
            };

            d[nameof(DatabasePath)] = DatabasePath;
            d[nameof(UseExistingCache)] = UseExistingCache;
            if (!suppressFileWatching) {
                d[nameof(WatchFileSystem)] = WatchFileSystem;
            }

            return d;
        }

        private static bool? ReadBool(Dictionary<string, object> properties, string key) {
            if (properties.TryGetValue(key, out object o)) {
                return (o as bool?) ?? (o as string)?.IsTrue();
            }

            return null;
        }

        #endregion
    }
}
