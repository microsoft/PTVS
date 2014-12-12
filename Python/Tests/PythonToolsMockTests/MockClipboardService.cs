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

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudioTools;

namespace PythonToolsMockTests {
    class MockClipboardService : IClipboardService {
        private IDataObject _data;

        public void SetClipboard(IDataObject dataObject) {
            _data = dataObject;
        }

        public IDataObject GetClipboard() {
            return _data;
        }

        public void FlushClipboard() {
            // TODO: We could try and copy the data locally, instead we just keep it alive.
        }

        public bool OpenClipboard() {
            return true;
        }

        public void EmptyClipboard() {
            _data = null;
        }

        public void CloseClipboard() {
        }
    }

}
