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
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;

namespace TestUtilities.Mocks {
    public class MockContentTypeRegistryService : IContentTypeRegistryService {
        #region IContentTypeRegistryService Members

        public IContentType AddContentType(string typeName, IEnumerable<string> baseTypeNames) {
            throw new NotImplementedException();
        }

        public IEnumerable<IContentType> ContentTypes {
            get { throw new NotImplementedException(); }
        }

        public IContentType GetContentType(string typeName) {
            if (typeName == "Python") {
                return new MockContentType("Python", new IContentType[0]);
            }
            throw new NotImplementedException();
        }

        public void RemoveContentType(string typeName) {
            throw new NotImplementedException();
        }

        public IContentType UnknownContentType {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}
