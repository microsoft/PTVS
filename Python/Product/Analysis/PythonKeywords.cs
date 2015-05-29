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
            if (version >= PythonLanguageVersion.V35) {
                yield return "await";
            }
            yield return "else";
            if (version.Is3x()) {
                yield return "False";
            }
            yield return "for";
            if (version >= PythonLanguageVersion.V33) {
                yield return "from";
            }
            yield return "if";
            yield return "in";
            yield return "is";
            yield return "lambda";
            yield return "None";
            yield return "not";
            yield return "or";
            if (version.Is3x()) {
                yield return "True";
            }
            yield return "yield";
        }

        /// <summary>
        /// Retuns a sequence of all keywords usable as a statement in a
        /// particular version of Python.
        /// </summary>
        public static IEnumerable<string> Statement(PythonLanguageVersion version = PythonLanguageVersion.None) {
            yield return "assert";
            if (version >= PythonLanguageVersion.V35) {
                yield return "async";
                yield return "await";
            }
            yield return "break";
            yield return "continue";
            yield return "class";
            yield return "def";
            yield return "del";
            if (version.Is2x()) {
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
            if (version.Is3x()) {
                yield return "nonlocal";
            }
            yield return "pass";
            if (version.Is2x()) {
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
            if (version >= PythonLanguageVersion.V35) {
                yield return "await";
            }
            yield return "return";
            if (version == PythonLanguageVersion.None || version >= PythonLanguageVersion.V25) {
                yield return "yield";
            }
        }
    }
}
