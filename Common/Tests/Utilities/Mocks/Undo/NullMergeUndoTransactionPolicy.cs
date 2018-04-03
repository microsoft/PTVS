using System;
using Microsoft.VisualStudio.Text.Operations;

namespace TestUtilities.Mocks {
    internal sealed class NullMergeUndoTransactionPolicy : IMergeTextUndoTransactionPolicy {
        #region Private Fields

        private static NullMergeUndoTransactionPolicy _instance;

        #endregion

        #region Private Constructor

        private NullMergeUndoTransactionPolicy() {}

        #endregion

        /// <summary>
        /// Gets the <see cref="NullMergeUndoTransactionPolicy"/> object.
        /// </summary>
        public static IMergeTextUndoTransactionPolicy Instance => _instance ?? (_instance = new NullMergeUndoTransactionPolicy());

        public bool TestCompatiblePolicy(IMergeTextUndoTransactionPolicy other) {
            return false;
        }

        public bool CanMerge(ITextUndoTransaction newerTransaction, ITextUndoTransaction olderTransaction) {
            return false;
        }

        public void PerformTransactionMerge(ITextUndoTransaction existingTransaction, ITextUndoTransaction newTransaction) {
            throw new InvalidOperationException("Strings.NullMergePolicyCannotMerge");
        }
    }
}