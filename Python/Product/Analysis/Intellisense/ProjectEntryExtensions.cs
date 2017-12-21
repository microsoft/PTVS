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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    static class ProjectEntryExtensions {
        private static readonly object _currentCodeKey = new object();

        /// <summary>
        /// Gets the current code for the given buffer for the given project entry.
        /// 
        /// Returns the string for this code and the version of the code represented by the string.
        /// </summary>
        public static string GetCurrentCode(this IProjectEntry entry, int buffer, out int version) {
            lock (_currentCodeKey) {
                object dict;
                CurrentCode curCode;
                if (entry.Properties.TryGetValue(_currentCodeKey, out dict) &&
                    ((SortedDictionary<int, CurrentCode>)dict).TryGetValue(buffer, out curCode)) {
                    version = curCode.Version;
                    return curCode.Text.ToString();
                }

                if (entry.FilePath != null) {
                    try {
                        string allText = File.ReadAllText(entry.FilePath);
                        entry.SetCurrentCode(allText, buffer, 0);
                        version = 0;
                        return allText;
                    } catch (IOException) {
                    }
                }

                version = -1;
                return null;
            }
        }

        /// <summary>
        /// Sets the current code and updates the current version of the code.
        /// </summary>
        public static void SetCurrentCode(this IProjectEntry entry, string value, int buffer, int version) {
            lock (_currentCodeKey) {
                CurrentCode curCode = GetCurrentCode(entry, buffer);

                curCode.Text.Clear();
                curCode.Text.Append(value);
                curCode.Version = version;
            }
        }

        /// <summary>
        /// Updates the code applying the changes to the existing text buffer and updates the version.
        /// </summary>
        public static string UpdateCode(this IProjectEntry entry, IReadOnlyList<IReadOnlyList<ChangeInfo>> versions, int buffer, int version) {
            lock (_currentCodeKey) {
                CurrentCode curCode = GetCurrentCode(entry, buffer);
                var strBuffer = curCode.Text;

                foreach (var versionChange in versions) {
                    int delta = 0;
                    var lineLoc = LineInfo.SplitLines(strBuffer.ToString())
                        .Select(l => new NewLineLocation(l.EndIncludingLineBreak, l.LineBreak))
                        .ToArray();

                    foreach (var change in versionChange) {
                        int start = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.Start, strBuffer.Length);
                        int end = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.End, strBuffer.Length);
                        strBuffer.Remove(start + delta, end - start);
                        if (!string.IsNullOrEmpty(change.InsertedText)) {
                            strBuffer.Insert(start + delta, change.InsertedText);
                            delta += change.InsertedText.Length;
                        }

                        delta -= (end - start);
                    }
                }

                curCode.Version = version;
                return strBuffer.ToString();
            }
        }


        private static CurrentCode GetCurrentCode(IProjectEntry entry, int buffer) {
            object dictTmp;
            SortedDictionary<int, CurrentCode> dict;
            if (!entry.Properties.TryGetValue(_currentCodeKey, out dictTmp)) {
                entry.Properties[_currentCodeKey] = dict = new SortedDictionary<int, CurrentCode>();
            } else {
                dict = (SortedDictionary<int, CurrentCode>)dictTmp;
            }

            CurrentCode curCode;
            if (!dict.TryGetValue(buffer, out curCode)) {
                curCode = dict[buffer] = new CurrentCode();
            }

            return curCode;
        }

        /// <summary>
        /// Gets the verbatim AST for the current code and returns the current version.
        /// </summary>
        public static PythonAst GetVerbatimAst(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, int bufferId, out int version) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            var code = projectFile.GetCurrentCode(bufferId, out version);
            if (code != null) {
                var parser = Parser.CreateParser(
                    new StringReader(code),
                    langVersion,
                    options
                );

                return parser.ParseFile();
            }
            return null;
        }

        /// <summary>
        /// Gets the current AST and the code string for the project entry and returns the current code.
        /// </summary>
        public static PythonAst GetVerbatimAstAndCode(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, int bufferId, out int version, out string code) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            code = projectFile.GetCurrentCode(bufferId, out version);
            if (code != null) {
                var parser = Parser.CreateParser(
                    new StringReader(code),
                    langVersion,
                    options
                );

                return parser.ParseFile();
            }
            return null;
        }

        class CurrentCode {
            public readonly StringBuilder Text = new StringBuilder();
            public int Version;
        }
    }
}
