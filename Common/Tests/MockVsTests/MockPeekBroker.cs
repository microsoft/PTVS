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
	[Export(typeof(IPeekBroker))]
	internal class MockPeekBroker : IPeekBroker
	{
		public bool CanTriggerPeekSession(ITextView textView, string relationshipName, Predicate<string> isStandaloneFilePredicate)
		{
			throw new NotImplementedException();
		}

		public bool CanTriggerPeekSession(ITextView textView, ITrackingPoint triggerPoint, string relationshipName, Predicate<string> isStandaloneFilePredicate)
		{
			throw new NotImplementedException();
		}

		public bool CanTriggerPeekSession(PeekSessionCreationOptions options, Predicate<string> isStandaloneFilePredicate)
		{
			throw new NotImplementedException();
		}

		public IPeekSession CreatePeekSession(ITextView textView, ITrackingPoint triggerPoint, string relationshipName)
		{
			throw new NotImplementedException();
		}

		public IPeekSession CreatePeekSession(PeekSessionCreationOptions options)
		{
			throw new NotImplementedException();
		}

		public void DismissPeekSession(ITextView textView)
		{
			throw new NotImplementedException();
		}

		public IPeekSession GetPeekSession(ITextView textView)
		{
			throw new NotImplementedException();
		}

		public bool IsPeekSessionActive(ITextView textView)
		{
			throw new NotImplementedException();
		}

		public void TriggerNestedPeekSession(ITextView textView, string relationshipName, IPeekSession containingSession)
		{
			throw new NotImplementedException();
		}

		public void TriggerNestedPeekSession(ITextView textView, ITrackingPoint triggerPoint, string relationshipName, IPeekSession containingSession)
		{
			throw new NotImplementedException();
		}

		public void TriggerNestedPeekSession(PeekSessionCreationOptions options, IPeekSession containingSession)
		{
			throw new NotImplementedException();
		}

		public IPeekSession TriggerPeekSession(ITextView textView, string relationshipName)
		{
			throw new NotImplementedException();
		}

		public IPeekSession TriggerPeekSession(ITextView textView, ITrackingPoint triggerPoint, string relationshipName)
		{
			throw new NotImplementedException();
		}

		public IPeekSession TriggerPeekSession(PeekSessionCreationOptions options)
		{
			throw new NotImplementedException();
		}
	}
}
