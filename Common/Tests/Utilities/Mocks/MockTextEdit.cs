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
    public class MockTextEdit : ITextEdit
    {
        private readonly List<Edit> _edits = new List<Edit>();
        private readonly MockTextSnapshot _snapshot;
        private bool _canceled, _applied;

        public MockTextEdit(MockTextSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public bool Delete(int startPosition, int charsToDelete)
        {
            _edits.Add(new DeletionEdit(startPosition, charsToDelete));
            return true;
        }

        public bool Delete(Span deleteSpan)
        {
            return Delete(deleteSpan.Start, deleteSpan.Length);
        }

        public bool HasEffectiveChanges
        {
            get { throw new System.NotImplementedException(); }
        }

        public bool HasFailedChanges
        {
            get { throw new System.NotImplementedException(); }
        }

        public bool Insert(int position, char[] characterBuffer, int startIndex, int length)
        {
            return Insert(position, new String(characterBuffer, startIndex, length));
        }

        public bool Insert(int position, string text)
        {
            _edits.Add(new InsertionEdit(position, text));
            return true;
        }

        public bool Replace(int startPosition, int charsToReplace, string replaceWith)
        {
            _edits.Add(new ReplacementEdit(startPosition, charsToReplace, replaceWith));
            return true;
        }

        public bool Replace(Span replaceSpan, string replaceWith)
        {
            return Replace(replaceSpan.Start, replaceSpan.Length, replaceWith);
        }

        private static int CompareEdits(Edit left, Edit right)
        {
            int res = right.Position - left.Position;
            if (res == 0)
            {
                if (left is InsertionEdit)
                {
                    if (right is DeletionEdit)
                    {
                        res = 1;
                    }
                }
                else if (right is InsertionEdit)
                {
                    res = -1;
                }
            }
            return res;
        }

        public ITextSnapshot Apply()
        {
            var text = new StringBuilder(_snapshot.GetText());
            var changes = new List<MockTextChange>();

            // Apply changes
            foreach (var edit in _edits.OrderByDescending(e => e.Position).ThenByDescending(e => e, EditTypeComparer.Instance))
            {
                MockTextChange change;
                if (edit is ReplacementEdit replace)
                {
                    text.Remove(replace.Position, replace.Length);
                    text.Insert(replace.Position, replace.Text);
                    change = new MockTextChange(
                        new SnapshotSpan(_snapshot, replace.Position, replace.Length),
                        replace.Position,
                        replace.Text
                    );
                }
                else if (edit is InsertionEdit insert)
                {
                    text.Insert(insert.Position, insert.Text);
                    change = new MockTextChange(
                        new SnapshotSpan(_snapshot, insert.Position, 0),
                        insert.Position,
                        insert.Text
                    );
                }
                else
                {
                    var delete = (DeletionEdit)edit;
                    text.Remove(delete.Position, delete.Length);
                    change = new MockTextChange(
                        new SnapshotSpan(_snapshot, delete.Position, delete.Length),
                        delete.Position,
                        string.Empty
                    );
                }

                changes.Add(change);
            }
            changes.Reverse();

            var previous = _snapshot;
            var res = ((MockTextBuffer)_snapshot.TextBuffer)._snapshot = new MockTextSnapshot(
                (MockTextBuffer)_snapshot.TextBuffer,
                text.ToString(),
                _snapshot,
                changes.ToArray()
            );
            _applied = true;
            ((MockTextBuffer)_snapshot.TextBuffer).EditApplied(previous);
            return res;
        }

        public void Cancel()
        {
            _edits.Clear();
            _canceled = true;
        }

        public bool Canceled
        {
            get { return _canceled; }
        }

        public ITextSnapshot Snapshot
        {
            get { return _snapshot; }
        }

        public void Dispose()
        {
            if (!_applied)
            {
                Cancel();
            }
        }

        private sealed class EditTypeComparer : IComparer<Edit>
        {
            public static IComparer<Edit> Instance = new EditTypeComparer();

            private EditTypeComparer() { }

            public int Compare(Edit x, Edit y) => (x is InsertionEdit).CompareTo(y is InsertionEdit);
        }

        class Edit
        {
            public readonly int Position;

            public Edit(int position)
            {
                Position = position;
            }
        }

        sealed class InsertionEdit : Edit
        {
            public readonly string Text;

            public InsertionEdit(int position, string text)
                : base(position)
            {
                Text = text;
            }

            public override string ToString()
            {
                return String.Format("<Insert Length={0} at {1}>", Text.Length, Position);
            }
        }

        sealed class DeletionEdit : Edit
        {
            public readonly int Length;

            public DeletionEdit(int startPosition, int charsToDelete)
                : base(startPosition)
            {
                Length = charsToDelete;
            }

            public override string ToString()
            {
                return String.Format("<Delete Length={0} at {1}>", Length, Position);
            }
        }

        sealed class ReplacementEdit : Edit
        {
            public readonly int Length;
            public readonly string Text;

            public ReplacementEdit(int startPosition, int charsToDelete, string text)
                : base(startPosition)
            {
                Length = charsToDelete;
                Text = text;
            }

            public override string ToString()
            {
                return String.Format("<Replace Length={0} at {1} with {2}>", Length, Position, Text.Length);
            }
        }
    }
}
