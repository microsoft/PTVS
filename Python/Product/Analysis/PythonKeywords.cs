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
using System.Linq;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// The canonical source of keyword names for Python.
    /// </summary>
    public static class PythonKeywords {
        /// <summary>
        /// Returns true if the specified identifier is a keyword in a
        /// particular version of Python.
        /// </summary>
        public static bool IsKeyword(
            string keyword,
            PythonLanguageVersion version = PythonLanguageVersion.None
        ) {
            return All(version).Contains(keyword, StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns true if the specified identifier is a statement keyword in a
        /// particular version of Python.
        /// </summary>
        public static bool IsStatementKeyword(
            string keyword,
            PythonLanguageVersion version = PythonLanguageVersion.None
        ) {
            return Statement(version).Contains(keyword, StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns true if the specified identifier is a statement keyword and
        /// never an expression in a particular version of Python.
        /// </summary>
        public static bool IsOnlyStatementKeyword(
            string keyword,
            PythonLanguageVersion version = PythonLanguageVersion.None
        ) {
            return Statement(version)
                .Except(Expression(version))
                .Contains(keyword, StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns a sequence of all keywords in a particular version of
        /// Python.
        /// </summary>
        public static IEnumerable<string> All(PythonLanguageVersion version = PythonLanguageVersion.None) {
            return Expression(version).Union(Statement(version));
        }

        /// <summary>
        /// Returns a sequence of all keywords usable in an expression in a
        /// particular version of Python.
        /// </summary>
        public static IEnumerable<string> Expression(PythonLanguageVersion version = PythonLanguageVersion.None) {
            yield return "and";
            yield return "as";
            if (version.IsNone() || version >= PythonLanguageVersion.V35) {
                yield return "await";
            }
            yield return "else";
            yield return "False";   // Not technically a keyword in Python 2.x, but may as well be
            yield return "for";
            if (version.IsNone() || version >= PythonLanguageVersion.V33) {
                yield return "from";
            }
            yield return "if";
            yield return "in";
            yield return "is";
            yield return "lambda";
            yield return "None";
            yield return "not";
            yield return "or";
            yield return "True";    // Not technically a keyword in Python 2.x, but may as well be
            yield return "yield";
        }

        /// <summary>
        /// Retuns a sequence of all keywords usable as a statement in a
        /// particular version of Python.
        /// </summary>
        public static IEnumerable<string> Statement(PythonLanguageVersion version = PythonLanguageVersion.None) {
            yield return "assert";
            if (version.IsNone() || version >= PythonLanguageVersion.V35) {
                yield return "async";
                yield return "await";
            }
            yield return "break";
            yield return "continue";
            yield return "class";
            yield return "def";
            yield return "del";
            if (version.IsNone() || version.Is2x()) {
                yield return "exec";
            }
            yield return "if";
            yield return "elif";
            yield return "except";
            yield return "finally";
            yield return "for";
            yield return "from";
            yield return "global";
            yield return "import";
            if (version.IsNone() || version.Is3x()) {
                yield return "nonlocal";
            }
            yield return "pass";
            if (version.IsNone() || version.Is2x()) {
                yield return "print";
            }
            yield return "raise";
            yield return "return";
            yield return "try";
            yield return "while";
            yield return "with";
            yield return "yield";
        }

        /// <summary>
        /// Returns a sequence of all keywords that are invalid outside of
        /// function definitions in a particular version of Python.
        /// </summary>
        public static IEnumerable<string> InvalidOutsideFunction(
            PythonLanguageVersion version = PythonLanguageVersion.None
        ) {
            if (version.IsNone() || version >= PythonLanguageVersion.V35) {
                yield return "await";
            }
            yield return "return";
            if (version.IsNone() || version >= PythonLanguageVersion.V25) {
                yield return "yield";
            }
        }
    }
}
