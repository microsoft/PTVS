// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class PythonVersionRecognition314Tests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ToLanguageVersion_Maps_3_14() {
            var lv = new Version(3, 14).ToLanguageVersion();
            Assert.AreEqual(PythonLanguageVersion.V314, lv, "Version(3,14) should map to PythonLanguageVersion.V314");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LatestVersion_Prefers_314_WhenAvailable() {
            var latest = PythonPaths.LatestVersion;
            if (latest == null) {
                Assert.Inconclusive("No interpreters detected - cannot validate LatestVersion for 3.14");
            }

            // If 3.14 is installed ensure LatestVersion reports it (x86 or x64)
            var has314 = TestUtilities.PythonPaths.Python314 != null || TestUtilities.PythonPaths.Python314_x64 != null;
            if (!has314) {
                Assert.Inconclusive("Python 3.14 not installed on test agent.");
            }

            Assert.AreEqual(PythonLanguageVersion.V314, latest.Version, "LatestVersion did not return 3.14 when it is installed");
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void LanguageVersion_ToVersion_RoundTrip_314() {
            var ver = PythonLanguageVersion.V314.ToVersion();
            Assert.AreEqual(3, ver.Major);
            Assert.AreEqual(14, ver.Minor);
            var roundTrip = ver.ToLanguageVersion();
            Assert.AreEqual(PythonLanguageVersion.V314, roundTrip);
        }
    }
}
