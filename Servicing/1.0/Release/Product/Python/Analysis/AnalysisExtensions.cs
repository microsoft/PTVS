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
            switch (factory.Configuration.Version.Major) {
                case 2:
                    switch (factory.Configuration.Version.Minor) {
                        case 4: return PythonLanguageVersion.V24;
                        case 5: return PythonLanguageVersion.V25;
                        case 6: return PythonLanguageVersion.V26;
                        case 7: return PythonLanguageVersion.V27;
                    }
                    break;
                case 3:
                    switch (factory.Configuration.Version.Minor) {
                        case 0: return PythonLanguageVersion.V30;
                        case 1: return PythonLanguageVersion.V31;
                        case 2: return PythonLanguageVersion.V32;
                    }
                    break;
            }
            throw new InvalidOperationException(String.Format("Unsupported Python version: {0}", factory.Configuration.Version.ToString()));
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
