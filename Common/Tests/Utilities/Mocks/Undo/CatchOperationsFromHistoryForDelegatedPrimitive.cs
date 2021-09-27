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
    /// <summary>
    /// This class is to make it easy to catch new undo/redo operations while a delegated primitive
    /// is in progress--it is called from DelegatedUndoPrimitive.Undo and .Redo with the IDispose
    /// using pattern to set up the history to send operations our way.
    /// </summary>
    internal sealed class CatchOperationsFromHistoryForDelegatedPrimitive : IDisposable
    {
        private readonly MockTextUndoHistory _history;
        private readonly MockTextUndoPrimitive _primitive;

        public CatchOperationsFromHistoryForDelegatedPrimitive(MockTextUndoHistory history, MockTextUndoPrimitive primitive, DelegatedUndoPrimitiveState state)
        {
            _history = history;
            _primitive = primitive;

            primitive.State = state;
            history.ForwardToUndoOperation(primitive);
        }

        public void Dispose()
        {
            _history.EndForwardToUndoOperation(_primitive);
            _primitive.State = DelegatedUndoPrimitiveState.Inactive;
        }
    }
}
