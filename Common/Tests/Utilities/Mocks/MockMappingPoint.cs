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

using System;
using Microsoft.VisualStudio.Text;

namespace TestUtilities.Mocks {
    public class MockMappingPoint : IMappingPoint {
        public MockMappingPoint() {
        }

        public ITextBuffer AnchorBuffer {
            get { throw new NotImplementedException(); }
        }

        public Microsoft.VisualStudio.Text.Projection.IBufferGraph BufferGraph {
            get { throw new NotImplementedException(); }
        }

        public SnapshotPoint? GetInsertionPoint(Predicate<ITextBuffer> match) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? GetPoint(Predicate<ITextBuffer> match, PositionAffinity affinity) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? GetPoint(ITextSnapshot targetSnapshot, PositionAffinity affinity) {
            throw new NotImplementedException();
        }

        public SnapshotPoint? GetPoint(ITextBuffer targetBuffer, PositionAffinity affinity) {
            throw new NotImplementedException();
        }
    }
}
