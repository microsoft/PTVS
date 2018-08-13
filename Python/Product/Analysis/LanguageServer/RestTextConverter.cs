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
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

// Based on https://github.com/Microsoft/vscode-python/blob/master/src/client/common/markdown/restTextConverter.ts

namespace Microsoft.PythonTools.Analysis {
    internal sealed class RestTextConverter {
        private List<string> _md;
        private State _state;

        enum State {
            Default,
            Preformatted,
            Code
        }

        /// <summary>
        /// Translates reStructruredText (Python doc syntax) to markdown. Only translates as much 
        /// as needed to display tooltips and the documentation in the completion list.
        /// </summary>
        /// <seealso cref="https://en.wikipedia.org/wiki/ReStructuredText" />
        public string ToMarkdown(string s) {
            if(string.IsNullOrEmpty(s)) {
                return s;
            }
            _state = State.Default;
            _md = new List<string>();
            return TransformLines(s);
        }

        public string EscapeMarkdown(string s) {
            // Not a complete escape list so it does not interfere
            // with subsequent code highlighting that also uses markdown.
            s = s
                .Replace("#", @"\#")
                .Replace("*", @"\*")
                .Replace(" _", @" \_");

            if (s.Length > 0 && s[0] == '_') {
                s = @"\_" + s.Substring(1);
            }
            return s;
        }

        private string TransformLines(string docstring) {
            var lines = docstring.Replace("\r", string.Empty).Split( new[] { '\n' });
            for (var i = 0; i < lines.Length; i += 1) {
                var line = lines[i];
                // Avoid leading empty lines
                if (_md.Count == 0 && line.Length == 0) {
                    continue;
                }
                switch (_state) {
                    case State.Default:
                        i += InDefaultState(lines, i);
                        break;
                    case State.Preformatted:
                        i += InPreformattedState(lines, i);
                        break;
                    case State.Code:
                        InCodeState(line);
                        break;
                    default:
                        break;
                }
            }
            EndCodeBlock();
            EndPreformattedBlock();

            var sb = new StringBuilder();
            foreach(var s in _md) {
                sb.AppendLine(s + "  "); // Keep hard line breaks
            }
            return sb.ToString().Trim();
        }

        private int InDefaultState(string[] lines, int i) {
            var line = lines[i];
            if (line.StartsWith("```", StringComparison.Ordinal)) {
                StartCodeBlock();
                return 0;
            }

            if (line.StartsWith("===", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal)) {
                return 0; // Eat standalone === or --- lines.
            }
            if (HandleDoubleColon(line)) {
                return 0;
            }
            if (IsIgnorable(line)) {
                return 0;
            }

            if (HandleSectionHeader(lines, i)) {
                return 1; // Eat line with === or ---
            }

            var result = CheckPreContent(lines, i);
            if (_state != State.Default) {
                return result; // Handle line in the new state
            }

            line = Cleanup(line);
            line = line.Replace("``", "`"); // Convert double backticks to single.
            line = EscapeMarkdown(line);
            _md.Add(line);
            return 0;
        }

        private int InPreformattedState(string[] lines, int i) {
            var line = lines[i];
            if (IsIgnorable(line)) {
                return 0;
            }
            // Preformatted block terminates by a line without leading whitespace.
            if (line.Length > 0 && !Char.IsWhiteSpace(line[0]) && !IsListItem(line)) {
                EndPreformattedBlock();
                return -1;
            }

            var prevLine = _md.Count > 0 ? _md[_md.Count - 1] : null;
            if (line.Length == 0 && prevLine != null && (prevLine.Length == 0 || prevLine.StartsWith("```", StringComparison.Ordinal))) {
                return 0; // Avoid more than one empty line in a row.
            }

            // Keep hard line breaks for the preformatted content
            line = PreserveIndentation(Cleanup(line));
            _md.Add($"{ line}  ");
            return 0;
        }

        private void InCodeState(string line) {
            var prevLine = _md.Count > 0 ? _md[_md.Count - 1] : null;
            if (line.Length == 0 && prevLine != null && (prevLine.Length == 0 || prevLine.StartsWith("```", StringComparison.Ordinal))) {
                return; // Avoid more than one empty line in a row.
            }

            if (line.StartsWith("```", StringComparison.Ordinal)) {
                EndCodeBlock();
            } else {
                _md.Add(line);
            }
        }

        private bool IsIgnorable(string line) {
            if (line.IndexOf("generated/", StringComparison.Ordinal) >= 0) {
                return true; // Drop generated content.
            }
            var trimmed = line.Trim();
            if (trimmed.StartsWithOrdinal("..") && trimmed.IndexOf("::", StringComparison.Ordinal) > 0) {
                // Ignore lines likes .. sectionauthor:: John Doe.
                return true;
            }
            return false;
        }

        private int CheckPreContent(string[] lines, int i) {
            var line = lines[i];
            if (i == 0 || line.Trim().Length == 0) {
                return 0;
            }

            if (!Char.IsWhiteSpace(line[0]) && !IsListItem(line)) {
                return 0; // regular line, nothing to do here.
            }
            // Indented content is considered to be preformatted.
            StartPreformattedBlock();
            return -1;
        }

        private bool HandleSectionHeader(string[] lines, int i) {
            var line = lines[i];
            if (i < lines.Length - 1 && (lines[i + 1].StartsWithOrdinal("==="))) {
                // Section title -> heading level 3.
                _md.Add($"### {Cleanup(line)}");
                return true;
            }
            if (i < lines.Length - 1 && (lines[i + 1].StartsWithOrdinal("---"))) {
                // Subsection title -> heading level 4.
                _md.Add($"#### {Cleanup(line)}");
                return true;
            }
            return false;
        }

        private bool HandleDoubleColon(string line) {
            if (!line.EndsWith("::", StringComparison.Ordinal)) {
                return false;
            }
            // Literal blocks begin with `::`. Such as sequence like
            // '... as shown below::' that is followed by a preformatted text.
            if (line.Length > 2 && !line.StartsWithOrdinal("..")) {
                // Ignore lines likes .. autosummary:: John Doe.
                // Trim trailing : so :: turns into :.
                _md.Add(line.Substring(0, line.Length - 1));
            }

            StartPreformattedBlock();
            return true;
        }

        private void StartPreformattedBlock() {
            // Remove previous empty line so we avoid double empties.
            TryRemovePrecedingEmptyLines();
            _state = State.Preformatted;
        }

        private void EndPreformattedBlock() {
            if (_state == State.Preformatted) {
                TryRemovePrecedingEmptyLines();
                _state = State.Default;
            }
        }

        private void StartCodeBlock() {
            // Remove previous empty line so we avoid double empties.
            TryRemovePrecedingEmptyLines();
            _md.Add("```python");
            _state = State.Code;
        }

        private void EndCodeBlock() {
            if (_state == State.Code) {
                TryRemovePrecedingEmptyLines();
                _md.Add("```");
                _state = State.Default;
            }
        }

        private void TryRemovePrecedingEmptyLines() {
            while (_md.Count > 0 && _md[_md.Count - 1].Trim().Length == 0) {
                _md.RemoveAt(_md.Count - 1);
            }
        }

        private bool IsListItem(string line) {
            var trimmed = line.Trim();
            var ch = trimmed.Length > 0 ? trimmed[0] : '\0';
            return ch == '*' || ch == '-' || Char.IsDigit(ch);
        }

        private string Cleanup(string line) => line.Replace(":mod:", "module:");

        private string PreserveIndentation(string line) {
            var sb = new StringBuilder();
            for (var j = 0; j < line.Length; j++) {
                switch (line[j]) {
                    case ' ':
                        sb.Append("&nbsp;");
                        break;
                    case '\t':
                        sb.Append("&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;");
                        break;
                    default:
                        sb.Append(line.Substring(j));
                        j = line.Length;
                        break;
                }
            }
            return sb.Replace("``", "`").ToString();
        }
    }
}
