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

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// The possible modes for a <see cref="FuzzyStringMatcher"/>.
    /// </summary>
    enum FuzzyMatchMode
    {
        Prefix = 0,
        PrefixIgnoreCase = 1,
        Substring = 2,
        SubstringIgnoreCase = 3,
        Fuzzy = 4,
        FuzzyIgnoreCase = 5,
        FuzzyIgnoreLowerCase = 6,
        Regex = 7,
        RegexIgnoreCase = 8,

        Default = FuzzyIgnoreLowerCase,
    }

    /// <summary>
    /// Compares strings against patterns for sorting and filtering.
    /// </summary>
    class FuzzyStringMatcher
    {
        delegate int Matcher(string text, string pattern, bool ignoreCase);
        readonly Matcher _matcher;
        readonly bool _ignoreCase;

        readonly static bool[] _ignoreCaseMap = new[] { false, true, false, true, false, true, false, false, true };
        readonly static Matcher[] _matcherMap = new Matcher[] {
            PrefixMatch, PrefixMatch,
            SubstringMatch, SubstringMatch,
            FuzzyMatch, FuzzyMatch,
            FuzzyMatchIgnoreLowerCase,
            RegexMatch, RegexMatch
        };

        public FuzzyStringMatcher(FuzzyMatchMode mode)
        {
            _ignoreCase = _ignoreCaseMap[(int)mode];
            _matcher = _matcherMap[(int)mode];
        }

        /// <summary>
        /// Returns an integer indicating how well text matches pattern. Larger
        /// values indicate a better match.
        /// </summary>
        public int GetSortKey(string text, string pattern)
        {
            return _matcher(text, pattern, _ignoreCase);
        }

        /// <summary>
        /// Returns false if text does not match pattern well enough to be
        /// displayed.
        /// </summary>
        public bool IsCandidateMatch(string text, string pattern)
        {
            return IsCandidateMatch(text, pattern, _matcher(text, pattern, _ignoreCase));
        }

        /// <summary>
        /// Returns false if text does not match pattern well enough to be
        /// displayed.
        /// </summary>
        /// <remarks>
        /// Use this overload if the sort key is already available, as this will
        /// avoid recalculating it.
        /// </remarks>
        public bool IsCandidateMatch(string text, string pattern, int sortKey)
        {
            return sortKey >= pattern.Length;
        }

        static int PrefixMatch(string text, string pattern, bool ignoreCase)
        {
            if (text.StartsWithOrdinal(pattern) || text.StartsWith(pattern, StringComparison.CurrentCulture))
            {
                return pattern.Length * 2 + (text.Length == pattern.Length ? 1 : 0);
            }
            else if (ignoreCase && (text.StartsWithOrdinal(pattern, ignoreCase: true) || text.StartsWith(pattern, StringComparison.CurrentCultureIgnoreCase)))
            {
                return pattern.Length + (text.Length == pattern.Length ? 1 : 0);
            }
            else
            {
                return 0;
            }
        }

        static int SubstringMatch(string text, string pattern, bool ignoreCase)
        {
            int position = text.IndexOfOrdinal(pattern);
            if (position >= 0)
            {
                return pattern.Length * 2 + (position == 0 ? 1 : 0);
            }
            position = text.IndexOf(pattern, StringComparison.CurrentCulture);
            if (position >= 0)
            {
                return pattern.Length * 2 + (position == 0 ? 1 : 0);
            }
            if (ignoreCase)
            {
                position = text.IndexOfOrdinal(pattern, ignoreCase: true);
                if (position >= 0)
                {
                    return pattern.Length + (position == 0 ? 1 : 0);
                }
                position = text.IndexOf(pattern, StringComparison.CurrentCultureIgnoreCase);
                if (position >= 0)
                {
                    return pattern.Length + (position == 0 ? 1 : 0);
                }
            }
            return 0;
        }

        static int RegexMatch(string text, string pattern, bool ignoreCase)
        {
            try
            {
                var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
                if (match != null && match.Success)
                {
                    return match.Value.Length * 2 + (match.Index == 0 ? 1 : 0);
                }
                match = Regex.Match(text, pattern);
                if (match != null && match.Success)
                {
                    return match.Value.Length * 2 + (match.Index == 0 ? 1 : 0);
                }
                if (ignoreCase)
                {
                    match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (match != null && match.Success)
                    {
                        return match.Value.Length + (match.Index == 0 ? 1 : 0);
                    }
                    match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match != null && match.Success)
                    {
                        return match.Value.Length + (match.Index == 0 ? 1 : 0);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                Trace.TraceWarning("Exception in Regex.Match(\"{0}\", \"{1}\"): {2}", text, pattern, ex);
            }
            return 0;
        }

        /// <summary>
        /// The reward for the first matching character.
        /// </summary>
        const int BASE_REWARD = 1;
        /// <summary>
        /// The amount to increase the reward for each consecutive character.
        /// This bonus is cumulative for each character.
        /// </summary>
        const int CONSECUTIVE_BONUS = 1;
        /// <summary>
        /// The amount to increase the reward at the start of the word. This
        /// bonus is applied once but remains for each consecutive character.
        /// </summary>
        const int START_OF_WORD_BONUS = 4;
        /// <summary>
        /// The amount to increase the reward after an underscore. This bonus
        /// is applied once but remains for each consecutive character.
        /// </summary>
        const int AFTER_UNDERSCORE_BONUS = 3;
        /// <summary>
        /// The amount to increase the reward for case-sensitive matches where
        /// the user typed an uppercase character. This bonus is only applied
        /// for the matching character.
        /// </summary>
        const int MATCHED_UPPERCASE_BONUS = 1;
        /// <summary>
        /// The amount to increase the reward for case-insensitive matches when
        /// the user typed a lowercase character. This bonus is only applied
        /// for the matching character, and is intended to be negative.
        /// </summary>
        const int EXPECTED_LOWERCASE_BONUS = -1;
        /// <summary>
        /// The amount to increase the reward for case-insensitive matches when
        /// the user typed an uppercase character. This bonus is only applied
        /// for the matching character, and is intended to be negative.
        /// </summary>
        const int EXPECTED_UPPERCASE_BONUS = -2;


        static int FuzzyMatchInternal(string text, string pattern, bool ignoreLowerCase, bool ignoreUpperCase)
        {
            if (text == null || pattern == null)
            {
                return 0;
            }
            int total = 0;
            int increment = BASE_REWARD + START_OF_WORD_BONUS;
            int y = 0;

            try
            {
                checked
                {
                    var cmp1 = CultureInfo.InvariantCulture.CompareInfo;
                    var cmp2 = CultureInfo.CurrentCulture.CompareInfo;
                    for (int x = 0; x < text.Length; ++x)
                    {
                        if (y >= pattern.Length)
                        {
                            // Prevent bonus for y == pattern.Length
                            y += 1;
                            break;
                        }

                        if (cmp1.Compare(text, x, 1, pattern, y, 1, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth) == 0 ||
                            cmp2.Compare(text, x, 1, pattern, y, 1, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth) == 0)
                        {
                            if (char.IsUpper(pattern, y))
                            {
                                if (char.IsUpper(text, x))
                                {
                                    // Apply a bonus for case-sensitive matches
                                    // when the user has typed an uppercase
                                    // character.
                                    total += increment + MATCHED_UPPERCASE_BONUS;
                                    increment += CONSECUTIVE_BONUS;
                                    y += 1;
                                }
                                else if (ignoreUpperCase)
                                {
                                    // The user typed uppercase and it matched
                                    // lowercase, so reward with a slight
                                    // penalty.
                                    total += increment + EXPECTED_UPPERCASE_BONUS;
                                    increment += CONSECUTIVE_BONUS;
                                    y += 1;
                                }
                                else
                                {
                                    // The user typed uppercase and it matched
                                    // lowercase.
                                    increment = BASE_REWARD;
                                }
                            }
                            else
                            {
                                if (char.IsLower(text, x))
                                {
                                    // The user typed lowercase and it matched
                                    // lowercase.
                                    total += increment;
                                    increment += CONSECUTIVE_BONUS;
                                    y += 1;
                                }
                                else if (ignoreLowerCase)
                                {
                                    // The user typed lowercase and it matched
                                    // uppercase, so reward with a slight
                                    // penalty.
                                    total += increment + EXPECTED_LOWERCASE_BONUS;
                                    increment += CONSECUTIVE_BONUS;
                                    y += 1;
                                }
                                else
                                {
                                    // The user typed lowercase and it matched
                                    // uppercase, but we don't care.
                                    increment = BASE_REWARD;
                                }
                            }
                        }
                        else if (text[x] == '_')
                        {
                            increment = BASE_REWARD + AFTER_UNDERSCORE_BONUS;
                        }
                        else
                        {
                            increment = BASE_REWARD;
                        }
                    }

                    if (y < pattern.Length)
                    {
                        total = 0;
                    }
                }
            }
            catch (OverflowException)
            {
                return int.MaxValue;
            }
            return total;
        }

        static int FuzzyMatch(string text, string pattern, bool ignoreCase)
        {
            return FuzzyMatchInternal(text, pattern, ignoreCase, ignoreCase);
        }

        static int FuzzyMatchIgnoreLowerCase(string text, string pattern, bool ignoreCase)
        {
            int total = FuzzyMatchInternal(text, pattern, true, false);
            if (total == 0)
            {
                total = FuzzyMatchInternal(text, pattern, true, true);
            }
            return total;
        }
    }
}
