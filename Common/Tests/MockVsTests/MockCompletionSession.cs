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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	internal class MockCompletionSession : ICompletionSession
	{
		private readonly MockCompletionBroker _broker;
		private readonly ObservableCollection<CompletionSet> _sets;
		private readonly ITrackingPoint _triggerPoint;

		public MockCompletionSession(MockCompletionBroker broker, ITextView view, ITrackingPoint triggerPoint)
		{
			_broker = broker;
			TextView = view;
			_sets = new ObservableCollection<CompletionSet>();
			_sets.CollectionChanged += sets_CollectionChanged;
			_triggerPoint = triggerPoint;
			CompletionSets = new ReadOnlyObservableCollection<CompletionSet>(_sets);
		}

		private void sets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Add)
			{
				throw new NotImplementedException();
			}
			if (SelectedCompletionSet == null)
			{
				SelectedCompletionSet = CompletionSets[0];
			}
		}

		public void Commit()
		{
			if (SelectedCompletionSet != null)
			{
				Completion selectedCompletion = SelectedCompletionSet.SelectionStatus.Completion;
				if (selectedCompletion != null && selectedCompletion.InsertionText != null)
				{
					ITrackingSpan applicableTo = SelectedCompletionSet.ApplicableTo;
					ITextBuffer buffer = applicableTo.TextBuffer;
					ITextSnapshot snapshot = buffer.CurrentSnapshot;
					SnapshotSpan replaceSpan = applicableTo.GetSpan(snapshot);

					buffer.Replace(replaceSpan.Span, selectedCompletion.InsertionText);
					TextView.Caret.EnsureVisible();
				}
			}

			Committed?.Invoke(this, EventArgs.Empty);
			Dismiss();
		}

		public event EventHandler Committed;

		public ReadOnlyObservableCollection<CompletionSet> CompletionSets { get; }

		public void Filter()
		{
			foreach (CompletionSet completionSet in CompletionSets)
			{
				completionSet.Filter();
			}

			// Now that we're through, see if there's a better match out there.
			Match();
		}

		public bool IsStarted { get; private set; }

		public CompletionSet SelectedCompletionSet { get; set; }

		public event EventHandler<ValueChangedEventArgs<CompletionSet>> SelectedCompletionSetChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public void Collapse()
		{
			throw new NotImplementedException();
		}

		public void Dismiss()
		{
			IsDismissed = true;
			Dismissed?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler Dismissed;

		public SnapshotPoint? GetTriggerPoint(ITextSnapshot textSnapshot)
		{
			return GetTriggerPoint(textSnapshot.TextBuffer).GetPoint(textSnapshot);
		}

		public ITrackingPoint GetTriggerPoint(ITextBuffer textBuffer)
		{
			if (textBuffer == _triggerPoint.TextBuffer)
			{
				return _triggerPoint;
			}
			throw new NotImplementedException();
		}

		public bool IsDismissed { get; private set; }

		public bool Match()
		{
			foreach (CompletionSet completionSet in CompletionSets)
			{
				completionSet.SelectBestMatch();
			}

			return true;
		}

		public IIntellisensePresenter Presenter => throw new NotImplementedException();

		public event EventHandler PresenterChanged
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public void Recalculate()
		{
			throw new NotImplementedException();
		}

		public event EventHandler Recalculated
		{
			add { throw new NotImplementedException(); }
			remove { throw new NotImplementedException(); }
		}

		public void Start()
		{
			if (IsStarted)
			{
				throw new InvalidOperationException("Session has already been started");
			}
			IsStarted = true;

			foreach (var provider in _broker._completionProviders)
			{
				foreach (var targetContentType in provider.Metadata.ContentTypes)
				{
					if (TextView.TextBuffer.ContentType.IsOfType(targetContentType))
					{
						var source = provider.Value.TryCreateCompletionSource(TextView.TextBuffer);
						if (source != null)
						{
							source.AugmentCompletionSession(this, _sets);
						}
					}
				}
			}

			if (_sets.Count > 0 && !IsDismissed)
			{
				_broker._stackMap.GetStackForTextView(TextView).PushSession(this);
			}
		}

		public ITextView TextView { get; }

		public PropertyCollection Properties { get; } = new PropertyCollection();
	}
}
