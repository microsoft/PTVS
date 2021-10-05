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
	internal class MockTextUndoTransaction : ITextUndoTransaction
	{
		private readonly MockTextUndoHistory _history;
		private readonly MockTextUndoTransaction _parent;

		private UndoTransactionState _state;
		private readonly List<ITextUndoPrimitive> _primitives;

		public MockTextUndoTransaction(ITextUndoHistory history, ITextUndoTransaction parent, string description)
		{
			_history = history as MockTextUndoHistory;
			_parent = parent as MockTextUndoTransaction;

			Description = description;

			_state = UndoTransactionState.Open;
			_primitives = new List<ITextUndoPrimitive>();
			MergePolicy = NullMergeUndoTransactionPolicy.Instance;
			IsReadOnly = true;
		}

		/// <summary>
		/// This is how you turn transaction into "Invalid" state. Use it to indicate that this transaction is retired forever,
		/// such as when clearing transactions from the redo stack.
		/// </summary>
		internal void Invalidate()
		{
			_state = UndoTransactionState.Invalid;
		}

		internal bool IsInvalid => _state == UndoTransactionState.Invalid;

		/// <summary>
		/// Used by UndoHistoryImpl.cs to allow UndoPrimitives to be modified during merging.
		/// </summary>
		internal bool IsReadOnly { get; set; }

		/// <summary>
		/// Description is the [localized] string that describes the transaction to a user.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// State is the UndoTransactionState for the UndoTransaction, as described in that type.
		/// </summary>
		public UndoTransactionState State => _state;

		/// <summary>
		/// History is a reference to the UndoHistory that contains this transaction.
		/// </summary>
		public ITextUndoHistory History => _history;

		/// <summary>
		/// UndoPrimitives allows access to the list of primitives in this transaction container, but should only be called
		/// after the transaction has been completed. 
		/// </summary>
		public IList<ITextUndoPrimitive> UndoPrimitives => IsReadOnly ? (IList<ITextUndoPrimitive>)_primitives.AsReadOnly() : _primitives;

		/// <summary>
		/// Complete marks the transaction finished and eligible for Undo.
		/// </summary>
		public void Complete()
		{
			if (State != UndoTransactionState.Open)
			{
				throw new InvalidOperationException("Complete called on transaction that is not opened");
			}

			_state = UndoTransactionState.Completed;

			// now we need to pump these primitives into the parent, if the parent exists.
			FlattenPrimitivesToParent();
		}

		/// <summary>
		/// This is called by the transaction when it is complete. It results in the parent getting
		/// all of this transaction's undo history, so that transactions are not really recursive (they
		/// exist for rollback).
		/// </summary>
		public void FlattenPrimitivesToParent()
		{
			if (_parent != null)
			{
				// first, copy up each primitive. 
				_parent.CopyPrimitivesFrom(this);

				// once all the primitives are in the parent, just clear them so
				// no one has a chance to tweak them here, or do/undo us.
				_primitives.Clear();
			}
		}

		/// <summary>
		/// Copies all of the primitives from the given transaction, and appends them to the UndoPrimitives list.
		/// </summary>
		/// <param name="transaction">The MockTextUndoTransaction to copy from.</param>
		public void CopyPrimitivesFrom(MockTextUndoTransaction transaction)
		{
			foreach (var p in transaction.UndoPrimitives)
			{
				AddUndo(p);
			}
		}

		/// <summary>
		/// Cancel marks an Open transaction Canceled, and Undoes and clears any primitives that have been added.
		/// </summary>
		public void Cancel()
		{
			for (var i = _primitives.Count - 1; i >= 0; --i)
			{
				_primitives[i].Undo();
			}

			_primitives.Clear();
			_state = UndoTransactionState.Canceled;
		}

		/// <summary>
		/// AddUndo adds a new primitive to the end of the list when the transaction is Open.
		/// </summary>
		/// <param name="undo"></param>
		public void AddUndo(ITextUndoPrimitive undo)
		{
			_primitives.Add(undo);
			undo.Parent = this;

			MergeMostRecentUndoPrimitive();
		}

		/// <summary>
		/// This is called by AddUndo, so that primitives are always in a fully merged state as we go.
		/// </summary>
		protected void MergeMostRecentUndoPrimitive()
		{
			// no merging unless there are at least two items
			if (_primitives.Count < 2)
			{
				return;
			}

			var top = _primitives[_primitives.Count - 1];

			ITextUndoPrimitive victim = null;
			var victimIndex = -1;

			for (var i = _primitives.Count - 2; i >= 0; --i)
			{
				if (top.GetType() == _primitives[i].GetType() && top.CanMerge(_primitives[i]))
				{
					victim = _primitives[i];
					victimIndex = i;
					break;
				}
			}

			if (victim != null)
			{
				var newPrimitive = top.Merge(victim);
				_primitives.RemoveRange(_primitives.Count - 1, 1);
				_primitives.RemoveRange(victimIndex, 1);
				_primitives.Add(newPrimitive);
			}
		}

		public ITextUndoTransaction Parent => _parent;

		/// <summary>
		/// This is true iff every contained primitive is CanRedo and we are in an Undone state.
		/// </summary>
		public bool CanRedo
		{
			get
			{
				if (_state == UndoTransactionState.Invalid)
				{
					return true;
				}

				if (State != UndoTransactionState.Undone)
				{
					return false;
				}

				return UndoPrimitives.All(primitive => primitive.CanRedo);
			}
		}

		/// <summary>
		/// This is true iff every contained primitive is CanUndo and we are in a Completed state.
		/// </summary>
		public bool CanUndo
		{
			get
			{
				if (_state == UndoTransactionState.Invalid)
				{
					return true;
				}

				if (State != UndoTransactionState.Completed)
				{
					return false;
				}

				return UndoPrimitives.All(primitive => primitive.CanUndo);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void Do()
		{
			if (_state == UndoTransactionState.Invalid)
			{
				return;
			}

			if (!CanRedo)
			{
				throw new InvalidOperationException("Strings.DoCalledButCanRedoFalse");
			}

			_state = UndoTransactionState.Redoing;

			for (var i = 0; i < _primitives.Count; ++i)
			{
				_primitives[i].Do();
			}

			_state = UndoTransactionState.Completed;
		}

		/// <summary>
		/// This defers to the linked transaction if there is one.
		/// </summary>
		public void Undo()
		{
			if (_state == UndoTransactionState.Invalid)
			{
				return;
			}

			if (!CanUndo)
			{
				throw new InvalidOperationException("Strings.UndoCalledButCanUndoFalse");
			}

			_state = UndoTransactionState.Undoing;

			for (var i = _primitives.Count - 1; i >= 0; --i)
			{
				_primitives[i].Undo();
			}

			_state = UndoTransactionState.Undone;
		}

		public IMergeTextUndoTransactionPolicy MergePolicy { get; set; }

		/// <summary>
		/// Closes a transaction and disposes it.
		/// </summary>
		public void Dispose()
		{
			switch (State)
			{
				case UndoTransactionState.Open:
					Cancel();
					break;

				case UndoTransactionState.Canceled:
				case UndoTransactionState.Completed:
					break;

				case UndoTransactionState.Redoing:
				case UndoTransactionState.Undoing:
				case UndoTransactionState.Undone:
					throw new InvalidOperationException("Strings.ClosingAnOpenTransactionThatAppearsToBeUndoneOrUndoing");
			}

			_history.EndTransaction(this);
		}
	}
}