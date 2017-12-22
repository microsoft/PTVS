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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using static Microsoft.PythonTools.Intellisense.ProjectEntryExtensions;

namespace AnalysisTests {
    [TestClass]
    public class CodeBufferTests {
        [TestMethod, Priority(0)]
        public void CodeBuffer() {
            var cc = new CurrentCode {
                Version = 0
            };
            cc.Text.Append(@"def f(x):
    return

def g(y):
    return y * 2
");

            cc.UpdateCode(new[] { new[] {
                // We *should* batch adjacent insertions, but we should also
                // work fine even if we don't. Note that the insertion point
                // tracks forward with previous insertions in the same version.
                // If each of these were in its own array, the location would
                // have to change for each.
                ChangeInfo.Insert(" ", new SourceLocation(2, 11)),
                ChangeInfo.Insert("g", new SourceLocation(2, 11)),
                ChangeInfo.Insert("(", new SourceLocation(2, 11)),
                ChangeInfo.Insert("x", new SourceLocation(2, 11)),
                ChangeInfo.Insert(")", new SourceLocation(2, 11))
            } }, finalVersion: 1);

            AssertUtil.Contains(cc.Text.ToString(), "return g(x)");
            Assert.AreEqual(1, cc.Version);

            cc.UpdateCode(new[] {
                new [] {
                    ChangeInfo.Delete(new SourceLocation(2, 14), new SourceLocation(2, 15))
                },
                new [] {
                    ChangeInfo.Insert("x * 2", new SourceLocation(2, 14))
                }
            }, finalVersion: 3);

            AssertUtil.Contains(cc.Text.ToString(), "return g(x * 2)");

            cc.UpdateCode(new[] { new[] {
                ChangeInfo.Replace(new SourceLocation(2, 18), new SourceLocation(2, 19), "300")
            } }, finalVersion: 4);

            AssertUtil.Contains(cc.Text.ToString(), "return g(x * 300)");

            try {
                cc.UpdateCode(new[] { new[] {
                    ChangeInfo.Delete(new SourceLocation(2, 13), new SourceLocation(2, 22)),
                    ChangeInfo.Insert("#", new SourceLocation(2, 7))
                } }, finalVersion: 5);
                Assert.Fail("Expected exception");
            } catch (Exception) {
            }
            AssertUtil.Contains(cc.Text.ToString(), "return g");
        }
    }
}
