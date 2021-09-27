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
    internal sealed class NullMergeUndoTransactionPolicy : IMergeTextUndoTransactionPolicy
    {
        private static NullMergeUndoTransactionPolicy _instance;

        private NullMergeUndoTransactionPolicy() { }

        /// <summary>
        /// Gets the <see cref="NullMergeUndoTransactionPolicy"/> object.
        /// </summary>
        public static IMergeTextUndoTransactionPolicy Instance => _instance ?? (_instance = new NullMergeUndoTransactionPolicy());

        public bool TestCompatiblePolicy(IMergeTextUndoTransactionPolicy other)
        {
            return false;
        }

        public bool CanMerge(ITextUndoTransaction newerTransaction, ITextUndoTransaction olderTransaction)
        {
            return false;
        }

        public void PerformTransactionMerge(ITextUndoTransaction existingTransaction, ITextUndoTransaction newTransaction)
        {
            throw new InvalidOperationException("Strings.NullMergePolicyCannotMerge");
        }
    }
}