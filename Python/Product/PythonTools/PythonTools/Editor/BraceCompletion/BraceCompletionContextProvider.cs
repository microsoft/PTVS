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

namespace Microsoft.PythonTools.Editor.BraceCompletion
{
    [Export(typeof(IBraceCompletionContextProvider))]
    [BracePair('(', ')')]
    [BracePair('[', ']')]
    [BracePair('{', '}')]
    [BracePair('"', '"')]
    [BracePair('\'', '\'')]
    [ContentType(PythonCoreConstants.ContentType)]
    internal sealed class BraceCompletionContextProvider : IBraceCompletionContextProvider
    {
        [Import]
        internal PythonEditorServices EditorServices = null;

        public bool TryCreateContext(ITextView textView, SnapshotPoint openingPoint, char openingBrace, char closingBrace, out IBraceCompletionContext context)
        {
            if (IsValidBraceCompletionContext(EditorServices.GetBufferInfo(openingPoint.Snapshot.TextBuffer), openingPoint))
            {
                context = new BraceCompletionContext();
                return true;
            }
            else
            {
                context = null;
                return false;
            }
        }

        private static bool IsValidBraceCompletionContext(PythonTextBufferInfo buffer, SnapshotPoint openingPoint)
        {
            if (buffer == null)
            {
                return false;
            }

            Debug.Assert(openingPoint.Position >= 0, "SnapshotPoint.Position should always be zero or positive.");
            if (openingPoint.Position < 0)
            {
                return false;
            }

            // If we have a token here that is in any of these categories, we do
            // not want to complete.
            var category = buffer.GetTokenAtPoint(openingPoint)?.Category ?? TokenCategory.None;
            return !(
                category == TokenCategory.Comment ||
                category == TokenCategory.LineComment ||
                category == TokenCategory.DocComment ||
                category == TokenCategory.StringLiteral ||
                category == TokenCategory.IncompleteMultiLineStringLiteral
            );
        }
    }
}
