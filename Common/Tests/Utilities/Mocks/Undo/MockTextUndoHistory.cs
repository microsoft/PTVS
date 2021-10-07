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
	internal class MockTextUndoHistory : ITextUndoHistory
	{
		public event EventHandler<TextUndoRedoEventArgs> UndoRedoHappened;

		public event EventHandler<TextUndoTransactionCompletedEventArgs> UndoTransactionCompleted;

		private MockTextUndoTransaction _currentTransaction;
		private readonly Stack<ITextUndoTransaction> _undoStack;
		private readonly Stack<ITextUndoTransaction> _redoStack;
		private MockTextUndoPrimitive _activeUndoOperationPrimitive;
		private TextUndoHistoryState _state;
		private PropertyCollection _properties;

		internal MockTextUndoHistoryRegistry UndoHistoryRegistry { get; set; }

		public MockTextUndoHistory(MockTextUndoHistoryRegistry undoHistoryRegistry)
		{
			_currentTransaction = null;
			UndoHistoryRegistry = undoHistoryRegistry;
			_undoStack = new Stack<ITextUndoTransaction>();
			_redoStack = new Stack<ITextUndoTransaction>();
			_activeUndoOperationPrimitive = null;
			_state = TextUndoHistoryState.Idle;
		}

		/// <summary>
		/// The full undo stack for this history. Does not include any currently opened or redo transactions.
		/// </summary>
		public IEnumerable<ITextUndoTransaction> UndoStack => _undoStack;

		/// <summary>
		/// The full redo stack for this history. Does not include any currently opened or undo transactions.
		/// </summary>
		public IEnumerable<ITextUndoTransaction> RedoStack => _redoStack;

		/// <summary>
		/// It returns most recently pushed (topmost) item of the <see cref="ITextUndoHistory.UndoStack"/> or if the stack is
		/// empty it returns null.
		/// </summary>
		public ITextUndoTransaction LastUndoTransaction
		{
			get
			{
				if (_undoStack.Count != 0)
				{
					return _undoStack.Peek();
				}

				return null;
			}
		}

		/// <summary>
		/// It returns most recently pushed (topmost) item of the <see cref="ITextUndoHistory.RedoStack"/> or if the stack is
		/// empty it returns null.
		/// </summary>
		public ITextUndoTransaction LastRedoTransaction
		{
			get
			{
				if (_redoStack.Count != 0)
				{
					return _redoStack.Peek();
				}

				return null;
			}
		}

		/// <summary>
		/// Whether a single undo is permissible (corresponds to the most recent visible undo UndoTransaction's CanUndo).        
		/// </summary>
		/// <remarks>
		/// If there are hidden transactions on top of the visible transaction, this property returns true only they are 
		/// undoable as well.
		/// </remarks>
		public bool CanUndo => _undoStack.Count > 0 && _undoStack.Peek().CanUndo;

		/// <summary>
		/// Whether a single redo is permissible (corresponds to the most recent visible redo UndoTransaction's CanRedo).
		/// </summary>
		/// <remarks>
		/// If there are hidden transactions on top of the visible transaction, this property returns true only they are 
		/// redoable as well.
		/// </remarks>
		public bool CanRedo => _redoStack.Count > 0 && _redoStack.Peek().CanRedo;

		/// <summary>
		/// The most recent visible undo UndoTransactions's Description.
		/// </summary>
		public string UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : "Strings.HistoryCantUndo";

		/// <summary>
		/// The most recent visible redo UndoTransaction's Description.
		/// </summary>
		public string RedoDescription => _undoStack.Count > 0 ? _redoStack.Peek().Description : "Strings.HistoryCantRedo";

		/// <summary>
		/// The current UndoTransaction in progress.
		/// </summary>
		public ITextUndoTransaction CurrentTransaction => _currentTransaction;

		/// <summary>
		/// 
		/// </summary>
		public TextUndoHistoryState State => _state;

		public ITextUndoTransaction CreateInvisibleTransaction(string description)
		{
			return CreateTransaction(description);
		}

		/// <summary>
		/// Creates a new transaction, nests it in the previously current transaction, and marks it current.
		/// If there is a redo stack, it gets cleared.
		/// UNDONE: should the redo-clearing happen now or when the new transaction is committed?
		/// </summary>
		/// <param name="description">A string description for the transaction.</param>
		/// <returns></returns>
		public ITextUndoTransaction CreateTransaction(string description)
		{
			description = description ?? string.Empty;

			// If there is a pending transaction that has already been completed, we should not be permitted
			// to open a new transaction, since it cannot later be added to its parent.
			if ((_currentTransaction != null) && (_currentTransaction.State != UndoTransactionState.Open))
			{
				throw new InvalidOperationException("Strings.CannotCreateTransactionWhenCurrentTransactionNotOpen");
			}

			// new transactions that are visible should clear the redo stack.
			if (_currentTransaction == null)
			{
				foreach (var textUndoTransaction in _redoStack)
				{
					MockTextUndoTransaction redoTransaction = (MockTextUndoTransaction)textUndoTransaction;
					redoTransaction.Invalidate();
				}

				_redoStack.Clear();
			}

			MockTextUndoTransaction newTransaction = new MockTextUndoTransaction(this, _currentTransaction, description);

			_currentTransaction = newTransaction;

			return _currentTransaction;
		}

		/// <summary>
		/// Performs requested amount of undo operation and places the transactions on the redo stack.
		/// UNDONE: What if there is a currently opened transaction?
		/// </summary>
		/// <param name="count">The number of undo operations to perform. At the end of the operation, requested number of visible
		/// transactions are undone. Hence actual number of transactions undone might be more than this number if there are some 
		/// hidden transactions adjacent to (on top of or at the bottom of) the visible ones.
		/// </param>        
		/// <remarks>
		/// After the last visible transaction is undone, hidden transactions left on top the stack are undone as well until a 
		/// visible or linked transaction is encountered or stack is emptied totally.
		/// </remarks>
		public void Undo(int count)
		{
			if (count <= 0)
			{
				throw new ArgumentException("count must be > 0", nameof(count));
			}

			if (!IsThereEnoughVisibleTransactions(_undoStack, count))
			{
				throw new InvalidOperationException("Not enough undos/redos on the stack");
			}

			TextUndoHistoryState originalState = _state;
			_state = TextUndoHistoryState.Undoing;
			using (new AutoEnclose(delegate { _state = originalState; }))
			{
				while (count > 0)
				{
					if (!_undoStack.Peek().CanUndo)
					{
						throw new InvalidOperationException("Undo cannot request primitives from stack");
					}

					ITextUndoTransaction ut = _undoStack.Pop();
					ut.Undo();
					_redoStack.Push(ut);

					RaiseUndoRedoHappened(_state, ut);

					--count;
				}
			}
		}

		/// <summary>
		/// Performs an undo operation and places the primitives on the redo stack, up until (and 
		/// including) the transaction indicated. This is called by the linked undo transaction that
		/// is aware of the linking relationship between transactions, and it does not call back into
		/// the transactions' public Undo().
		/// </summary>
		/// <param name="transaction"></param>
		public void UndoInIsolation(MockTextUndoTransaction transaction)
		{
			TextUndoHistoryState originalState = _state;
			_state = TextUndoHistoryState.Undoing;
			using (new AutoEnclose(delegate { _state = originalState; }))
			{
				if (_undoStack.Contains(transaction))
				{
					MockTextUndoTransaction undone = null;
					while (undone != transaction)
					{
						MockTextUndoTransaction ut = _undoStack.Pop() as MockTextUndoTransaction;
						ut.Undo();
						_redoStack.Push(ut);

						RaiseUndoRedoHappened(_state, ut);

						undone = ut;
					}
				}
			}
		}

		/// <summary>
		/// Performs requested amount of redo operation and places the transactions on the undo stack.
		/// UNDONE: What if there is a currently opened transaction?
		/// </summary>
		/// <param name="count">The number of redo operations to perform. At the end of the operation, requested number of visible
		/// transactions are redone. Hence actual number of transactions redone might be more than this number if there are some 
		/// hidden transactions adjacent to (on top of or at the bottom of) the visible ones.
		/// </param>        
		/// <remarks>
		/// After the last visible transaction is redone, hidden transactions left on top the stack are redone as well until a 
		/// visible or linked transaction is encountered or stack is emptied totally.
		/// </remarks>
		public void Redo(int count)
		{
			if (count <= 0)
			{
				throw new ArgumentException("count must be > 0", nameof(count));
			}

			if (!IsThereEnoughVisibleTransactions(_redoStack, count))
			{
				throw new InvalidOperationException("No more undos/redos on the stack");
			}

			TextUndoHistoryState originalState = _state;
			_state = TextUndoHistoryState.Redoing;
			using (new AutoEnclose(delegate { _state = originalState; }))
			{
				while (count > 0)
				{
					if (!_redoStack.Peek().CanRedo)
					{
						throw new InvalidOperationException("Cannot redo/ask primitive from stack");
					}

					ITextUndoTransaction ut = _redoStack.Pop();
					ut.Do();
					_undoStack.Push(ut);

					RaiseUndoRedoHappened(_state, ut);

					--count;
				}
			}
		}

		/// <summary>
		/// Performs a redo operation and places the primitives on the redo stack, up until (and 
		/// including) the transaction indicated. This is called by the linked undo transaction that
		/// is aware of the linking relationship between transactions, and it does not call back into
		/// the transactions' public Redo().
		/// </summary>
		/// <param name="transaction"></param>
		public void RedoInIsolation(MockTextUndoTransaction transaction)
		{
			TextUndoHistoryState originalState = _state;
			_state = TextUndoHistoryState.Redoing;
			using (new AutoEnclose(delegate { _state = originalState; }))
			{
				if (_redoStack.Contains(transaction))
				{
					MockTextUndoTransaction redone = null;
					while (redone != transaction)
					{
						MockTextUndoTransaction ut = _redoStack.Pop() as MockTextUndoTransaction;
						ut.Do();
						_undoStack.Push(ut);

						RaiseUndoRedoHappened(_state, ut);

						redone = ut;
					}
				}
			}
		}

		/// <summary>
		/// This method is called from the DelegatedUndoPrimitive just as it starts a do or undo, so that this
		/// history knows to forward any new UndoableOperations to the primitive. This and its pair EndForward... only manage
		/// the state of the activeUndoOperationPrimitive.
		/// </summary>
		/// <param name="primitive">The delegated primitive to be marked active</param>
		public void ForwardToUndoOperation(MockTextUndoPrimitive primitive)
		{
			if (_activeUndoOperationPrimitive != null)
			{
				throw new InvalidOperationException();
			}

			_activeUndoOperationPrimitive = primitive;
		}

		/// <summary>
		/// This method ends the lifetime of the activeUndoOperationPrimitive and should be called after ForwardToUndoOperation.
		/// </summary>
		/// <param name="primitive">The previously active delegated primitive--used for sanity check.</param>
		public void EndForwardToUndoOperation(MockTextUndoPrimitive primitive)
		{
			if (_activeUndoOperationPrimitive != primitive)
			{
				throw new InvalidOperationException();
			}

			_activeUndoOperationPrimitive = null;
		}

		/// <summary>
		/// This is how the transactions alert their containing history that they have finished
		/// (likely from the Dispose() method). 
		/// </summary>
		/// <param name="transaction">This is the transaction that's finishing. It should match the history's current transaction.
		/// If it does not match, then the current transaction will be discarded and an exception will be thrown.</param>
		public void EndTransaction(ITextUndoTransaction transaction)
		{
			if (_currentTransaction != transaction)
			{
				_currentTransaction = null;
				throw new InvalidOperationException("Strings.EndTransactionOutOfOrder");
			}

			// only add completed transactions to their parents (or the stack)
			if (_currentTransaction.State == UndoTransactionState.Completed)
			{
				if (_currentTransaction.Parent == null) // stack bottomed out!
				{
					MergeOrPushToUndoStack(_currentTransaction);
				}
			}

			_currentTransaction = _currentTransaction.Parent as MockTextUndoTransaction;
		}

		/// <summary>
		/// This does two different things, depending on the MergeUndoTransactionPolicys in question.
		/// It either simply pushes the current transaction to the undo stack, OR it merges it with
		/// the most recent item in the stack.
		/// </summary>
		private void MergeOrPushToUndoStack(MockTextUndoTransaction transaction)
		{
			ITextUndoTransaction transactionAdded;
			TextUndoTransactionCompletionResult transactionResult;

			MockTextUndoTransaction utPrevious = _undoStack.Count > 0 ? _undoStack.Peek() as MockTextUndoTransaction : null;
			if (utPrevious != null && ProceedWithMerge(transaction, utPrevious))
			{
				// Temporarily make utPrevious non-read-only, during merge.
				utPrevious.IsReadOnly = false;
				try
				{
					transaction.MergePolicy.PerformTransactionMerge(utPrevious, transaction);
				}
				finally
				{
					utPrevious.IsReadOnly = true;
				}

				// utPrevious is already on the undo stack, so we don't need to add it; but report
				// it as the added transaction in the UndoTransactionCompleted event.
				transactionAdded = utPrevious;
				transactionResult = TextUndoTransactionCompletionResult.TransactionMerged;
			}
			else
			{
				_undoStack.Push(transaction);

				transactionAdded = transaction;
				transactionResult = TextUndoTransactionCompletionResult.TransactionAdded;
			}

			RaiseUndoTransactionCompleted(transactionAdded, transactionResult);
		}

		public bool ValidTransactionForMarkers(ITextUndoTransaction transaction)
		{
			return transaction == null                     //// you can put a marker on the null transaction
				   || _currentTransaction == transaction  //// you can put a marker on the currently active transaction
				   || (transaction.History == this && transaction.State != UndoTransactionState.Invalid);
			//// and you can put a marker on any transaction in this history.
		}

		public static bool IsThereEnoughVisibleTransactions(Stack<ITextUndoTransaction> stack, int visibleCount)
		{
			if (visibleCount <= 0)
			{
				return true;
			}

			foreach (ITextUndoTransaction transaction in stack)
			{
				visibleCount--;

				if (visibleCount <= 0)
				{
					return true;
				}
			}

			return false;
		}

		private bool ProceedWithMerge(MockTextUndoTransaction transaction1, MockTextUndoTransaction transaction2)
		{
			MockTextUndoHistoryRegistry registry = UndoHistoryRegistry;

			return transaction1.MergePolicy != null
				   && transaction2.MergePolicy != null
				   && transaction1.MergePolicy.TestCompatiblePolicy(transaction2.MergePolicy)
				   && transaction1.MergePolicy.CanMerge(transaction1, transaction2);
		}

		private void RaiseUndoRedoHappened(TextUndoHistoryState state, ITextUndoTransaction transaction)
		{
			EventHandler<TextUndoRedoEventArgs> undoRedoHappened = UndoRedoHappened;
			undoRedoHappened?.Invoke(this, new TextUndoRedoEventArgs(state, transaction));
		}

		private void RaiseUndoTransactionCompleted(ITextUndoTransaction transaction, TextUndoTransactionCompletionResult result)
		{
			EventHandler<TextUndoTransactionCompletedEventArgs> undoTransactionAdded = UndoTransactionCompleted;
			undoTransactionAdded?.Invoke(this, new TextUndoTransactionCompletedEventArgs(transaction, result));
		}

		public PropertyCollection Properties => _properties ?? (_properties = new PropertyCollection());
	}
}