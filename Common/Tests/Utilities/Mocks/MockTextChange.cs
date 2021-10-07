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

namespace TestUtilities.Mocks
{
	internal class MockTextChange : ITextChange
	{
		private readonly SnapshotSpan _removed;
		private readonly string _inserted;
		private readonly int _newStart;
		private static readonly string[] NewLines = new[] { "\r\n", "\r", "\n" };

		public MockTextChange(SnapshotSpan removedSpan, int newStart, string insertedText)
		{
			_removed = removedSpan;
			_inserted = insertedText;
			_newStart = newStart;
		}

		public int Delta => _inserted.Length - _removed.Length;

		public int LineCountDelta => _inserted.Split(NewLines, StringSplitOptions.None).Length -
					_removed.GetText().Split(NewLines, StringSplitOptions.None).Length;

		public int NewEnd => NewPosition + _inserted.Length;

		public int NewLength => _inserted.Length;

		public int NewPosition => _newStart;

		public Span NewSpan => new Span(NewPosition, NewLength);

		public string NewText => _inserted;

		public int OldEnd => _removed.End;

		public int OldLength => _removed.Length;

		public int OldPosition => _removed.Start;

		public Span OldSpan => _removed.Span;

		public string OldText => _removed.GetText();
	}
}
