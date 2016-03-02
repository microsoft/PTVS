using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Communication;
namespace Microsoft.PythonTools {
    using System.IO;
    using Parsing;
    using Parsing.Ast;
    using AP = AnalysisProtocol;

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
        public static string UpdateCode(this IProjectEntry entry, AP.VersionChanges[] versions, int buffer, int version) {
            lock (_currentCodeKey) {
                CurrentCode curCode = GetCurrentCode(entry, buffer);
                var strBuffer = curCode.Text;

                foreach (var versionChange in versions) {
                    int delta = 0;

                    foreach (var change in versionChange.changes) {
                        strBuffer.Remove(change.start + delta, change.length);
                        strBuffer.Insert(change.start + delta, change.newText);

                        delta += change.newText.Length - change.length;
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

            var parser = Parser.CreateParser(
                new StringReader(projectFile.GetCurrentCode(bufferId, out version)),
                langVersion,
                options
            );

            return parser.ParseFile();
        }

        /// <summary>
        /// Gets the current AST and the code string for the project entry and returns the current code.
        /// </summary>
        public static PythonAst GetVerbatimAstAndCode(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, int bufferId, out int version, out string code) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            code = projectFile.GetCurrentCode(bufferId, out version);
            var parser = Parser.CreateParser(
                new StringReader(code),
                langVersion,
                options
            );

            return parser.ParseFile();
        }

        class CurrentCode {
            public readonly StringBuilder Text = new StringBuilder();
            public int Version;
        }
    }
}
