using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.TestAdapter.UnitTest {
    public static class UnitTestExtensions {

        public static TestCase ToVsTestCase(this UnitTestTestCase test, bool isWorkspace, string projectHome) {
            var normalizedPath = test.Source.ToLowerInvariant();
            var moduleName = PathUtils.CreateFriendlyFilePath(projectHome, normalizedPath).ToLowerInvariant();
            
            var fullyQualifiedName = MakeFullyQualifiedTestName(moduleName, test.Id);
            var testCase = new TestCase(fullyQualifiedName, PythonConstants.UnitTestExecutorUri, normalizedPath) {
                DisplayName = test.Name,
                LineNumber = test.Line,
                CodeFilePath = normalizedPath
            };

            return testCase;
        }

        // Note any changes here need to agree with TestReader.ParseFullyQualifiedTestName
        private static string MakeFullyQualifiedTestName(string modulePath, string unitTestId) {
            // ie. test3.Test_test3.test_A
            var idParts = unitTestId.Split('.');
            Debug.Assert(idParts.Length == 3);

            // drop module name and append to filepath
            var parts = new[] { modulePath, idParts[1], idParts[2]};
            return String.Join("::", parts);
        }

    }
}
