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

extern alias pythontools;

namespace PythonToolsTests
{
	[TestClass]
	public class FuzzyStringMatcherTests
	{
		static List<string> Data = new List<string> {
			"hello",
			"hello_world",
			"helloWorld",
			"HelloWorld",
			"Hello_World",
			"hElLoWoRlD",
			"world_hello"
		};

		private class Comparer : IComparer<string>
		{
			readonly Dictionary<string, int> _cache;
			readonly string _pattern;
			readonly FuzzyStringMatcher _outer;

			public Comparer(string pattern, FuzzyStringMatcher outer)
			{
				_cache = new Dictionary<string, int>();
				_pattern = pattern;
				_outer = outer;
			}

			public int Compare(string x, string y)
			{
				if (!_cache.TryGetValue(x, out global::System.Int32 value1))
				{
					_cache[x] = value1 = _outer.GetSortKey(x, _pattern);
				}
				if (!_cache.TryGetValue(y, out global::System.Int32 value2))
				{
					_cache[y] = value2 = _outer.GetSortKey(y, _pattern);
				}

				// Comparison uses reverse order to sort best matches earlier.
				return Comparer<int>.Default.Compare(value2, value1);
			}
		}

		void TestSortOrderInternal(string pattern, FuzzyStringMatcher comparer, IEnumerable<string> data, string[] expected)
		{
			var sorted = data.Where(s => comparer.IsCandidateMatch(s, pattern))
				.Select((s, i) => new { S = s, I = i })
				.OrderBy(t => t.S, new Comparer(pattern, comparer))
				.ThenBy(t => t.I)
				.Select(t => t.S)
				.ToList();
			Console.WriteLine("Pattern:  {0}", pattern);
			Console.WriteLine("Expected: {0}", string.Join(", ", expected));
			Console.WriteLine("Actual:   {0}", string.Join(", ", sorted));
			Console.WriteLine();
			Assert.AreEqual(expected.Length, sorted.Count);
			foreach (var tup in sorted.Zip(expected, (x, y) => Tuple.Create(x, y)))
			{
				Assert.AreEqual(tup.Item2, tup.Item1);
			}
		}

		void TestSortOrder(string pattern, FuzzyStringMatcher comparer, params string[] expected)
		{
			TestSortOrderInternal(pattern, comparer, Data, expected);
		}

		void TestSortOrder(string pattern, FuzzyStringMatcher comparer, IEnumerable<string> data, params string[] expected)
		{
			TestSortOrderInternal(pattern, comparer, data, expected);
		}

		string GetBestMatch(string pattern, FuzzyStringMatcher comparer, params string[] options)
		{
			string best = null;
			int bestValue = 0;
			foreach (var opt in options)
			{
				int value = comparer.GetSortKey(opt, pattern);
				Console.WriteLine("GetSortKey(\"{0}\", \"{1}\") = {2}", opt, pattern, value);
				if (value > bestValue)
				{
					best = opt;
					bestValue = value;
				}
			}

			Console.WriteLine();
			return best;
		}


		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortKey_Prefix()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Prefix);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(10, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(0, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortKey_PrefixIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.PrefixIgnoreCase);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(10, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(5, cmp.GetSortKey("HelloWorld", "hello"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortOrder_Prefix()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Prefix);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld");
			TestSortOrder("hellow", cmp);
			TestSortOrder("helloW", cmp, "helloWorld");
			TestSortOrder("world", cmp, "world_hello");
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortOrder_PrefixIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.PrefixIgnoreCase);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "HelloWorld", "Hello_World", "hElLoWoRlD");
			TestSortOrder("hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("helloW", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("Hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("world", cmp, "world_hello");
		}


		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortKey_Substring()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Substring);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(11, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(0, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "llo"));
			Assert.AreEqual(4, cmp.GetSortKey("hello", "ll"));
			Assert.AreEqual(4, cmp.GetSortKey("hello", "lo"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortKey_SubstringIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.SubstringIgnoreCase);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(11, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("HelloWorld", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "llo"));
			Assert.AreEqual(3, cmp.GetSortKey("hello", "LLO"));
			Assert.AreEqual(2, cmp.GetSortKey("hello", "LL"));
			Assert.AreEqual(2, cmp.GetSortKey("hello", "LO"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortOrder_Substring()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Substring);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "world_hello");
			TestSortOrder("hellow", cmp);
			TestSortOrder("helloW", cmp, "helloWorld");
			TestSortOrder("world", cmp, "world_hello", "hello_world");
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortOrder_SubstringIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.SubstringIgnoreCase);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "world_hello", "HelloWorld", "Hello_World", "hElLoWoRlD");
			TestSortOrder("hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("helloW", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("Hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("world", cmp, "world_hello", "hello_world", "helloWorld", "HelloWorld", "Hello_World", "hElLoWoRlD");
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortKey_Fuzzy()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Fuzzy);
			Assert.AreEqual(35, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(35, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(0, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "llo"));
			Assert.AreEqual(3, cmp.GetSortKey("hello", "ll"));
			Assert.AreEqual(2, cmp.GetSortKey("hello", "lo"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortKey_FuzzyIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreCase);
			Assert.AreEqual(35, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(35, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(34, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(34, cmp.GetSortKey("HelloWorld", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "llo"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "LLO"));
			Assert.AreEqual(-1, cmp.GetSortKey("hello", "LL"));
			Assert.AreEqual(-2, cmp.GetSortKey("hello", "LO"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(UnitTestPriority.P0), TestCategory("FuzzyStringMatcher")]
		public void SortOrder_Fuzzy()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Fuzzy);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "world_hello");
			TestSortOrder("hellow", cmp, "hello_world");
			TestSortOrder("helloW", cmp, "helloWorld");
			TestSortOrder("world", cmp, "world_hello", "hello_world");
		}

		[TestMethod, Priority(0), TestCategory("FuzzyStringMatcher")]
		public void SortOrder_FuzzyIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreCase);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "HelloWorld", "Hello_World", "hElLoWoRlD", "world_hello");
			TestSortOrder("hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD", "hello_world", "Hello_World");
			TestSortOrder("helloW", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD", "Hello_World", "hello_world");
			TestSortOrder("Hellow", cmp, "HelloWorld", "helloWorld", "hElLoWoRlD", "Hello_World", "hello_world");
			TestSortOrder("world", cmp, "world_hello", "hello_world", "Hello_World", "helloWorld", "HelloWorld", "hElLoWoRlD");
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortKey_FuzzyIgnoreLowerCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreLowerCase);
			Assert.AreEqual(35, cmp.GetSortKey("hello", "hello"));
			Assert.AreEqual(35, cmp.GetSortKey("helloWorld", "hello"));
			Assert.AreEqual(34, cmp.GetSortKey("Hello", "hello"));
			Assert.AreEqual(34, cmp.GetSortKey("HelloWorld", "hello"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "llo"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "LLO"));
			Assert.AreEqual(-1, cmp.GetSortKey("hello", "LL"));
			Assert.AreEqual(-2, cmp.GetSortKey("hello", "LO"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "helloThere"));
		}

		[TestMethod, Priority(0), TestCategory("FuzzyStringMatcher")]
		public void SortOrder_FuzzyIgnoreLowerCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreLowerCase);
			TestSortOrder("hello", cmp, "hello", "hello_world", "helloWorld", "HelloWorld", "Hello_World", "hElLoWoRlD", "world_hello");
			TestSortOrder("hellow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD", "hello_world", "Hello_World");
			TestSortOrder("helloW", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD", "Hello_World", "hello_world");
			TestSortOrder("Hellow", cmp, "HelloWorld", "helloWorld", "hElLoWoRlD", "Hello_World", "hello_world");
			TestSortOrder("world", cmp, "world_hello", "hello_world", "Hello_World", "helloWorld", "HelloWorld", "hElLoWoRlD");
		}


		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortKey_Regex()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Regex);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "he..o"));
			Assert.AreEqual(11, cmp.GetSortKey("helloWorld", "he..o"));
			Assert.AreEqual(0, cmp.GetSortKey("Hello", "he.+o"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "l+o"));
			Assert.AreEqual(4, cmp.GetSortKey("hello", "l+"));
			Assert.AreEqual(4, cmp.GetSortKey("hello", "lo(x?)"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "h[elo]+There"));
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortKey_RegexIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.RegexIgnoreCase);
			Assert.AreEqual(11, cmp.GetSortKey("hello", "he..o"));
			Assert.AreEqual(11, cmp.GetSortKey("helloWorld", "he..o"));
			Assert.AreEqual(6, cmp.GetSortKey("Hello", "he.+o"));
			Assert.AreEqual(8, cmp.GetSortKey("HelloWorld", "he.+o"));
			Assert.AreEqual(6, cmp.GetSortKey("hello", "l+o"));
			Assert.AreEqual(3, cmp.GetSortKey("hello", "L+O"));
			Assert.AreEqual(2, cmp.GetSortKey("hello", "L+"));
			Assert.AreEqual(2, cmp.GetSortKey("hello", "LO(X?)"));
			Assert.AreEqual(0, cmp.GetSortKey("hello", "h[elo]+There"));
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void SortOrder_Regex()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.Regex);
			TestSortOrder("h.+o", cmp, "hello_world", "helloWorld", "hElLoWoRlD", "hello", "world_hello");
			TestSortOrder("h[^_]+w", cmp);
			TestSortOrder("h[^_]+W", cmp, "helloWorld", "hElLoWoRlD");
			TestSortOrder("w.+d", cmp, "world_hello", "hello_world");
		}

		[TestMethod, Priority(UnitTestPriority.P1)]
		public void SortOrder_RegexIgnoreCase()
		{
			var cmp = new FuzzyStringMatcher(FuzzyMatchMode.RegexIgnoreCase);
			TestSortOrder("h.+o", cmp, "hello_world", "helloWorld", "hElLoWoRlD", "hello", "world_hello", "Hello_World", "HelloWorld");
			TestSortOrder("h.+ow", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("h[^_]+W", cmp, "helloWorld", "hElLoWoRlD", "HelloWorld");
			TestSortOrder("H[^_]+w", cmp, "helloWorld", "HelloWorld", "hElLoWoRlD");
			TestSortOrder("w.+d", cmp, "world_hello", "hello_world", "helloWorld", "HelloWorld", "Hello_World", "hElLoWoRlD");
		}


		[TestMethod, Priority(0), TestCategory("FuzzyStringMatcher")]
		public void FuzzyAcronymCompletions()
		{
			var cmp1 = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreLowerCase);
			var cmp2 = new FuzzyStringMatcher(FuzzyMatchMode.FuzzyIgnoreCase);

			Assert.AreEqual("MyClassName", GetBestMatch("MCN", cmp1, "MyClassName", "MyOtherClassName"));
			Assert.AreEqual("MyOtherClassName", GetBestMatch("MOCN", cmp1, "MyClassName", "MyOtherClassName"));

			// FuzzyIgnoreLowerCase is required to handle the case where a
			// lowercase character appears before its uppercase, which spoils
			// the score.
			Assert.AreEqual("MycClassName", GetBestMatch("MCN", cmp1, "MycClassName", "MyClassName"));
			Assert.AreEqual("MyClassName", GetBestMatch("MCN", cmp2, "MycClassName", "MyClassName"));
		}
	}
}
