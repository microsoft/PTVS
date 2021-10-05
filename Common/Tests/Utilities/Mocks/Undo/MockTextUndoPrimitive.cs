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

namespace TestUtilities.Mocks
{
	internal class MockTextUndoPrimitive : ITextUndoPrimitive
	{
		private readonly Stack<Action> _undoOperations;
		private MockTextUndoTransaction _parent;
		private readonly MockTextUndoHistory _history;
		private DelegatedUndoPrimitiveState _state;

		public DelegatedUndoPrimitiveState State
		{
			get => _state;
			set => _state = value;
		}

		public MockTextUndoPrimitive(MockTextUndoHistory history, MockTextUndoTransaction parent, Action operationCurried)
		{
			RedoOperations = new Stack<Action>();
			_undoOperations = new Stack<Action>();

			_parent = parent;
			_history = history;
			_state = DelegatedUndoPrimitiveState.Inactive;

			_undoOperations.Push(operationCurried);
		}

		public bool CanRedo => RedoOperations.Count > 0;

		public bool CanUndo => _undoOperations.Count > 0;

		/// <summary>
		/// Here, we undo everything in the list of undo operations, and then clear the list. While this is happening, the
		/// History will collect new operations for the redo list and pass them on to us.
		/// </summary>
		public void Undo()
		{
			using (new CatchOperationsFromHistoryForDelegatedPrimitive(_history, this, DelegatedUndoPrimitiveState.Undoing))
			{
				while (_undoOperations.Count > 0)
				{
					_undoOperations.Pop()();
				}
			}
		}

		/// <summary>
		/// This is only called for "Redo," not for the original "Do." The action is to redo everything in the list of
		/// redo operations, and then clear the list. While this is happening, the History will collect new operations
		/// for the undo list and pass them on to us.
		/// </summary>
		public void Do()
		{
			using (new CatchOperationsFromHistoryForDelegatedPrimitive(_history, this, DelegatedUndoPrimitiveState.Redoing))
			{
				while (RedoOperations.Count > 0)
				{
					RedoOperations.Pop()();
				}
			}
		}

		public ITextUndoTransaction Parent
		{
			get => _parent;
			set => _parent = value as MockTextUndoTransaction;
		}

		/// <summary>
		/// This is called by the UndoHistory implementation when we are mid-undo/mid-redo and
		/// the history receives a new UndoableOperation. The action is then to add that operation
		/// to the inverse list.
		/// </summary>
		/// <param name="operation"></param>
		public void AddOperation(Action operation)
		{
			if (_state == DelegatedUndoPrimitiveState.Redoing)
			{
				_undoOperations.Push(operation);
			}
			else if (_state == DelegatedUndoPrimitiveState.Undoing)
			{
				RedoOperations.Push(operation);
			}
			else
			{
				throw new InvalidOperationException("Strings.DelegatedUndoPrimitiveStateDoesNotAllowAdd");
			}
		}

		public bool MergeWithPreviousOnly => true;

		internal Stack<Action> RedoOperations { get; }

		public bool CanMerge(ITextUndoPrimitive primitive)
		{
			return false;
		}

		public ITextUndoPrimitive Merge(ITextUndoPrimitive primitive)
		{
			throw new InvalidOperationException("Strings.DelegatedUndoPrimitiveCannotMerge");
		}
	}

}
