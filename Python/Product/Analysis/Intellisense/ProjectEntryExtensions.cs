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
        public static CurrentCode GetCurrentCode(this IProjectEntry entry, int buffer, out int version) {
            lock (_currentCodeKey) {
                var curCode = GetCurrentCode(entry, buffer);
                if (curCode.Version >= 0) {
                    version = curCode.Version;
                    return curCode;
                }

                if (entry.FilePath != null) {
                    try {
                        string allText = File.ReadAllText(entry.FilePath);
                        curCode.Text.Append(allText);
                        curCode.Version = version = 0;
                        return curCode;
                    } catch (IOException) {
                    }
                }

                curCode.Text.Clear();
                curCode.Version = version = 0;
                return curCode;
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
                curCode = dict[buffer] = new CurrentCode { Version = -1 };
            }

            return curCode;
        }

        /// <summary>
        /// Gets the verbatim AST for the current code and returns the current version.
        /// </summary>
        public static PythonAst GetVerbatimAst(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, int bufferId, out int version) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            var code = projectFile.GetCurrentCode(bufferId, out version)?.Text.ToString();
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

            code = projectFile.GetCurrentCode(bufferId, out version)?.Text.ToString();
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

        public class CurrentCode {
            public readonly StringBuilder Text = new StringBuilder();
            public int Version;

            /// <summary>
            /// Updates the code applying the changes to the existing text buffer and updates the version.
            /// </summary>
            public void UpdateCode(IReadOnlyList<IReadOnlyList<ChangeInfo>> versions, int finalVersion) {
                lock (_currentCodeKey) {
                    if (finalVersion < Version) {
                        throw new InvalidOperationException("code is out of sync");
                    }

                    foreach (var versionChange in versions) {
                        int lastStart = -1;
                        int delta = 0;
                        var lineLoc = LineInfo.SplitLines(Text.ToString())
                            .Select(l => new NewLineLocation(l.EndIncludingLineBreak, l.LineBreak))
                            .ToArray();

                        foreach (var change in versionChange) {
                            int start = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.Start, Text.Length);
                            if (start < lastStart) {
                                throw new InvalidOperationException("changes must be in order of start location");
                            }
                            lastStart = start;

                            int end = NewLineLocation.LocationToIndex(lineLoc, change.ReplacedSpan.End, Text.Length);
                            if (end > start) {
                                Text.Remove(start + delta, end - start);
                            }
                            if (!string.IsNullOrEmpty(change.InsertedText)) {
                                Text.Insert(start + delta, change.InsertedText);
                                delta += change.InsertedText.Length;
                            }

                            delta -= (end - start);
                        }

                    }
                    Version = finalVersion;
                }
            }

        }
    }
}
