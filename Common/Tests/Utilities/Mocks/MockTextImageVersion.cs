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

using Microsoft.VisualStudio.Text;
using System;

namespace TestUtilities.Mocks
{
    public class MockTextImageVersion : ITextImageVersion
    {
        private readonly ITextVersion _source;

        public MockTextImageVersion(ITextVersion source)
        {
            _source = source;
        }

        public ITextImageVersion Next => (_source.Next != null) ? (ITextImageVersion)(new MockTextImageVersion(_source.Next)) : null;

        public int Length => _source.Length;

        public INormalizedTextChangeCollection Changes => _source.Changes;

        public int VersionNumber => _source.VersionNumber;

        public object Identifier => _source.TextBuffer;

        public int TrackTo(VersionedPosition other, PointTrackingMode mode)
        {
            throw new NotSupportedException();
        }

        public Span TrackTo(VersionedSpan span, SpanTrackingMode mode)
        {
            throw new NotSupportedException();
        }
    }
}
