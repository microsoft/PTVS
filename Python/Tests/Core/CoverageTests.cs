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
	[TestClass]
	public class CoverageTests
	{

		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestMethod]
		public void TestFlowControl()
		{
			RunOneTest(
				GetPath("FlowControl"),
				Module("test",
					Stats(20, 8, 25, 8),
					Global(
						Function("f",
							Stats(1, 1, 1, 1),
							Covered(34, 5, 14),
							Uncovered(35, 5, 23)
						)
					),
					Covered(1, 4, 8),
					Covered(2, 5, 21),
					Uncovered(4, 5, 23),
					Covered(6, 4, 9),
					Uncovered(7, 5, 23),
					Covered(9, 5, 21),
					Covered(11, 7, 12),
					Uncovered(12, 5, 23),
					Covered(14, 10, 12),
					Uncovered(15, 5, 23),
					Covered(17, 10, 17),
					Covered(18, 5, 21),
					Covered(21, 5, 31),
					Covered(23, 5, 21),
					Covered(27, 5, 31),
					Covered(28, 8, 21),
					Covered(29, 5, 23),
					Covered(31, 5, 21),
					Covered(37, 1, 8),
					Covered(40, 10, 17),
					Covered(41, 5, 10),
					Uncovered(42, 5, 23),
					Covered(45, 10, 17),
					Covered(46, 5, 13),
					Uncovered(47, 5, 23),
					Covered(49, 10, 17),
					Covered(50, 8, 13),
					Uncovered(51, 9, 14),
					Covered(52, 5, 21),
					Covered(55, 10, 17),
					Covered(56, 8, 13),
					Uncovered(57, 9, 17),
					Covered(58, 5, 21)
				)
			);
		}

		[TestMethod]
		public void TestStatements()
		{
			RunOneTest(
				GetPath("Statements"),
				Module("test",
					Stats(1, 0, 16, 0),
					Covered(1, 1, 6),
					Covered(2, 1, 7),
					Covered(3, 1, 22),
					Covered(4, 1, 6),
					Covered(5, 1, 5),
					Covered(6, 1, 14),
					Covered(7, 1, 23),
					Covered(8, 1, 9),
					Covered(9, 1, 11),
					Covered(10, 1, 9),
					Covered(12, 5, 10),
					Covered(14, 5, 9),
					Covered(17, 5, 10),
					Covered(19, 5, 9),
					Covered(21, 6, 27),
					Covered(22, 5, 10)
				)
			);
		}


		[TestMethod]
		public void TestMultiLineExpressions()
		{
			RunOneTest(
				GetPath("MultiLineExpressions"),
				Module("test",
					Stats(1, 0, 48, 0),
					Global(
						Function("<lambda>",
							Stats(0, 1, 0, 1),
							Uncovered(57, 3, 5)
						)
					),
					Covered(1, 2, 4),
					Covered(2, 1, 4),
					Covered(3, 1, 4),
					Covered(4, 1, 4),
					Covered(7, 1, 2),
					Covered(8, 1, 2),
					Covered(9, 1, 2),
					Covered(11, 1, 4),
					Covered(12, 1, 2),
					Covered(13, 1, 2),
					Covered(15, 1, 2),
					Covered(16, 1, 2),
					Covered(17, 2, 3),
					Covered(18, 1, 2),
					Covered(19, 1, 6),
					Covered(21, 2, 3),
					Covered(22, 5, 6),
					Covered(23, 1, 6),
					Covered(24, 4, 11),
					Covered(25, 1, 10),
					Covered(26, 6, 9),
					Covered(27, 2, 5),
					Covered(28, 1, 4),
					Covered(29, 1, 14),
					Covered(31, 2, 3),
					Covered(32, 1, 13),
					Covered(34, 1, 9),
					Covered(35, 2, 3),
					Covered(36, 1, 8),
					Covered(37, 1, 9),
					Covered(38, 1, 2),
					Covered(39, 1, 2),
					Covered(40, 1, 7),
					Covered(41, 1, 2),
					Covered(42, 1, 2),
					Covered(43, 1, 14),
					Covered(45, 1, 2),
					Covered(46, 1, 7),
					Covered(47, 1, 2),
					Covered(48, 1, 2),
					Covered(49, 1, 2),
					Covered(50, 1, 4),
					Covered(51, 1, 2),
					Covered(52, 1, 4),
					Covered(53, 2, 3),
					Covered(54, 1, 2),
					Covered(55, 1, 8),
					Covered(56, 1, 2)
				)
			);
		}


		[TestMethod]
		public void TestExpressions()
		{
			RunOneTest(
				GetPath("Expressions"),
				Module("test",
					Stats(1, 0, 24, 0),
					Covered(1, 1, 4),
					Covered(2, 1, 2),
					Covered(3, 1, 6),
					Covered(4, 1, 9),
					Covered(5, 1, 8),
					Covered(6, 1, 6),
					Covered(7, 1, 9),
					Covered(8, 1, 8),
					Covered(9, 1, 6),
					Covered(10, 1, 19),
					Covered(11, 1, 30),
					Covered(12, 1, 19),
					Covered(13, 1, 31),
					Covered(14, 1, 21),
					Covered(15, 1, 12),
					Covered(16, 1, 17),
					Covered(17, 1, 7),
					Covered(18, 1, 12),
					Covered(19, 1, 17),
					Covered(20, 1, 12),
					Covered(21, 1, 9),
					Covered(22, 1, 9),
					Covered(23, 1, 21),
					Covered(24, 1, 17)
				)
			);
		}

		[TestMethod]
		public void TestSimple()
		{
			RunOneTest(
				GetPath("Simple"),
				Module("test",
					Stats(1, 0, 3, 0),
					Global(
						Function("f",
							Stats(1, 0, 1, 0),
							Covered(1, 11, 16)
						),
						Function("g",
							Stats(3, 1, 5, 1),
							Covered(4, 8, 12),
							Covered(5, 9, 20),
							Uncovered(7, 9, 21),
							Covered(8, 5, 10),
							Covered(9, 5, 9),
							Covered(10, 5, 13)
						)
					),
					Covered(13, 1, 4),
					Covered(14, 1, 4),
					Covered(15, 1, 4)
				)
			);
		}

		[TestMethod]
		public void TestMultiModule()
		{
			RunOneTest(
				GetPath("MultiModule"),
				Module("xxx",
					Stats(1, 0, 11, 0),
					Global(
						Function("f",
							Stats(1, 0, 1, 0),
							Covered(4, 11, 16)
						),
						Function("g",
							Stats(3, 1, 6, 1),
							Covered(10, 8, 12),
							Covered(11, 9, 20),
							Uncovered(13, 9, 21),
							Covered(14, 5, 10),
							Covered(15, 5, 9),
							Covered(16, 5, 15),
							Covered(17, 5, 13)
						),
						Function("g.h",
							Stats(1, 0, 1, 0),
							Covered(8, 9, 18)
						),
						Function("func",
							Stats(1, 0, 4, 0),
							Covered(20, 5, 11),
							Covered(21, 9, 10),
							Covered(22, 9, 10),
							Covered(23, 5, 13)
						)
					),
					Class("C.D",
						Stats(0, 0, 0, 0),
						Function("__init__",
							Stats(1, 0, 1, 0),
							Covered(28, 13, 30)
						)
					),
					Class("C",
						Stats(1, 0, 1, 0),
						Function("__init__",
							Stats(1, 0, 2, 0),
							Covered(32, 9, 27),
							Covered(33, 9, 23)
						),
						Covered(30, 5, 10)
					),
					Covered(1, 1, 11),
					Covered(2, 1, 12),
					Covered(3, 1, 11),
					Covered(35, 1, 10),
					Covered(37, 1, 4),
					Covered(38, 1, 4),
					Covered(39, 1, 4),
					Covered(42, 1, 5),
					Covered(43, 3, 4),
					Covered(44, 3, 4),
					Covered(45, 3, 4)
				),
				Module("yyy",
					Stats(1, 0, 1, 0),
					Covered(1, 1, 12)
				),
				Module("foo.blah",
					Stats(1, 0, 1, 0),
					Covered(1, 1, 12)
				),
				Module("foo",
					Stats(1, 0, 2, 0),
					Covered(1, 1, 12),
					Covered(2, 1, 12)
				)
			);
		}


		private static string GetPath(string name)
		{
			return TestData.GetPath(Path.Combine("TestData", "Coverage", name, "coverage.xml"));
		}

		private void RunOneTest(string inputFile, params ModuleCoverage[] expected)
		{
			string baseDir = Path.GetDirectoryName(inputFile);
			using (FileStream tmp = new FileStream(inputFile, FileMode.Open))
			{
				// Read in the data from coverage.py's XML file
				CoverageFileInfo[] fileInfo = new CoveragePyConverter(baseDir, tmp).Parse();

				// Convert that into offsets within the actual code
				var covInfo = ImportCoverageCommand.Import(fileInfo);

				try
				{
					foreach (var test in expected)
					{
						test.Validate(covInfo);
					}
				}
				finally
				{
					DumpCoverage(covInfo);
				}

				// verify we produce the expected .coveragexml
				var exportedFile = Path.Combine(Path.GetDirectoryName(inputFile), "coverage.coveragexml");
				if (File.Exists(exportedFile))
				{
					var expectedExport = File.ReadAllText(exportedFile);
					MemoryStream outputStream = new MemoryStream();
					new CoverageExporter(
						outputStream,
						covInfo
					).Export();
					outputStream.Flush();
					outputStream.Seek(0, SeekOrigin.Begin);
					var exported = new StreamReader(outputStream).ReadToEnd();

					Assert.AreEqual(
						expectedExport.Replace("||basedir||", TestData.GetPath("")),
						exported
					);
				}
			}
		}

		class Call
		{
			public readonly string Header;
			public readonly Call[] Children;

			public Call(string header, Call[] calls)
			{
				Header = header;
				Children = calls;
			}

			public override string ToString()
			{
				StringBuilder tmp = new StringBuilder();
				ToString(tmp, 3);
				return tmp.ToString();
			}

			public void ToString(StringBuilder builder, int depth)
			{
				string indent = new string(' ', depth * 4);

				if (Header != null)
				{
					builder.Append(indent);
					builder.Append(Header);
					if (Children.Length > 0)
					{
						builder.AppendLine();
					}
				}

				for (int i = 0; i < Children.Length; i++)
				{
					Children[i].ToString(builder, depth + 1);
					if (i != Children.Length - 1)
					{
						builder.AppendLine(",");
					}
					else
					{
						builder.AppendLine("");
					}
				}

				if (Header != null)
				{
					if (Children.Length > 0)
					{
						builder.Append(indent);
					}
					builder.Append(")");
				}
			}
		}

		private void DumpCoverage(Dictionary<CoverageFileInfo, CoverageMapper> covInfo)
		{
			List<Call> calls = new List<Call>();
			foreach (var file in covInfo)
			{
				List<Call> children = new List<Call>();

				children.Add(WriteStats(file.Value.GlobalScope));

				var methods = WriteMethods(file.Value.GlobalScope);
				if (methods.Length > 0)
				{
					children.Add(new Call("Global(", methods));
				}

				foreach (var klass in file.Value.Classes)
				{
					List<Call> classMembers = new List<Call>();
					classMembers.Add(WriteStats(klass));
					classMembers.AddRange(WriteMethods(klass));
					classMembers.AddRange(WriteLines(klass.Lines));

					children.Add(
						new Call(
							string.Format("Class(\"{0}\", ", CoverageExporter.GetQualifiedName(klass.Statement)),
							classMembers.ToArray()
						)
					);
				}

				children.AddRange(WriteLines(file.Value.GlobalScope.Lines));

				calls.Add(
					new Call(
						string.Format("Module(\"{0}\", ", file.Value.ModuleName),
						children.ToArray()
					)
				);

			}
			Console.WriteLine("-----");
			Console.WriteLine(new Call(null, calls.ToArray()).ToString());
		}

		private Call WriteStats(CoverageScope scope)
		{
			return new Call(
				string.Format(
					"Stats({0}, {1}, {2}, {3}",
					scope.BlocksCovered,
					scope.BlocksNotCovered,
					scope.LinesCovered,
					scope.LinesNotCovered
				),
				Array.Empty<Call>()
			);
		}

		private Call[] WriteMethods(CoverageScope klass)
		{
			List<Call> calls = new List<Call>();

			WriteMethods(calls, klass);

			return calls.ToArray();
		}

		private void WriteMethods(List<Call> calls, CoverageScope klass)
		{
			foreach (var child in klass.Children)
			{
				FunctionDefinition funcDef = child.Statement as FunctionDefinition;
				if (funcDef != null)
				{
					List<Call> lines = new List<Call>();
					lines.Add(WriteStats(child));
					lines.AddRange(WriteLines(child.Lines));

					calls.Add(
						new Call(
							string.Format(
								"Function(\"{0}\",",
								CoverageExporter.GetQualifiedFunctionName(child.Statement)
							),
							lines.ToArray()
						)
					);

					WriteMethods(calls, child);
				}
			}
		}

		private Call[] WriteLines(SortedDictionary<int, CoverageLineInfo> lineInfo)
		{
			List<Call> calls = new List<Call>();
			foreach (var keyValue in lineInfo)
			{
				calls.Add(
					new Call(
						string.Format("{0}({1}, {2}, {3}",
							keyValue.Value.Covered ? "Covered" : "Uncovered",
							keyValue.Key,
							keyValue.Value.ColumnStart,
							keyValue.Value.ColumnEnd
						),
						Array.Empty<Call>()
					)
				);
			}

			return calls.ToArray();
		}

		private static ModuleCoverage Module(string name, params ExpectedCoverage[] expected)
		{
			return new ModuleCoverage(name, expected);
		}

		private static ExpectedCoverage Class(string name, params ExpectedCoverage[] expected)
		{
			return new ClassCoverage(name, expected);
		}

		private static ExpectedCoverage Global(params ExpectedCoverage[] expected)
		{
			return new GlobalCoverage(expected);
		}


		private static ExpectedCoverage Function(string name, params ExpectedCoverage[] expected)
		{
			return new FunctionCoverage(name, expected);
		}

		private static ExpectedCoverage Covered(int lineNo, int startColumn, int endColumn)
		{
			return new LineCoverage(lineNo, startColumn, endColumn, true);
		}

		private static ExpectedCoverage Uncovered(int lineNo, int startColumn, int endColumn)
		{
			return new LineCoverage(lineNo, startColumn, endColumn, false);
		}

		private static ExpectedCoverage Stats(int blocksCovered, int blocksNotCovered, int linesCovered, int linesNotCovered)
		{
			return new StatsCoverage(blocksCovered, blocksNotCovered, linesCovered, linesNotCovered);
		}

		abstract class ExpectedCoverage
		{
			internal abstract void Validate(CoverageMapper mapper, CoverageScope parentScope);
		}

		class ModuleCoverage
		{
			private readonly ExpectedCoverage[] _expected;
			private readonly string _name;

			public ModuleCoverage(string name, ExpectedCoverage[] expected)
			{
				_name = name;
				_expected = expected;
			}

			public void Validate(Dictionary<CoverageFileInfo, CoverageMapper> info)
			{
				foreach (var keyValue in info)
				{
					if (keyValue.Value.ModuleName == _name)
					{
						foreach (var expected in _expected)
						{
							expected.Validate(
								keyValue.Value,
								keyValue.Value.GlobalScope
							);
						}
						return;
					}
				}
				Assert.Fail("Failed to find module: " + _name);
			}
		}

		class ClassCoverage : ExpectedCoverage
		{
			private readonly ExpectedCoverage[] _expected;
			private readonly string _name;

			public ClassCoverage(string name, ExpectedCoverage[] expected)
			{
				_name = name;
				_expected = expected;
			}

			internal override void Validate(CoverageMapper mapper, CoverageScope parentScope)
			{
				foreach (var scope in mapper.Classes)
				{
					if (CoverageExporter.GetQualifiedName(scope.Statement) == _name)
					{
						foreach (var expected in _expected)
						{
							expected.Validate(mapper, scope);
						}
						return;
					}
				}

				Assert.Fail("Failed to find class: " + _name);
			}

		}

		class GlobalCoverage : ExpectedCoverage
		{
			private readonly ExpectedCoverage[] _expected;

			public GlobalCoverage(ExpectedCoverage[] expected)
			{
				_expected = expected;
			}

			internal override void Validate(CoverageMapper mapper, CoverageScope parentScope)
			{
				foreach (var expected in _expected)
				{
					expected.Validate(mapper, parentScope);
				}
			}
		}

		class FunctionCoverage : ExpectedCoverage
		{
			private readonly ExpectedCoverage[] _expected;
			private readonly string _name;

			public FunctionCoverage(string name, ExpectedCoverage[] expected)
			{
				_name = name;
				_expected = expected;
			}

			internal override void Validate(CoverageMapper mapper, CoverageScope parentScope)
			{
				Assert.IsTrue(
					FindFunction(mapper, parentScope),
					"Failed to find function: " + _name
				);
			}

			private bool FindFunction(CoverageMapper mapper, CoverageScope parentScope)
			{
				foreach (var scope in parentScope.Children)
				{
					if (scope.Statement is FunctionDefinition)
					{
						if (CoverageExporter.GetQualifiedFunctionName(scope.Statement) == _name)
						{
							foreach (var expected in _expected)
							{
								expected.Validate(mapper, scope);
							}
							return true;
						}

						if (FindFunction(mapper, scope))
						{
							// nested function...
							return true;
						}
					}
				}
				return false;
			}
		}

		class LineCoverage : ExpectedCoverage
		{
			private readonly int _endColumn;
			private readonly int _lineNo;
			private readonly int _startColumn;
			private readonly bool _covered;

			public LineCoverage(int lineNo, int startColumn, int endColumn, bool covered)
			{
				_lineNo = lineNo;
				_startColumn = startColumn;
				_endColumn = endColumn;
				_covered = covered;
			}

			internal override void Validate(CoverageMapper mapper, CoverageScope parentScope)
			{
				Assert.IsTrue(parentScope.Lines.ContainsKey(_lineNo), "Line number is missing coverage information");
				var covInfo = parentScope.Lines[_lineNo];

				Assert.AreEqual(_startColumn, covInfo.ColumnStart);
				Assert.AreEqual(_endColumn, covInfo.ColumnEnd);
				Assert.AreEqual(_covered, covInfo.Covered);
			}
		}

		class StatsCoverage : ExpectedCoverage
		{
			private readonly int BlocksCovered, BlocksNotCovered, LinesCovered, LinesNotCovered;

			public StatsCoverage(int blocksCovered, int blocksNotCovered, int linesCovered, int linesNotCovered)
			{
				BlocksCovered = blocksCovered;
				BlocksNotCovered = blocksNotCovered;
				LinesCovered = linesCovered;
				LinesNotCovered = linesNotCovered;
			}

			internal override void Validate(CoverageMapper mapper, CoverageScope parentScope)
			{
				Assert.AreEqual(BlocksCovered, parentScope.BlocksCovered);
				Assert.AreEqual(BlocksNotCovered, parentScope.BlocksNotCovered);
				Assert.AreEqual(LinesCovered, parentScope.LinesCovered);
				Assert.AreEqual(LinesNotCovered, parentScope.LinesNotCovered);
			}
		}
	}
}
