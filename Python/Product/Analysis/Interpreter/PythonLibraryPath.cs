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
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Analysis {
    public sealed class PythonLibraryPath {
        private readonly string _path;
        private readonly bool _isStandardLibrary;
        private readonly string _modulePrefix;

        private static readonly Regex ParseRegex = new Regex(
            @"(?<path>[^|]+)\|(?<stdlib>stdlib)?\|(?<prefix>[^|]+)?"
        );

        public PythonLibraryPath(string path, bool isStandardLibrary, string modulePrefix) {
            _path = path;
            _isStandardLibrary = isStandardLibrary;
            _modulePrefix = modulePrefix;
        }

        public string Path {
            get { return _path; }
        }

        public bool IsStandardLibrary {
            get { return _isStandardLibrary; }
        }

        public string ModulePrefix {
            get { return _modulePrefix ?? string.Empty; }
        }

        public override string ToString() {
            return string.Format("{0}|{1}|{2}", _path, _isStandardLibrary ? "stdlib" : "", _modulePrefix ?? "");
        }

        public static PythonLibraryPath Parse(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException("source");
            }
            
            var m = ParseRegex.Match(s);
            if (!m.Success || !m.Groups["path"].Success) {
                throw new FormatException();
            }
            
            return new PythonLibraryPath(
                m.Groups["path"].Value,
                m.Groups["stdlib"].Success,
                m.Groups["prefix"].Success ? m.Groups["prefix"].Value : null
            );
        }
    }
}
