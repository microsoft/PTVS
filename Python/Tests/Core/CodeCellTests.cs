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

namespace PythonToolsTests
{
	using CCA = CodeCellAnalysis;
	using PriorityAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.PriorityAttribute;

	[TestClass]
	public class CodeCellTests
	{
		public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);

		private static string ShowWhitespace(string s)
		{
			return s.Replace(' ', '\u00B7').Replace('\t', '\u2409');
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void CodeCellMarkers()
		{
			foreach (var trueMarker in new[] {
				"#%%",
				"#%% Comment",
				"# In[1]:",
				"# In[abcdefg]:comment after",
				"#In[1043540398340162312]:"
			})
			{
				foreach (var ws in new[] { "", " ", "\t", "  \t  " })
				{
					Assert.IsTrue(CCA.IsCellMarker(ws + trueMarker), ShowWhitespace(ws + trueMarker));
					Assert.IsTrue(CCA.IsCellMarker(trueMarker + ws), ShowWhitespace(trueMarker + ws));
					Assert.IsTrue(CCA.IsCellMarker(ws + trueMarker + ws), ShowWhitespace(ws + trueMarker + ws));
				}
			}

			foreach (var falseMarker in new[] {
				"",
				"#",
				"'''#%%'''",
				"# In[abcdefg]",
				"# In[abcdefg] :"
			})
			{
				Assert.IsFalse(CCA.IsCellMarker(falseMarker), ShowWhitespace(falseMarker));
			}
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void EmptyCodeCell()
		{
			var buffer = new MockTextBuffer(@"# comment here

# In[ ]:

#%% next cell
", PythonContentType);

			AssertCellStart(buffer, 0, 0);
			AssertCellStart(buffer, 1, 0);
			AssertCellStart(buffer, 2, 0);
			AssertCellStart(buffer, 3, 0);
			AssertCellStart(buffer, 4, 4);
			AssertCellStart(buffer, 5, 4);

			AssertCellEnd(buffer, 0, 2);
			AssertCellEnd(buffer, 1, 2);
			AssertCellEnd(buffer, 2, 2);
			AssertCellEnd(buffer, 3, 2);
			AssertCellEnd(buffer, 4, 4);
			AssertCellEnd(buffer, 5, 4);

		}

		private static void AssertCellStart(ITextBuffer buffer, int startFromLine, int expectedLine)
		{
			var found = CCA.FindStartOfCell(buffer.CurrentSnapshot.GetLineFromLineNumber(startFromLine));
			if (expectedLine < 0)
			{
				Assert.IsNull(found, "Actually found: " + found?.GetText() ?? "(null)");
			}
			else
			{
				Assert.IsNotNull(found, "Actually found: (null)\r\n\r\nExpected: " + buffer.CurrentSnapshot.GetLineFromLineNumber(expectedLine).GetText());
				Assert.AreEqual(expectedLine, found.LineNumber, "Actually found: " + found.GetText());
			}
		}

		private static void AssertCellEnd(ITextBuffer buffer, int startFromLine, int expectedLine, bool withWhitespace = false)
		{
			var cellStart = CCA.FindStartOfCell(buffer.CurrentSnapshot.GetLineFromLineNumber(startFromLine));
			var found = CCA.FindEndOfCell(cellStart, buffer.CurrentSnapshot.GetLineFromLineNumber(startFromLine), withWhitespace);
			if (expectedLine < 0)
			{
				Assert.IsNull(found, "Actually found: " + found?.GetText() ?? "(null)");
			}
			else
			{
				Assert.IsNotNull(found, "Actually found: (null)\r\n\r\nExpected: " + buffer.CurrentSnapshot.GetLineFromLineNumber(expectedLine).GetText());
				Assert.AreEqual(expectedLine, found.LineNumber, "Actually found: " + found.GetText());
			}
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void FindStartOfCodeCell()
		{
			var code = new MockTextBuffer(@"x
# ...
x
#%% cell here
x

#%% cell here
x
");

			AssertCellStart(code, 0, -1);
			AssertCellStart(code, 2, -1);
			AssertCellStart(code, 3, 3);
			AssertCellStart(code, 5, 3);
			AssertCellStart(code, 6, 6);
			AssertCellStart(code, 8, 6);
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void FindEndOfCodeCell()
		{
			var code = new MockTextBuffer(@"x
# ...
x
#%% cell here
x

#%% cell here
x
");

			AssertCellEnd(code, 0, 0);
			AssertCellEnd(code, 2, 2);
			AssertCellEnd(code, 3, 4);
			AssertCellEnd(code, 5, 4);  // Start in whitespace finds previous cell
			AssertCellEnd(code, 5, 5, withWhitespace: true);
			AssertCellEnd(code, 6, 7);
			AssertCellEnd(code, 8, 7);  // Start in whitespace finds previous cell
			AssertCellEnd(code, 8, 8, withWhitespace: true);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void FindStartOfCodeCellWithComment()
		{
			var code = new MockTextBuffer(@"
# Preceding comment
#
# and whitespace

#%% cell here
x

# Next preceding commint

#%% cell here
x
");

			AssertCellStart(code, 0, 1);
			AssertCellStart(code, 1, 1);
			AssertCellStart(code, 4, 1);
			AssertCellStart(code, 6, 1);
			AssertCellStart(code, 7, 8);
			AssertCellStart(code, 11, 8);
			AssertCellStart(code, 12, 8);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void FindEndOfCodeCellWithComment()
		{
			var code = new MockTextBuffer(@"
# Preceding comment
#
# and whitespace

#%% cell here
x

# Next preceding comment

#%% cell here
x
");

			AssertCellEnd(code, 0, 6);
			AssertCellEnd(code, 1, 6);
			AssertCellEnd(code, 4, 6);
			AssertCellEnd(code, 4, 7, withWhitespace: true);
			AssertCellEnd(code, 6, 6);
			AssertCellEnd(code, 7, 11);
			AssertCellEnd(code, 11, 11);
			AssertCellEnd(code, 11, 12, withWhitespace: true);
			AssertCellEnd(code, 12, 11);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void FindStartOfEmptyCodeCell()
		{
			var code = new MockTextBuffer(@"
#%% empty cell here

#%% cell after empty cell

");

			AssertCellStart(code, 1, 1);
			AssertCellStart(code, 2, 1);
			AssertCellStart(code, 3, 3);
			AssertCellStart(code, 4, 3);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void FindEndOfEmptyCodeCell()
		{
			var code = new MockTextBuffer(@"
#%% empty cell here

#%% cell after empty cell

");

			AssertCellEnd(code, 1, 1);
			AssertCellEnd(code, 1, 2, withWhitespace: true);
			AssertCellEnd(code, 3, 3);
			AssertCellEnd(code, 3, 5, withWhitespace: true);
		}

		private static void AssertTags(ITextBuffer buffer, params string[] spans)
		{
			var tags = OutliningTaggerProvider.OutliningTagger.ProcessCellTags(
				buffer.CurrentSnapshot,
				CancellationTokens.After1s
			).ToList();

			AssertUtil.AreEqual(tags.Select(t => t.Span.Span.ToString()), spans);
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void OutlineCodeCell()
		{
			AssertTags(new MockTextBuffer(@"#%% cell 1

x

# comment

#%% cell 2

y

"), "[10..15)", "[28..47)");

			// No tags, but should not time out
			AssertTags(new MockTextBuffer(@"#%% empty cell here

#%% cell after empty cell

"));

			AssertTags(new MockTextBuffer(@"#%% cell here

x
#%% empty cell

"), "[13..18)");

			AssertTags(new MockTextBuffer(@"#%% empty cell here

# comment before
#%% cell after empty cell

"), "[39..70)");
		}
	}
}
