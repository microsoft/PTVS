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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;
using PriorityAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.PriorityAttribute;

namespace PythonToolsMockTests {
    [TestClass]
    public class SquiggleTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private static string FormatErrorTag(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}: {1} ({2}:{3})",
                tag.Tag.ErrorType,
                tag.Tag.ToolTipContent,
                (tag.Tag as ErrorTagWithMoniker)?.Moniker ?? "no moniker",
                FormatErrorTagSpan(tag)
            );
        }

        private static string FormatErrorTagSpan(TrackingTagSpan<ErrorTag> tag) {
            return string.Format("{0}-{1}",
                tag.Span.GetStartPoint(tag.Span.TextBuffer.CurrentSnapshot).Position,
                tag.Span.GetEndPoint(tag.Span.TextBuffer.CurrentSnapshot).Position
            );
        }


        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public async Task UnresolvedImportSquiggle() {
            List<string> squiggles;

            using (var view = new PythonEditor("import fob, oar\r\nfrom baz import *\r\nfrom spam import eggs")) {
                var errorProvider = view.VS.ServiceProvider.GetComponentModel().GetService<IErrorProviderFactory>();
                var tagger = errorProvider.GetErrorTagger(view.View.TextView.TextBuffer);
                // Ensure all tasks have been updated
                var taskProvider = (ErrorTaskProvider)view.VS.ServiceProvider.GetService(typeof(ErrorTaskProvider));
                var time = await taskProvider.FlushAsync();
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
                @".*warning:.*fob.*\(Python.+:7-10\)",
                @".*warning:.*oar.*\(Python.+:12-15\)",
                @".*warning:.*baz.*\(Python.+:22-25\)",
                @".*warning:.*spam.*\(Python.+:41-45\)"
            }) {
                Assert.IsTrue(i < squiggles.Count, "Not enough squiggles");
                AssertUtil.AreEqual(new Regex(expected, RegexOptions.IgnoreCase | RegexOptions.Singleline), squiggles[i]);
                i += 1;
            }
        }

        [TestMethod, Priority(UnitTestPriority.P2_FAILING)]
        public async Task HandledImportSquiggle() {
            var testCases = new List<Tuple<string, string[]>>();
            testCases.Add(Tuple.Create(
                "try:\r\n    import spam\r\nexcept ValueError:\r\n    pass\r\n",
                new[] { @".*warning:.*spam.*\(Python.+:17-21\)" }
            ));

            testCases.AddRange(
                new[] { "", " BaseException", " Exception", " ImportError", " (ValueError, ImportError)" }
                .Select(ex => Tuple.Create(
                    string.Format("try:\r\n    import spam\r\nexcept{0}:\r\n    pass\r\n", ex),
                    new string[0]
                ))
            );

            using (var view = new PythonEditor()) {
                var errorProvider = view.VS.ServiceProvider.GetComponentModel().GetService<IErrorProviderFactory>();
                var tagger = errorProvider.GetErrorTagger(view.View.TextView.TextBuffer);
                // Ensure all tasks have been updated
                var taskProvider = (ErrorTaskProvider)view.VS.ServiceProvider.GetService(typeof(ErrorTaskProvider));
                Assert.IsNotNull(taskProvider, "no ErrorTaskProvider available");

                foreach (var testCase in testCases) {
                    view.Text = testCase.Item1;
                    var time = await taskProvider.FlushAsync();
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
