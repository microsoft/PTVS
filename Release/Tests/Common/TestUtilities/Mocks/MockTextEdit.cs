/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System;
using System.Text;

namespace TestUtilities.Mocks {
    public class MockTextEdit : ITextEdit {
        private readonly List<Edit> _edits = new List<Edit>();
        private readonly ITextSnapshot _snapshot;
        private bool _canceled, _applied;

        public MockTextEdit(ITextSnapshot snapshot) {
            _snapshot = snapshot;
        }

        public bool Delete(int startPosition, int charsToDelete) {
            _edits.Add(new DeletionEdit(startPosition, charsToDelete));
            return true;
        }

        public bool Delete(Span deleteSpan) {
            return Delete(deleteSpan.Start, deleteSpan.Length);
        }

        public bool HasEffectiveChanges {
            get { throw new System.NotImplementedException(); }
        }

        public bool HasFailedChanges {
            get { throw new System.NotImplementedException(); }
        }

        public bool Insert(int position, char[] characterBuffer, int startIndex, int length) {
            return Insert(position, new String(characterBuffer, startIndex, length));
        }

        public bool Insert(int position, string text) {
            _edits.Add(new InsertionEdit(position, text));
            return true;
        }

        public bool Replace(int startPosition, int charsToReplace, string replaceWith) {
            Delete(startPosition, charsToReplace);
            Insert(startPosition, replaceWith);
            return true;
        }

        public bool Replace(Span replaceSpan, string replaceWith) {
            return Replace(replaceSpan.Start, replaceSpan.Length, replaceWith);
        }

        private static int CompareEdits(Edit left, Edit right) {
            int res = right.Position - left.Position;
            if (res == 0) {
                if (left is InsertionEdit) {
                    if (right is DeletionEdit) {
                        res = 1;
                    }
                } else if (right is InsertionEdit) {
                    res = -1;
                }
            }
            return res;
        }

        public ITextSnapshot Apply() {
            _edits.Sort(CompareEdits);

            // this works for non-overlapping edits...
            StringBuilder text = new StringBuilder(_snapshot.GetText());
            foreach (var edit in _edits) {
                InsertionEdit insert = edit as InsertionEdit;
                if (insert != null) {
                    text.Insert(insert.Position, insert.Text);
                } else {
                    DeletionEdit delete = edit as DeletionEdit;
                    text.Remove(delete.Position, delete.Length);
                }
            }

            var res = ((MockTextBuffer)_snapshot.TextBuffer)._snapshot = new MockTextSnapshot((MockTextBuffer)_snapshot.TextBuffer, text.ToString());
            _applied = true;
            return res;
        }

        public void Cancel() {
            _edits.Clear();
            _canceled = true;
        }

        public bool Canceled {
            get { return _canceled; }
        }

        public ITextSnapshot Snapshot {
            get { return _snapshot; }
        }

        public void Dispose() {
            if (!_applied) {
                Cancel();
            }
        }

        class Edit {
            public readonly int Position;

            public Edit(int position) {
                Position = position;
            }
        }

        sealed class InsertionEdit : Edit {
            public readonly string Text;

            public InsertionEdit(int position, string text)
                : base(position) {
                Text = text;
            }
        }

        sealed class DeletionEdit : Edit {
            public readonly int Length;

            public DeletionEdit(int startPosition, int charsToDelete)
                : base(startPosition) {
                Length = charsToDelete;
            }
        }
    }
}
