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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter {
    public static class AnalysisExtensions {
        public static IPythonInterpreterFactory GetInterpreterFactory(this IPythonInterpreterFactory[] factories, string id, string versionStr) {
            Guid guid;
            Version version;
            if (Guid.TryParse(id, out guid) && Version.TryParse(versionStr, out version)) {
                return GetInterpreterFactory(factories, guid, version);
            }
            return null;
        }

        public static PythonLanguageVersion GetLanguageVersion(this IPythonInterpreterFactory factory) {
            return factory.Configuration.Version.ToLanguageVersion();
        }

        public static IPythonInterpreterFactory GetInterpreterFactory(this IPythonInterpreterFactory[] factories, Guid id, Version version) {
            foreach (var factory in factories) {
                if (factory.Id == id && factory.Configuration.Version == version) {
                    return factory;
                }
            }
            return null;
        }

        /// <summary>
        /// Removes all trailing white space including new lines, tabs, and form feeds.
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string TrimDocumentation(this string self) {
            if (self != null) {
                return self.TrimEnd('\n', '\r', ' ', '\f', '\t');
            } 
            return self;
        }
    }
}
