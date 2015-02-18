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
