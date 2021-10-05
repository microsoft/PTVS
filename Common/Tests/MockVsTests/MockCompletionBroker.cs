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
	[Export(typeof(ICompletionBroker))]
	class MockCompletionBroker : ICompletionBroker
	{
		internal readonly IEnumerable<Lazy<ICompletionSourceProvider, IContentTypeMetadata>> _completionProviders;
		internal readonly IIntellisenseSessionStackMapService _stackMap;

		[ImportingConstructor]
		public MockCompletionBroker(IIntellisenseSessionStackMapService stackMap, [ImportMany] IEnumerable<Lazy<ICompletionSourceProvider, IContentTypeMetadata>> completionProviders)
		{
			_stackMap = stackMap;
			_completionProviders = completionProviders;
		}

		public ICompletionSession CreateCompletionSession(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret)
		{
			return new MockCompletionSession(this, textView, triggerPoint);
		}

		public void DismissAllSessions(ITextView textView)
		{
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ICompletionSession)
				{
					session.Dismiss();
				}
			}
		}

		public ReadOnlyCollection<ICompletionSession> GetSessions(ITextView textView)
		{
			List<ICompletionSession> res = new List<ICompletionSession>();
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ICompletionSession)
				{
					res.Add(session as ICompletionSession);
				}
			}
			return new ReadOnlyCollection<ICompletionSession>(res);
		}

		public bool IsCompletionActive(ITextView textView)
		{
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ICompletionSession)
				{
					return true;
				}
			}
			return false;
		}

		public ICompletionSession TriggerCompletion(ITextView textView, ITrackingPoint triggerPoint, bool trackCaret)
		{
			var session = CreateCompletionSession(textView, triggerPoint, trackCaret);

			session.Start();

			return session;
		}

		public ICompletionSession TriggerCompletion(ITextView textView)
		{
			return TriggerCompletion(
				textView,
				textView.TextBuffer.CurrentSnapshot.CreateTrackingPoint(
					textView.Caret.Position.BufferPosition.Position,
					PointTrackingMode.Negative
				),
				true
			);
		}
	}
}
