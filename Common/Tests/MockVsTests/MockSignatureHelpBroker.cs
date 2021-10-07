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
	[Export(typeof(ISignatureHelpBroker))]
	internal class MockSignatureHelpBroker : ISignatureHelpBroker
	{
		private readonly IEnumerable<Lazy<ISignatureHelpSourceProvider, IContentTypeMetadata>> _sigProviders;
		private readonly IIntellisenseSessionStackMapService _stackMap;

		[ImportingConstructor]
		public MockSignatureHelpBroker(IIntellisenseSessionStackMapService stackMap, [ImportMany] IEnumerable<Lazy<ISignatureHelpSourceProvider, IContentTypeMetadata>> sigProviders)
		{
			_stackMap = stackMap;
			_sigProviders = sigProviders;
		}

		public ISignatureHelpSession CreateSignatureHelpSession(VisualStudio.Text.Editor.ITextView textView, VisualStudio.Text.ITrackingPoint triggerPoint, bool trackCaret)
		{
			throw new NotImplementedException();
		}

		public void DismissAllSessions(VisualStudio.Text.Editor.ITextView textView)
		{
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ISignatureHelpSession)
				{
					session.Dismiss();
				}
			}
		}

		public ReadOnlyCollection<ISignatureHelpSession> GetSessions(VisualStudio.Text.Editor.ITextView textView)
		{
			List<ISignatureHelpSession> res = new List<ISignatureHelpSession>();
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ISignatureHelpSession)
				{
					res.Add(session as ISignatureHelpSession);
				}
			}
			return new ReadOnlyCollection<ISignatureHelpSession>(res);
		}

		public bool IsSignatureHelpActive(VisualStudio.Text.Editor.ITextView textView)
		{
			foreach (var session in _stackMap.GetStackForTextView(textView).Sessions)
			{
				if (session is ISignatureHelpSession)
				{
					return true;
				}
			}
			return false;
		}

		public ISignatureHelpSession TriggerSignatureHelp(VisualStudio.Text.Editor.ITextView textView, VisualStudio.Text.ITrackingPoint triggerPoint, bool trackCaret)
		{
			throw new NotImplementedException();
		}

		public ISignatureHelpSession TriggerSignatureHelp(VisualStudio.Text.Editor.ITextView textView)
		{
			ObservableCollection<ISignature> sets = new ObservableCollection<ISignature>();
			MockSignatureHelpSession session = new MockSignatureHelpSession(
				textView,
				sets,
				textView.TextBuffer.CurrentSnapshot.CreateTrackingPoint(
					textView.Caret.Position.BufferPosition.Position,
					PointTrackingMode.Negative
				)
			);

			foreach (var provider in _sigProviders)
			{
				foreach (var targetContentType in provider.Metadata.ContentTypes)
				{
					if (textView.TextBuffer.ContentType.IsOfType(targetContentType))
					{
						var source = provider.Value.TryCreateSignatureHelpSource(textView.TextBuffer);
						if (source != null)
						{
							source.AugmentSignatureHelpSession(session, sets);
						}
					}
				}
			}

			if (session.Signatures.Count > 0 && !session.IsDismissed)
			{
				_stackMap.GetStackForTextView(textView).PushSession(session);
			}

			return session;
		}
	}
}
