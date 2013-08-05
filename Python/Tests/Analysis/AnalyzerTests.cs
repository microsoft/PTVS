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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class AnalyzerTests {
        [TestMethod]
        public void LogFileEncoding() {
            // Ensure that log messages round-trip correctly.

            const string TEST = "Abc \u01FA\u0299\uFB3B";
            var log1 = Path.GetTempFileName();
            var log2 = Path.GetTempFileName();

            try {
                using (var analyzer = new PyLibAnalyzer(
                    Guid.Empty,
                    new Version(),
                    null,
                    null,
                    null,
                    null,
                    log1,
                    log2,
                    null,
                    false,
                    false)) {

                    analyzer.StartTraceListener();
                    analyzer.TraceError(TEST);
                    analyzer.TraceWarning(TEST);
                    analyzer.TraceInformation(TEST);
                    analyzer.TraceVerbose(TEST);

                    analyzer.LogToGlobal(TEST);
                }

                var content1 = File.ReadLines(log1, Encoding.UTF8)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Skip(1)    // Skip the header
                    .Select(line => line.Trim())
                    .ToArray();
                Console.WriteLine(string.Join(Environment.NewLine, content1));
                Console.WriteLine();
                Assert.IsTrue(Regex.IsMatch(content1[0], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[ERROR\] " + TEST + "$"), content1[0]);
                Assert.IsTrue(Regex.IsMatch(content1[1], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[WARNING\] " + TEST + "$"), content1[1]);
                Assert.IsTrue(Regex.IsMatch(content1[2], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: " + TEST + "$"), content1[2]);
#if DEBUG
                Assert.IsTrue(Regex.IsMatch(content1[3], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[VERBOSE\] " + TEST + "$"), content1[3]);
#endif

                var content2 = File.ReadAllText(log2, Encoding.UTF8);
                Console.WriteLine(content2);
                Assert.IsTrue(Regex.IsMatch(content2, @"\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d " + TEST + " .+$"), content2);
            } finally {
                File.Delete(log1);
                File.Delete(log2);
            }
        }
    }
}
