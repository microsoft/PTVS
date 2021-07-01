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
    /// Represents a completion item that can be individually shown and hidden.
    /// A completion set using these as items needs to provide a filter that
    /// checks the Visible property.
    /// </summary>
    class DynamicallyVisibleCompletion : Completion
    {
        private Func<string> _lazyDescriptionSource;
        private Func<ImageSource> _lazyIconSource;
        private bool _visible, _previouslyVisible;

        /// <summary>
        /// Creates a default completion item.
        /// </summary>
        public DynamicallyVisibleCompletion()
            : base() { }

        /// <summary>
        /// Creates a completion item with the specifed text, which will be
        /// used both for display and insertion.
        /// </summary>
        public DynamicallyVisibleCompletion(string displayText)
            : base(displayText) { }

        /// <summary>
        /// Initializes a new instance with the specified text and description.
        /// </summary>
        /// <param name="displayText">The text that is to be displayed by an
        /// IntelliSense presenter.</param>
        /// <param name="insertionText">The text that is to be inserted into
        /// the buffer if this completion is committed.</param>
        /// <param name="description">A description that can be displayed with
        /// the display text of the completion.</param>
        /// <param name="iconSource">The icon.</param>
        /// <param name="iconAutomationText">The text to be used as the
        /// automation name for the icon.</param>
        public DynamicallyVisibleCompletion(string displayText, string insertionText, string description, ImageSource iconSource, string iconAutomationText)
            : base(displayText, insertionText, description, iconSource, iconAutomationText) { }


        /// <summary>
        /// Initializes a new instance with the specified text, description and
        /// a lazily initialized icon.
        /// </summary>
        /// <param name="displayText">The text that is to be displayed by an
        /// IntelliSense presenter.</param>
        /// <param name="insertionText">The text that is to be inserted into
        /// the buffer if this completion is committed.</param>
        /// <param name="lazyDescriptionSource">A function returning the
        /// description.</param>
        /// <param name="lazyIconSource">A function returning the icon. It will
        /// be called once and the result is cached.</param>
        /// <param name="iconAutomationText">The text to be used as the
        /// automation name for the icon.</param>
        public DynamicallyVisibleCompletion(string displayText, string insertionText, Func<string> lazyDescriptionSource, Func<ImageSource> lazyIconSource, string iconAutomationText)
            : base(displayText, insertionText, null, null, iconAutomationText)
        {
            _lazyDescriptionSource = lazyDescriptionSource;
            _lazyIconSource = lazyIconSource;
        }

        /// <summary>
        /// Gets or sets whether the completion item should be shown to the
        /// user.
        /// </summary>
        internal bool Visible
        {
            get
            {
                return _visible;
            }
            set
            {
                _previouslyVisible = _visible;
                _visible = value;
            }
        }

        /// <summary>
        /// Resets <see cref="Visible"/> to its value before it was last set.
        /// </summary>
        internal void UndoVisible()
        {
            _visible = _previouslyVisible;
        }


        // Summary:
        //     Gets a description that can be displayed together with the display text of
        //     the completion.
        //
        // Returns:
        //     The description.

        /// <summary>
        /// Gets a description that can be displayed together with the display
        /// text of the completion.
        /// </summary>
        /// <value>The description.</value>
        public override string Description
        {
            get
            {
                if (base.Description == null && _lazyDescriptionSource != null)
                {
                    base.Description = _lazyDescriptionSource();
                    _lazyDescriptionSource = null;
                }
                return base.Description.LimitLines();
            }
            set
            {
                base.Description = value;
            }
        }

        /// <summary>
        /// Gets or sets an icon that could be used to describe the completion.
        /// </summary>
        /// <value>The icon.</value>
        public override ImageSource IconSource
        {
            get
            {
                if (base.IconSource == null && _lazyIconSource != null)
                {
                    base.IconSource = _lazyIconSource();
                    _lazyIconSource = null;
                }
                return base.IconSource;
            }
            set
            {
                base.IconSource = value;
            }
        }
    }

    /// <summary>
    /// Represents a set of completions filtered and selected using a
    /// <see cref="FuzzyStringMatcher"/>.
    /// </summary>
    class FuzzyCompletionSet : CompletionSet
    {
        private readonly BulkObservableCollection<Completion> _completions;
        private readonly FilteredObservableCollection<Completion> _filteredCompletions;
        private readonly FuzzyStringMatcher _comparer;
        private readonly bool _shouldFilter;
        private readonly bool _shouldHideAdvanced;
        private readonly bool _matchInsertionText;

        private Completion _previousSelection;

        public const bool DefaultCommitByDefault = true;

        private readonly static Regex _advancedItemPattern = new Regex(
            @"__\w+__($|\s)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        private readonly static IList<Completion> _noCompletions = new[] { new Completion(
            Strings.NoCompletionsCompletion,
            string.Empty,
            Strings.WarningUnknownType,
            null,
            null
        ) };

        /// <summary>
        /// Initializes a new instance with the specified properties.
        /// </summary>
        /// <param name="moniker">The unique, non-localized identifier for the
        /// completion set.</param>
        /// <param name="displayName">The localized name of the completion set.
        /// </param>
        /// <param name="applicableTo">The tracking span to which the
        /// completions apply.</param>
        /// <param name="completions">The list of completions.</param>
        /// <param name="options">The options to use for filtering and
        /// selecting items.</param>
        /// <param name="comparer">The comparer to use to order the provided
        /// completions.</param>
        /// <param name="matchInsertionText">If true, matches user input against
        /// the insertion text; otherwise, uses the display text.</param>
        public FuzzyCompletionSet(
            string moniker,
            string displayName,
            ITrackingSpan applicableTo,
            IEnumerable<DynamicallyVisibleCompletion> completions,
            CompletionOptions options,
            IComparer<Completion> comparer,
            bool matchInsertionText = false
        ) :
            base(moniker, displayName, applicableTo, null, null)
        {
            _matchInsertionText = matchInsertionText;
            _completions = new BulkObservableCollection<Completion>();
            _completions.AddRange(completions
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.DisplayText))
                .OrderBy(c => c, comparer)
            );
            _comparer = new FuzzyStringMatcher(FuzzyMatchMode.Default);

            _shouldFilter = options.FilterCompletions;
            _shouldHideAdvanced = options.HideAdvancedMembers && !_completions.All(IsAdvanced);

            if (!_completions.Any())
            {
                _completions = null;
            }

            if (_completions != null && _shouldFilter | _shouldHideAdvanced)
            {
                _filteredCompletions = new FilteredObservableCollection<Completion>(_completions);

                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = !_shouldHideAdvanced || !IsAdvanced(c);
                }
                _filteredCompletions.Filter(IsVisible);
            }

            CommitByDefault = DefaultCommitByDefault;
        }

        private bool IsAdvanced(Completion comp)
        {
            return _advancedItemPattern.IsMatch(_matchInsertionText ? comp.InsertionText : comp.DisplayText);
        }

        /// <summary>
        /// If True, the best item is selected by default. Otherwise, the user
        /// will need to manually select it before committing.
        /// </summary>
        /// <remarks>
        /// By default, this is set to <see cref="DefaultCommitByDefault"/>
        /// </remarks>
        public bool CommitByDefault { get; set; }

        /// <summary>
        /// Gets or sets the list of completions that are part of this completion set.
        /// </summary>
        /// <value>
        /// A list of <see cref="Completion"/> objects.
        /// </value>
        public override IList<Completion> Completions => _filteredCompletions ?? _completions ?? _noCompletions;

        private static bool IsVisible(Completion completion)
        {
            return ((DynamicallyVisibleCompletion)completion).Visible;
        }

        /// <summary>
        /// Restricts the set of completions to those that match the applicability text
        /// of the completion set, and then determines the best match.
        /// </summary>
        public override void Filter()
        {
            if (_completions == null)
            {
                return;
            }

            if (_filteredCompletions == null)
            {
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    c.Visible = true;
                }
                return;
            }

            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

            if (!string.IsNullOrEmpty(text))
            {
                FilterToText(text);
            }
            else if (_shouldHideAdvanced)
            {
                FilterToPredicate(c => !IsAdvanced(c));
            }
            else
            {
                FilterToPredicate(_ => true);
            }
        }

        private void FilterToPredicate(Func<Completion, bool> predicate)
        {
            bool allVisible = true;
            foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
            {
                bool v = c.Visible = predicate(c);
                allVisible &= v;
            }

            if (allVisible)
            {
                _filteredCompletions.StopFiltering();
            }
            else
            {
                _filteredCompletions.Filter(IsVisible);
            }
        }

        private void FilterToText(string filterText)
        {
            bool hideAdvanced = _shouldHideAdvanced && !filterText.StartsWithOrdinal("__");
            bool anyVisible = false;
            foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
            {
                if (hideAdvanced && IsAdvanced(c))
                {
                    c.Visible = false;
                }
                else if (_shouldFilter)
                {
                    c.Visible = _comparer.IsCandidateMatch(_matchInsertionText ? c.InsertionText : c.DisplayText, filterText);
                }
                else
                {
                    c.Visible = true;
                }
                anyVisible |= c.Visible;
            }
            if (!anyVisible)
            {
                foreach (var c in _completions.Cast<DynamicallyVisibleCompletion>())
                {
                    // UndoVisible only works reliably because we always
                    // set Visible in the previous loop.
                    c.UndoVisible();
                }
            }
            _filteredCompletions.Filter(IsVisible);
        }

        /// <summary>
        /// Determines the best match in the completion set.
        /// </summary>
        public override void SelectBestMatch()
        {
            if (_completions == null)
            {
                return;
            }

            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

            Completion bestMatch = _previousSelection;
            int bestValue = 0;
            bool isUnique = true;
            bool allowSelect = true;

            // Using the Completions property to only search through visible
            // completions.
            foreach (var comp in Completions)
            {
                int value = _comparer.GetSortKey(_matchInsertionText ? comp.InsertionText : comp.DisplayText, text);
                if (bestMatch == null || value > bestValue)
                {
                    bestMatch = comp;
                    bestValue = value;
                    isUnique = true;
                }
                else if (value == bestValue)
                {
                    isUnique = false;
                }
            }

            if (!CommitByDefault)
            {
                allowSelect = false;
                isUnique = false;
            }

            try
            {
                if ((bestMatch as DynamicallyVisibleCompletion)?.Visible == true)
                {
                    SelectionStatus = new CompletionSelectionStatus(
                        bestMatch,
                        isSelected: allowSelect && bestValue > 0,
                        isUnique: isUnique
                    );
                }
                else
                {
                    SelectionStatus = new CompletionSelectionStatus(
                        null,
                        isSelected: false,
                        isUnique: false
                    );
                }
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                ex.ReportUnhandledException(null, GetType(), allowUI: false);
            }

            _previousSelection = bestMatch;
        }

        /// <summary>
        /// Determines and selects the only match in the completion set.
        /// This ignores the user's filtering preferences.
        /// </summary>
        /// <returns>
        /// True if a match is found and selected; otherwise, false if there
        /// is no single match in the completion set.
        /// </returns> 
        public bool SelectSingleBest()
        {
            if (_completions == null)
            {
                return false;
            }

            var text = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

            Completion bestMatch = null;

            // Unfilter and then search all completions
            FilterToPredicate(_ => true);

            foreach (var comp in _completions)
            {
                if (_comparer.IsCandidateMatch(_matchInsertionText ? comp.InsertionText : comp.DisplayText, text))
                {
                    if (bestMatch == null)
                    {
                        bestMatch = comp;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            try
            {
                if (bestMatch != null)
                {
                    SelectionStatus = new CompletionSelectionStatus(
                        bestMatch,
                        isSelected: true,
                        isUnique: true
                    );
                    return true;
                }
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                ex.ReportUnhandledException(null, GetType(), allowUI: false);
            }

            return false;
        }
    }
}
