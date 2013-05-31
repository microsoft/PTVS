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
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace TestAdapterTests {
    class MockRunContext : IRunContext {
        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider) {
            throw new NotImplementedException();
        }

        public bool InIsolation {
            get { throw new NotImplementedException(); }
        }

        public bool IsBeingDebugged {
            get { return false; }
        }

        public bool IsDataCollectionEnabled {
            get { throw new NotImplementedException(); }
        }

        public bool KeepAlive {
            get { throw new NotImplementedException(); }
        }

        public string SolutionDirectory {
            get { throw new NotImplementedException(); }
        }

        public string TestRunDirectory {
            get { throw new NotImplementedException(); }
        }

        public IRunSettings RunSettings {
            get { throw new NotImplementedException(); }
        }
    }
}
