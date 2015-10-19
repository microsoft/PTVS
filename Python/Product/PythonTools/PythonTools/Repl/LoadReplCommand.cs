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
using System.Threading.Tasks;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
    [Export(typeof(IInteractiveWindowCommand))]
    [ContentType(PythonCoreConstants.ContentType)]
    class LoadReplCommand : IInteractiveWindowCommand {
        const string _commentPrefix = "%%";

        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments) {
            var finder = new FileFinder(arguments);

            var eval = window.Evaluator as BasePythonReplEvaluator;
            if (eval != null && eval.CurrentOptions != null) {
                finder.Search(eval.CurrentOptions.WorkingDirectory);
                finder.SearchAll(eval.CurrentOptions.SearchPaths, ';');
            }

            finder.ThrowIfNotFound();
            string commandPrefix = "$";
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
