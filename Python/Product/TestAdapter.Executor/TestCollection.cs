using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.TestAdapter {
    class TestCollection : ITestCaseDiscoverySink {

        public List<TestCase> Tests;

        public void SendTestCase(TestCase discoveredTest) {
            Tests.Add(discoveredTest);
        }
    }
}
