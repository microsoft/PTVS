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

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Tracks our quick info response.  We kick off an async request
    /// to get the info and then attach it to the buffer.  We then
    /// trigger the session and this instance is retrieved.
    /// </summary>
    internal sealed class QuickInfo
    {
        public readonly string Text;
        public readonly ITrackingSpan Span;

        public QuickInfo(string text, ITrackingSpan span)
        {
            Text = text;
            Span = span;
        }
    }
}
