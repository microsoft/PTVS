// Visual Studio Shared Project
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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestWindow.Extensibility;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(IStackTraceParser))]
    sealed class PythonStackTraceParser : IStackTraceParser {
        public Uri ExecutorUri {
            get {
                return PythonConstants.UnitTestExecutorUri;
            }
        }

        public IEnumerable<StackFrame> GetStackFrames(string errorStackTrace) {
            PythonLanguageVersion languageVersion = PythonLanguageVersion.None;

            int versionIndex = errorStackTrace.IndexOf('\n');
            if (versionIndex >= 0) {
                string ver = errorStackTrace.Substring(0, versionIndex);
                Version version;
                if (Version.TryParse(ver, out version)) {
                    languageVersion = version.ToLanguageVersion();
                }

                errorStackTrace = errorStackTrace.Substring(versionIndex + 1);
            }

            var regex = new Regex(@"File ""(.+)"", line (\d+), in (\w+)");

            foreach (Match match in regex.Matches(errorStackTrace)) {
                int lineno;
                if (int.TryParse(match.Groups[2].Value, out lineno)) {
                    // In some cases (django), we may get a file name that isn't really a file
                    // Ex: File "<frozen importlib._bootstrap>", line 978, in _gcd_import
                    var file = match.Groups[1].Value;
                    string funcName = match.Groups[3].Value;
                    if (File.Exists(file)) {
                        yield return new StackFrame(
                            GetQualifiedFunctionName(file, lineno, funcName, languageVersion),
                            file,
                            lineno
                        );
                    } else {
                        yield return new StackFrame(funcName);
                    }
                }
            }
        }

        private static string GetQualifiedFunctionName(string filename, int lineNo, string functionName, PythonLanguageVersion version) {
            PythonAst ast = null;
            try {
                using (var source = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    ast = Parser.CreateParser(source, version).ParseFile();
                }
            } catch (ArgumentException) {
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            } catch (NotSupportedException) {
            } catch (System.Security.SecurityException) {
            }

            if (ast == null) {
                return functionName;
            }

            return QualifiedFunctionNameWalker.GetDisplayName(
                lineNo,
                functionName,
                ast,
                (a, n) => string.IsNullOrEmpty(a) ? n : Strings.DebugStackFrameNameInName.FormatUI(n, a)
            );
        }
    }
}
