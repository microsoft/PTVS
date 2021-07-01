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

namespace Microsoft.PythonTools.Editor
{
    internal static class ReplPromptHelpers
    {
        internal static readonly Regex PromptRegex = new Regex(
            @"^(
                \>\>\>(\s|\s*$)
              | \.\.\.(\s|\s*$)
              | \s*In\s*\[.*?\]\s*:(\s|\s*$)
              | \s*\.\.\.:(\s|\s*$)
               )",
            RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(0.5)
        );

        /// <summary>
        /// Given two snapshots, finds any inserted REPL prompts and removes
        /// them in a single edit.
        /// </summary>
        /// <returns>True if any changes were made.</returns>
        public static bool RemovePastedPrompts(ITextSnapshot before, ITextSnapshot after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }
            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }
            if (before.TextBuffer != after.TextBuffer)
            {
                throw new ArgumentException("'before' and 'after' must belong to the same buffer");
            }

            bool changeMade = false;

            using (var edit = after.TextBuffer.CreateEdit())
            {
                foreach (var change in EnumerateLineChanges(before.Version, after.Version))
                {
                    int len = GetPromptLength(change.Value);
                    if (len > 0)
                    {
                        edit.Replace(new Span(change.Key.Start, len), "");
                        changeMade = true;
                    }
                }
                if (changeMade)
                {
                    edit.Apply();
                }
            }

            return changeMade;
        }

        /// <summary>
        /// Given a block of text, removes all REPL prompts and
        /// optionally normalizes newlines.
        /// </summary>
        /// <param name="text">The text to remove prompts from.</param>
        /// <param name="newline">
        /// The newline to normalize to. If null or empty, newlines
        /// are not normalized.
        /// </param>
        public static string RemovePrompts(string text, string newline)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (string.IsNullOrEmpty(newline))
            {
                var lines = text.Split('\n')
                    .Select(l => l.Substring(GetPromptLength(l)));

                return string.Join("\n", lines);
            }
            else
            {
                var lines = text.Split('\n')
                    .Select(l => l.LastOrDefault() != '\r' ? l : l.Remove(l.Length - 1))
                    .Select(l => l.Substring(GetPromptLength(l)));

                return string.Join(newline, lines);
            }
        }



        private static int GetPromptLength(string line)
        {
            Match m;
            try
            {
                m = PromptRegex.Match(line);
            }
            catch (RegexMatchTimeoutException)
            {
                return 0;
            }
            return m.Success ? m.Length : 0;
        }

        private static IEnumerable<KeyValuePair<Span, string>> EnumerateLineChanges(ITextVersion v1, ITextVersion v2)
        {
            while (v1 != null && v1 != v2 && v1.VersionNumber < v2.VersionNumber)
            {
                foreach (var c in v1.Changes)
                {
                    int start = 0, next = c.NewText.IndexOf('\n');
                    while (next > start)
                    {
                        yield return new KeyValuePair<Span, string>(
                            new Span(c.NewPosition + start, next - start + 1),
                            c.NewText.Substring(start, next - start + 1)
                        );
                        start = next + 1;
                        next = c.NewText.IndexOf('\n', start);
                    }
                    yield return new KeyValuePair<Span, string>(
                        new Span(c.NewPosition + start, c.NewText.Length - start),
                        c.NewText.Substring(start)
                    );
                }
                v1 = v1.Next;
            }
        }

    }
}
