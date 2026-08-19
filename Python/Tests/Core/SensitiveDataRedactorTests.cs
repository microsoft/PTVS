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

using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class SensitiveDataRedactorTests {
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SanitizesSecretKeyValuePairs() {
            var sanitized = SensitiveDataRedactor.Sanitize("password=hunter2 token:abc123 api_key='plain-text' sig=sv-signature");

            Assert.AreEqual("password=<redacted> token:<redacted> api_key='<redacted>' sig=<redacted>", sanitized);
            AssertNoSecrets(sanitized);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SanitizesAuthorizationHeaders() {
            var sanitized = SensitiveDataRedactor.Sanitize("Before\r\nAuthorization: Bearer abc123\r\nAfter");

            Assert.AreEqual("Before\r\nAuthorization: <redacted>\r\nAfter", sanitized);
            AssertNoSecrets(sanitized);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void SanitizesUriCredentials() {
            var sanitized = SensitiveDataRedactor.Sanitize("https://user:password@example.com/path ftp://user@example.org/file");

            Assert.AreEqual("https://<redacted>@example.com/path ftp://<redacted>@example.org/file", sanitized);
            AssertNoSecrets(sanitized);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void PreservesOrdinaryDiagnosticText() {
            const string message = "File C:\\projects\\app.py failed with ValueError: invalid literal";

            Assert.AreEqual(message, SensitiveDataRedactor.Sanitize(message));
        }

        private static void AssertNoSecrets(string sanitized) {
            Assert.IsFalse(sanitized.Contains("hunter2"), sanitized);
            Assert.IsFalse(sanitized.Contains("abc123"), sanitized);
            Assert.IsFalse(sanitized.Contains("plain-text"), sanitized);
            Assert.IsFalse(sanitized.Contains("sv-signature"), sanitized);
            Assert.IsFalse(sanitized.Contains("user:password"), sanitized);
        }
    }
}