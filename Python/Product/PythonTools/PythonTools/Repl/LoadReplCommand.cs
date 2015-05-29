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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
#else
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.Repl {
#if DEV14_OR_LATER
    using IReplWindow = IInteractiveWindow;
    using IReplCommand = IInteractiveWindowCommand;
    using IReplCommand2 = IInteractiveWindowCommand;
    using ReplRoleAttribute = Microsoft.PythonTools.Repl.InteractiveWindowRoleAttribute;
#endif

    [Export(typeof(IReplCommand))]
    class LoadReplCommand : IReplCommand {
        const string _commentPrefix = "%%";

        #region IReplCommand Members

        public Task<ExecutionResult> Execute(IReplWindow window, string arguments) {
            var finder = new FileFinder(arguments);

            var eval = window.Evaluator as BasePythonReplEvaluator;
            if (eval != null && eval.CurrentOptions != null) {
                finder.Search(eval.CurrentOptions.WorkingDirectory);
                finder.SearchAll(eval.CurrentOptions.SearchPaths, ';');
            }

            finder.ThrowIfNotFound();
#if DEV14_OR_LATER
            string commandPrefix = "$";
#else
            string commandPrefix = (string)window.GetOptionValue(ReplOptions.CommandPrefix);
#endif
            string lineBreak = window.TextView.Options.GetNewLineCharacter();

            IEnumerable<string> lines = File.ReadLines(finder.Filename);
            IEnumerable<string> submissions;

            if (eval != null) {
                submissions = eval.JoinCode(lines).Where(CommentPrefixPredicate);
            } else {
                // v1 behavior, will probably never be hit, but if someone was developing their own IReplEvaluator
                // and using this class it would be hit.
                var submissionList = new List<string>();
                var currentSubmission = new List<string>();

                foreach (var line in lines) {
                    if (line.StartsWith(_commentPrefix)) {
                        continue;
                    }

                    if (line.StartsWith(commandPrefix)) {
                        AddSubmission(submissionList, currentSubmission, lineBreak);

                        submissionList.Add(line);
                        currentSubmission.Clear();
                    } else {
                        currentSubmission.Add(line);
                    }
                }

                AddSubmission(submissionList, currentSubmission, lineBreak);

                submissions = submissionList;
            }

            window.Submit(submissions);
            return ExecutionResult.Succeeded;
        }

        private static bool CommentPrefixPredicate(string input) {
            return !input.StartsWith(_commentPrefix);
        }

        private static void AddSubmission(List<string> submissions, List<string> lines, string lineBreak) {
            string submission = String.Join(lineBreak, lines);

            // skip empty submissions:
            if (submission.Length > 0) {
                submissions.Add(submission);
            }
        }

        public string Description {
            get { return "Loads commands from file and executes until complete"; }
        }

        public string Command {
            get { return "load"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }


#if DEV14_OR_LATER
        public IEnumerable<ClassificationSpan> ClassifyArguments(ITextSnapshot snapshot, Span argumentsSpan, Span spanToClassify) {
            yield break;
        }

        public string CommandLine {
            get {
                return "";
            }
        }

        public IEnumerable<string> DetailedDescription {
            get {
                yield return Description;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription {
            get {
                yield break;
            }
        }

        public IEnumerable<string> Names {
            get {
                yield return Command;
            }
        }
#endif
        #endregion

        class FileFinder {
            private readonly string _baseName;

            public FileFinder(string baseName) {
                _baseName = (baseName ?? "").Trim(' ', '\"');

                if (CommonUtils.IsValidPath(_baseName) && Path.IsPathRooted(_baseName) && File.Exists(_baseName)) {
                    Found = true;
                    Filename = _baseName;
                }
            }

            /// <summary>
            /// Searches the specified path and changes <see cref="Found"/> to
            /// true if the file exists. Returns true if the file was found in
            /// the provided path.
            /// </summary>
            public bool Search(string path) {
                if (Found) {
                    // File was found, but not in this path
                    return false;
                }

                if (!CommonUtils.IsValidPath(path) || !Path.IsPathRooted(path)) {
                    return false;
                }

                var fullPath = CommonUtils.GetAbsoluteFilePath(path, _baseName);
                if (File.Exists(fullPath)) {
                    Found = true;
                    Filename = fullPath;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Searches each path in the list of paths as if they had been
            /// passed to <see cref="Search"/> individually.
            /// </summary>
            public bool SearchAll(string paths, char separator) {
                if (Found) {
                    // File was found, but not in this path
                    return false;
                }

                if (string.IsNullOrEmpty(paths)) {
                    return false;
                }

                return SearchAll(paths.Split(separator));
            }

            /// <summary>
            /// Searches each path in the sequence as if they had been passed
            /// to <see cref="Search"/> individually.
            /// </summary>
            public bool SearchAll(IEnumerable<string> paths) {
                if (Found) {
                    // File was found, but not in this path
                    return false;
                }

                if (paths == null) {
                    return false;
                }

                foreach (var path in paths) {
                    if (Search(path)) {
                        return true;
                    }
                }
                return false;
            }

            [DebuggerStepThrough, DebuggerHidden]
            public void ThrowIfNotFound() {
                if (!Found) {
                    throw new FileNotFoundException("Cannot find file.", _baseName);
                }
            }

            public bool Found { get; private set; }

            public string Filename { get; private set; }
        }
    }
}
