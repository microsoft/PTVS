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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.Scripting.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsMockTests {
    [TestClass]
    public class SquiggleTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);
        public static ScriptEngine PythonEngine = Python.CreateEngine();

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestInitialize]
        public void TestInitialize() {
            UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = true;
        }

        [TestCleanup]
        public void TestCleanup() {
            UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = false;
        }

        private static string FormatErrorTag(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}: {1} ({2})",
                tag.Tag.ErrorType,
                tag.Tag.ToolTipContent,
                FormatErrorTagSpan(tag)
            );
        }

        private static string FormatErrorTagSpan(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}-{1}",
                tag.Span.GetStartPoint(tag.Span.TextBuffer.CurrentSnapshot).Position,
                tag.Span.GetEndPoint(tag.Span.TextBuffer.CurrentSnapshot).Position
            );
        }


        [TestMethod, Priority(1)]
        public void UnresolvedImportSquiggle() {
            List<string> squiggles;

            using (var view = new PythonEditor("import fob, oar\r\nfrom baz import *\r\nfrom .spam import eggs")) {
                var errorProvider = view.VS.ServiceProvider.GetComponentModel().GetService<IErrorProviderFactory>();
                var tagger = errorProvider.GetErrorTagger(view.View.TextView.TextBuffer);
                // Ensure all tasks have been updated
                var taskProvider = (ErrorTaskProvider)view.VS.ServiceProvider.GetService(typeof(ErrorTaskProvider));
                var time = taskProvider.FlushAsync().GetAwaiter().GetResult();
                Console.WriteLine("TaskProvider.FlushAsync took {0}ms", time.TotalMilliseconds);

                squiggles = tagger.GetTaggedSpans(new SnapshotSpan(view.CurrentSnapshot, 0, view.CurrentSnapshot.Length))
                    .Select(FormatErrorTag)
                    .ToList();
            }

            Console.WriteLine(" Squiggles found:");
            foreach (var actual in squiggles) {
                Console.WriteLine(actual);
            }
            Console.WriteLine(" Found {0} squiggle(s)", squiggles.Count);

            int i = 0;
            foreach (var expected in new[] {
                // Ensure that the warning includes the module name
                @".*warning:.*fob.*\(7-10\)",
                @".*warning:.*oar.*\(12-15\)",
                @".*warning:.*baz.*\(22-25\)",
                @".*warning:.*\.spam.*\(41-46\)"
            }) {
                Assert.IsTrue(i < squiggles.Count, "Not enough squiggles");
                AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.Singleline), squiggles[i]);
                i += 1;
            }
        }

        [TestMethod, Priority(1)]
        public void HandledImportSquiggle() {
            var testCases = new List<Tuple<string, string[]>>();
            testCases.AddRange(
                new[] { "", " BaseException", " Exception", " ImportError", " (ValueError, ImportError)" }
                .Select(ex => Tuple.Create(
                    string.Format("try:\r\n    import spam\r\nexcept{0}:\r\n    pass\r\n", ex),
                    new string[0]
                ))
            );

            testCases.Add(Tuple.Create(
                "try:\r\n    import spam\r\nexcept ValueError:\r\n    pass\r\n",
                new[] { @".*warning:.*spam.*\(17-21\)" }
            ));

            using (var view = new PythonEditor()) {
                var errorProvider = view.VS.ServiceProvider.GetComponentModel().GetService<IErrorProviderFactory>();
                var tagger = errorProvider.GetErrorTagger(view.View.TextView.TextBuffer);
                // Ensure all tasks have been updated
                var taskProvider = (ErrorTaskProvider)view.VS.ServiceProvider.GetService(typeof(ErrorTaskProvider));
                Assert.IsNotNull(taskProvider, "no ErrorTaskProvider available");

                foreach (var testCase in testCases) {
                    view.Text = testCase.Item1;
                    var time = taskProvider.FlushAsync().GetAwaiter().GetResult();
                    Console.WriteLine("TaskProvider.FlushAsync took {0}ms", time.TotalMilliseconds);

                    var squiggles = tagger.GetTaggedSpans(new SnapshotSpan(view.CurrentSnapshot, 0, view.CurrentSnapshot.Length))
                        .Select(FormatErrorTag)
                        .ToList();

                    Console.WriteLine(testCase.Item1);
                    Console.WriteLine(" Squiggles found:");
                    foreach (var actual in squiggles) {
                        Console.WriteLine(actual);
                    }
                    Console.WriteLine(" Found {0} squiggle(s)", squiggles.Count);
                    Console.WriteLine();

                    int i = 0;
                    foreach (var expected in testCase.Item2) {
                        Assert.IsTrue(i < squiggles.Count, "Not enough squiggles");
                        AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.Singleline), squiggles[i]);
                        i += 1;
                    }
                }
            }
        }
    }
}
