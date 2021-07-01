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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Repl
{
    [Export(typeof(IInteractiveWindowCommand))]
    [ContentType(PythonCoreConstants.ContentType)]
    class LoadReplCommand : IInteractiveWindowCommand
    {
        const string _commentPrefix = "%%";

        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments)
        {
            var finder = new FileFinder(arguments);

            var eval = window.GetPythonEvaluator();
            if (eval != null)
            {
                finder.Search(eval.Configuration.WorkingDirectory);
                foreach (var p in eval.Configuration.SearchPaths.MaybeEnumerate())
                {
                    finder.Search(p);
                }
            }

            finder.ThrowIfNotFound();
            string commandPrefix = "$";
            string lineBreak = window.TextView.Options.GetNewLineCharacter();

            IEnumerable<string> lines = File.ReadLines(finder.Filename);
            IEnumerable<string> submissions;

            if (eval != null)
            {
                submissions = ReplEditFilter.JoinToCompleteStatements(lines, eval.LanguageVersion).Where(CommentPrefixPredicate);
            }
            else
            {
                // v1 behavior, will probably never be hit, but if someone was developing their own IReplEvaluator
                // and using this class it would be hit.
                var submissionList = new List<string>();
                var currentSubmission = new List<string>();

                foreach (var line in lines)
                {
                    if (line.StartsWithOrdinal(_commentPrefix, ignoreCase: true))
                    {
                        continue;
                    }

                    if (line.StartsWithOrdinal(commandPrefix, ignoreCase: true))
                    {
                        AddSubmission(submissionList, currentSubmission, lineBreak);

                        submissionList.Add(line);
                        currentSubmission.Clear();
                    }
                    else
                    {
                        currentSubmission.Add(line);
                    }
                }

                AddSubmission(submissionList, currentSubmission, lineBreak);

                submissions = submissionList;
            }

            window.SubmitAsync(submissions);
            return ExecutionResult.Succeeded;
        }

        private static bool CommentPrefixPredicate(string input)
        {
            return !input.StartsWithOrdinal(_commentPrefix, ignoreCase: true);
        }

        private static void AddSubmission(List<string> submissions, List<string> lines, string lineBreak)
        {
            string submission = String.Join(lineBreak, lines);

            // skip empty submissions:
            if (submission.Length > 0)
            {
                submissions.Add(submission);
            }
        }

        public string Description
        {
            get { return Strings.ReplLoadCommandDescription; }
        }

        public string Command
        {
            get { return "load"; }
        }

        public IEnumerable<ClassificationSpan> ClassifyArguments(ITextSnapshot snapshot, Span argumentsSpan, Span spanToClassify)
        {
            yield break;
        }

        public string CommandLine
        {
            get
            {
                return "";
            }
        }

        public IEnumerable<string> DetailedDescription
        {
            get
            {
                yield return Description;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription
        {
            get
            {
                yield break;
            }
        }

        public IEnumerable<string> Names
        {
            get
            {
                yield return Command;
            }
        }

        class FileFinder
        {
            private readonly string _baseName;

            public FileFinder(string baseName)
            {
                _baseName = (baseName ?? "").Trim(' ', '\"');

                if (PathUtils.IsValidPath(_baseName) && Path.IsPathRooted(_baseName) && File.Exists(_baseName))
                {
                    Found = true;
                    Filename = _baseName;
                }
            }

            /// <summary>
            /// Searches the specified path and changes <see cref="Found"/> to
            /// true if the file exists. Returns true if the file was found in
            /// the provided path.
            /// </summary>
            public bool Search(string path)
            {
                if (Found)
                {
                    // File was found, but not in this path
                    return false;
                }

                if (!PathUtils.IsValidPath(path) || !Path.IsPathRooted(path))
                {
                    return false;
                }

                var fullPath = PathUtils.GetAbsoluteFilePath(path, _baseName);
                if (File.Exists(fullPath))
                {
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
            public bool SearchAll(string paths, char separator)
            {
                if (Found)
                {
                    // File was found, but not in this path
                    return false;
                }

                if (string.IsNullOrEmpty(paths))
                {
                    return false;
                }

                return SearchAll(paths.Split(separator));
            }

            /// <summary>
            /// Searches each path in the sequence as if they had been passed
            /// to <see cref="Search"/> individually.
            /// </summary>
            public bool SearchAll(IEnumerable<string> paths)
            {
                if (Found)
                {
                    // File was found, but not in this path
                    return false;
                }

                if (paths == null)
                {
                    return false;
                }

                foreach (var path in paths)
                {
                    if (Search(path))
                    {
                        return true;
                    }
                }
                return false;
            }

            [DebuggerStepThrough, DebuggerHidden]
            public void ThrowIfNotFound()
            {
                if (!Found)
                {
                    throw new FileNotFoundException(Strings.ReplLoadCommandFileNotFoundException, _baseName);
                }
            }

            public bool Found { get; private set; }

            public string Filename { get; private set; }
        }
    }
}
