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

namespace TestUtilities.Python
{
	public class PythonAnalysis : IDisposable
	{
		private readonly IPythonInterpreterFactory _factory;
		private readonly PythonAnalyzer _analyzer;
		private readonly Dictionary<string, IPythonProjectEntry> _entries;

		private readonly ConcurrentDictionary<IPythonProjectEntry, TaskCompletionSource<CollectingErrorSink>> _tasks;
		private readonly Dictionary<BuiltinTypeId, string[]> _cachedMembers;

		private readonly string _root;
		private readonly bool _disposeFactory;
		private bool _disposed;

		public PythonAnalysis(PythonLanguageVersion version)
			: this(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion())) { }

		private static IPythonInterpreterFactory TryFindFactory(string idOrDescription)
		{
			var provider = new CPythonInterpreterFactoryProvider(null, watchRegistry: false);
			var factory = provider.GetInterpreterFactory(idOrDescription);
			if (factory == null)
			{
				var config = provider.GetInterpreterConfigurations().FirstOrDefault(c => idOrDescription.Equals(c.Description, StringComparison.OrdinalIgnoreCase));
				if (config != null)
				{
					factory = provider.GetInterpreterFactory(config.Id);
				}
			}
			if (factory == null)
			{
				Assert.Inconclusive("Requested interpreter '{0}' is not installed", idOrDescription);
				return null;
			}

			return factory;
		}

		public PythonAnalysis(string idOrDescription)
			: this(TryFindFactory(idOrDescription)) { }

		public PythonAnalysis(Func<IPythonInterpreterFactory> factoryCreator)
			: this(factoryCreator())
		{
			_disposeFactory = true;
		}

		public PythonAnalysis(IPythonInterpreterFactory factory)
		{
			if (factory == null)
			{
				Assert.Inconclusive("Expected interpreter is not installed");
			}
			_factory = factory;
			_analyzer = PythonAnalyzer.CreateAsync(factory).WaitAndUnwrapExceptions();
			_entries = new Dictionary<string, IPythonProjectEntry>(StringComparer.OrdinalIgnoreCase);
			_tasks = new ConcurrentDictionary<IPythonProjectEntry, TaskCompletionSource<CollectingErrorSink>>();
			_cachedMembers = new Dictionary<BuiltinTypeId, string[]>();
			_root = TestData.GetTempPath();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~PythonAnalysis()
		{
			Dispose(false);
		}

		protected void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;

			if (disposing)
			{
				_analyzer.Dispose();
				if (_disposeFactory)
				{
					(_factory as IDisposable)?.Dispose();
				}
			}
		}

		public PythonAnalyzer Analyzer => _analyzer;
		public IReadOnlyDictionary<string, IPythonProjectEntry> Modules => _entries;
		public string CodeFolder => _root;

		public bool CreateProjectOnDisk { get; set; }
		public string DefaultModule { get; set; }
		public IModuleContext ModuleContext { get; set; }
		public bool AssertOnParseErrors { get; set; }

		public virtual BuiltinTypeId BuiltinTypeId_Str => _analyzer.LanguageVersion.Is3x() ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
		public virtual BuiltinTypeId BuiltinTypeId_StrIterator => _analyzer.LanguageVersion.Is3x() ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;


		public IPythonProjectEntry AddModule(string name, string code, string filename = null)
		{
			var path = Path.Combine(_root, (filename ?? (name.Replace('.', '\\') + ".py")));
			if (CreateProjectOnDisk)
			{
				Directory.CreateDirectory(PathUtils.GetParent(path));
				File.WriteAllText(path, code);
			}

			var entry = _analyzer.AddModule(name, path);
			_entries[name] = entry;
			if (DefaultModule == null)
			{
				DefaultModule = name;
			}

			UpdateModule(entry, code);
			return entry;
		}

		public void UpdateModule(IPythonProjectEntry entry, string code)
		{
			CollectingErrorSink errors = null;
			if (code != null)
			{
				errors = new CollectingErrorSink();
				var parser = Parser.CreateParser(
					new StringReader(code),
					_analyzer.LanguageVersion,
					new ParserOptions { BindReferences = true, ErrorSink = errors }
				);
				using (var p = entry.BeginParse())
				{
					p.Tree = parser.ParseFile();
					p.Complete();
				}
				if (errors.Errors.Any() || errors.Warnings.Any())
				{
					if (AssertOnParseErrors)
					{
						var errorMsg = MakeMessage(errors);
						Trace.TraceError(errorMsg);
						Assert.Fail("Errors parsing " + entry.FilePath, errorMsg);
					}
				}
			}

			entry.PreAnalyze();
		}

		private static string MakeMessage(CollectingErrorSink errors)
		{
			var sb = new StringBuilder();
			if (errors.Errors.Any())
			{
				sb.AppendLine("Errors:");
				foreach (var e in errors.Errors)
				{
					sb.AppendLine("  [{0}] {1}".FormatInvariant(Format(e.Span), e.Message));
				}
				sb.AppendLine();
			}
			if (errors.Warnings.Any())
			{
				sb.AppendLine("Warnings:");
				foreach (var e in errors.Warnings)
				{
					sb.AppendLine("  [{0}] {1}".FormatInvariant(Format(e.Span), e.Message));
				}
			}
			return sb.ToString();
		}

		private static string Format(SourceSpan span)
		{
			return $"({span.Start.Index},{span.Start.Line},{span.Start.Column})-({span.End.Index},{span.End.Line},{span.End.Column})";
		}

		public void SetLimits(AnalysisLimits limits)
		{
			_analyzer.Limits = limits;
		}

		public void SetSearchPaths(params string[] paths)
		{
			_analyzer.SetSearchPaths(paths);
		}

		public void SetTypeStubSearchPath(params string[] paths)
		{
			_analyzer.SetTypeStubPaths(paths);
		}

		public void ReanalyzeAll(CancellationToken? cancel = null)
		{
			foreach (var entry in _entries.Values)
			{
				entry.PreAnalyze();
			}
			WaitForAnalysis(cancel);
		}

		public void WaitForAnalysis(CancellationToken? cancel = null)
		{
			_analyzer.AnalyzeQueuedEntries(cancel ?? CancellationTokens.After5s);
			AssertListener.ThrowUnhandled();
		}

		private void Entry_OnNewAnalysis(object sender, EventArgs e)
		{
			var entry = sender as IPythonProjectEntry;
			Debug.Assert(entry != null);
			entry.NewAnalysis -= Entry_OnNewAnalysis;

			if (_tasks.TryRemove(entry, out TaskCompletionSource<CollectingErrorSink> task))
			{
				task.TrySetResult(task.Task.AsyncState as CollectingErrorSink);
			}
			else
			{
				Debug.Fail("Unexpected completion event");
			}
		}

		public string GetLogContent(IFormatProvider culture)
		{
			var factory = _factory as IPythonInterpreterFactoryWithLog;
			return factory?.GetAnalysisLogContent(culture);
		}

		#region Get Analysis Results

		public IEnumerable<string> GetMemberNames(string exprText, int index = 0, GetMemberOptions options = GetMemberOptions.None)
		{
			return GetMemberNames(_entries[DefaultModule], exprText, index, options);
		}

		public IEnumerable<string> GetMemberNames(IPythonProjectEntry module, string exprText, int index = 0, GetMemberOptions options = GetMemberOptions.None)
		{
			return module.Analysis.GetMembersByIndex(exprText, index, options).Select(m => m.Completion);
		}

		public IEnumerable<IPythonType> GetTypes(string exprText, int index = 0)
		{
			return GetTypes(_entries[DefaultModule], exprText, index);
		}

		public IEnumerable<IPythonType> GetTypes(IPythonProjectEntry module, string exprText, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(exprText, index).Select(m => m.PythonType);
		}

		public IEnumerable<BuiltinTypeId> GetTypeIds(string exprText, int index = 0)
		{
			return GetTypeIds(_entries[DefaultModule], exprText, index);
		}

		public IEnumerable<BuiltinTypeId> GetTypeIds(IPythonProjectEntry module, string exprText, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(exprText, index).Select(m =>
			{
				if (m.TypeId != BuiltinTypeId.Unknown)
				{
					return m.TypeId;
				}
				if ((m.PythonType?.TypeId ?? BuiltinTypeId.Unknown) != BuiltinTypeId.Unknown)
				{
					return m.PythonType.TypeId;
				}

				var state = _analyzer;
				if (m.PythonType.TypeId == BuiltinTypeId.NoneType)
				{
					return BuiltinTypeId.NoneType;
				}

				var bci = m as IBuiltinClassInfo;
				if (bci == null)
				{
					if (m is IBuiltinInstanceInfo bii)
					{
						bci = bii.ClassInfo;
					}
				}

				if (bci != null)
				{
					var count = (int)BuiltinTypeIdExtensions.LastTypeId;
					for (var i = 1; i <= count; ++i)
					{
						var bti = (BuiltinTypeId)i;
						if (!bti.IsVirtualId() && bci.PythonType == state.Interpreter.GetBuiltinType(bti))
						{
							return bti;
						}
					}
				}

				return BuiltinTypeId.Unknown;
			});
		}

		public IEnumerable<string> GetDescriptions(string variable, int index = 0)
		{
			return GetDescriptions(_entries[DefaultModule], variable, index);
		}

		public IEnumerable<string> GetDescriptions(IPythonProjectEntry module, string variable, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(variable, index).Select(m => m.Description);
		}

		public IEnumerable<string> GetDocumentations(string variable, int index = 0)
		{
			return GetDocumentations(_entries[DefaultModule], variable, index);
		}

		public IEnumerable<string> GetDocumentations(IPythonProjectEntry module, string variable, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(variable, index).Select(m => m.Documentation);
		}

		public IEnumerable<string> GetShortDescriptions(string variable, int index = 0)
		{
			return GetShortDescriptions(_entries[DefaultModule], variable, index);
		}

		public IEnumerable<string> GetShortDescriptions(IPythonProjectEntry module, string variable, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(variable, index).Select(m => m.ShortDescription);
		}

		public IEnumerable<string> GetCompletionDocumentation(string variable, string memberName, int index = 0)
		{
			return GetCompletionDocumentation(_entries[DefaultModule], variable, memberName, index);
		}

		public IEnumerable<string> GetCompletionDocumentation(IPythonProjectEntry module, string variable, string memberName, int index = 0)
		{
			return GetMember(module, variable, memberName, index).Select(m => m.Documentation);
		}

		public IEnumerable<MemberResult> GetMember(string variable, string memberName, int index = 0)
		{
			return GetMember(_entries[DefaultModule], variable, memberName, index);
		}

		public IEnumerable<MemberResult> GetMember(IPythonProjectEntry module, string variable, string memberName, int index = 0)
		{
			return module.Analysis.GetMembersByIndex(variable, index).Where(m => m.Name == memberName).OfType<MemberResult>();
		}

		public IEnumerable<string> GetAllNames(int index = 0)
		{
			return GetAllNames(_entries[DefaultModule], index);
		}

		public IEnumerable<string> GetAllNames(IPythonProjectEntry module, int index = 0)
		{
			return module.Analysis.GetAllMembers(SourceLocation.MinValue).OfType<MemberResult>().Select(m => m.Name);
		}

		public IEnumerable<string> GetNamesNoBuiltins(int index = 0, bool includeDunder = true)
		{
			return GetNamesNoBuiltins(_entries[DefaultModule], index, includeDunder);
		}

		public IEnumerable<string> GetNamesNoBuiltins(IPythonProjectEntry module, int index = 0, bool includeDunder = true)
		{
			var location = module.Tree.IndexToLocation(index);
			return Enumerable.Empty<string>();
			//var res = module.Analysis.GetMembers().GetVariablesNoBuiltins(location);
			//if (includeDunder) {
			//    return res;
			//}
			//return res.Where(n => n.Length < 4 || !n.StartsWith("__") || !n.EndsWith("__"));
		}

		public AnalysisValue[] GetValues(string variable, int index = 0)
		{
			return GetValues(_entries[DefaultModule], variable, index);
		}

		public AnalysisValue[] GetValues(IPythonProjectEntry module, string variable, int index = 0)
		{
			return module.Analysis.GetValuesByIndex(variable, index).ToArray();
		}

		public T GetValue<T>(string variable, int index = 0) where T : IAnalysisValue
		{
			return GetValue<T>(_entries[DefaultModule], variable, index);
		}

		public T GetValue<T>(IPythonProjectEntry module, string variable, int index = 0) where T : IAnalysisValue
		{
			var rs = module.Analysis.GetValuesByIndex(variable, index).ToArray();
			if (rs.Length == 0)
			{
				Assert.Fail("'{0}.{1}' had no variables".FormatInvariant(module.ModuleName, variable));
			}
			else if (rs.Length > 1)
			{
				foreach (var r in rs)
				{
					Trace.TraceInformation(r.ToString());
				}
				Assert.Fail("'{0}.{1}' had multiple values: {2}".FormatInvariant(module.ModuleName, variable, AnalysisSet.Create(rs)));
			}
			else
			{
				IAnalysisValue value = rs[0];
				Assert.IsInstanceOfType(value, typeof(T), "'{0}.{1}' was not expected type".FormatInvariant(module.ModuleName, variable));
				return (T)value;
			}
			return default(T);
		}

		public IOverloadResult[] GetSignatures(string exprText, int index = 0)
		{
			return GetSignatures(_entries[DefaultModule], exprText, index);
		}

		public IOverloadResult[] GetSignatures(IPythonProjectEntry module, string exprText, int index = 0)
		{
			return module.Analysis.GetSignaturesByIndex(exprText, index).ToArray();
		}

		public IOverloadResult[] GetOverrideable(int index = 0)
		{
			return GetOverrideable(_entries[DefaultModule], index);
		}

		public IOverloadResult[] GetOverrideable(IPythonProjectEntry module, int index = 0)
		{
			return module.Analysis.GetOverrideable(module.Tree.IndexToLocation(index)).ToArray();
		}

		public string[] GetDiagnostics()
		{
			return GetDiagnostics(_entries[DefaultModule]);
		}

		public string[] GetDiagnostics(IPythonProjectEntry module)
		{
			return Analyzer.GetDiagnostics(module)
				.Select(d => $"{d.code}:{d.message}:{(SourceSpan)d.range}")
				.ToArray();
		}

		#endregion

		#region Assert Analysis Results

		public void AssertHasAttr(string expr, params string[] attrs)
		{
			AssertHasAttr(_entries[DefaultModule], expr, 0, attrs);
		}

		public void AssertHasAttr(string expr, int index, params string[] attrs)
		{
			AssertHasAttr(_entries[DefaultModule], expr, index, attrs);
		}

		public void AssertHasAttr(IPythonProjectEntry module, string expr, int index, params string[] attrs)
		{
			AssertUtil.ContainsAtLeast(GetMemberNames(module, expr, index), attrs);
		}

		public void AssertNotHasAttr(string expr, params string[] attrs)
		{
			AssertNotHasAttr(_entries[DefaultModule], expr, 0, attrs);
		}

		public void AssertNotHasAttr(string expr, int index, params string[] attrs)
		{
			AssertNotHasAttr(_entries[DefaultModule], expr, index, attrs);
		}

		public void AssertNotHasAttr(IPythonProjectEntry module, string expr, int index, params string[] attrs)
		{
			AssertUtil.DoesntContain(GetMemberNames(module, expr, index), attrs);
		}

		public void AssertHasAttrExact(string expr, params string[] attrs)
		{
			AssertHasAttrExact(_entries[DefaultModule], expr, 0, attrs);
		}

		public void AssertHasAttrExact(string expr, int index, params string[] attrs)
		{
			AssertHasAttrExact(_entries[DefaultModule], expr, index, attrs);
		}

		public void AssertHasAttrExact(IPythonProjectEntry module, string expr, int index, params string[] attrs)
		{
			AssertUtil.ContainsExactly(GetMemberNames(module, expr, index), attrs);
		}

		public void AssertHasParameters(string expr, params string[] paramNames)
		{
			AssertHasParameters(_entries[DefaultModule], expr, 0, paramNames);
		}

		public void AssertHasParameters(string expr, int index, params string[] paramNames)
		{
			AssertHasParameters(_entries[DefaultModule], expr, index, paramNames);
		}

		public void AssertHasParameters(IPythonProjectEntry module, string expr, int index, params string[] paramNames)
		{
			var sigs = module.Analysis.GetSignaturesByIndex(expr, index).ToArray();
			foreach (var s in sigs)
			{
				var parameters = string.Join(", ", s.Parameters.Select(p => $"{p.Name} : {p.Type ?? "(null)"} = {p.DefaultValue ?? "(null)"}"));
				Trace.WriteLine($"{s.Name}({parameters})");
			}
			AssertUtil.AreEqual(sigs.Single().Parameters.Select(p => p.Name), paramNames);
		}

		public void AssertIsInstance(string expr)
		{
			AssertIsInstance(expr, 0);
		}

		public void AssertIsInstance(string expr, int index)
		{
			AssertIsInstance(_entries[DefaultModule], expr, index);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr)
		{
			AssertIsInstance(module, expr, 0);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr, int index)
		{
			AssertIsInstance(module, expr, index, Array.Empty<BuiltinTypeId>());
		}

		public void AssertIsInstance(string expr, params BuiltinTypeId[] types)
		{
			AssertIsInstance(_entries[DefaultModule], expr, 0, types);
		}

		public void AssertIsInstance(string expr, int index, params BuiltinTypeId[] types)
		{
			AssertIsInstance(_entries[DefaultModule], expr, index, types);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr, params BuiltinTypeId[] types)
		{
			AssertIsInstance(module, expr, 0, types);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr, int index, params BuiltinTypeId[] types)
		{
			var fixedTypes = types.Select(t =>
			{
				if (t == BuiltinTypeId.Str)
				{
					return BuiltinTypeId_Str;
				}
				else if (t == BuiltinTypeId.StrIterator)
				{
					return BuiltinTypeId_StrIterator;
				}
				return t;
			}).ToArray();
			AssertUtil.ContainsExactly(GetTypeIds(module, expr, index), fixedTypes);
		}

		public void AssertIsInstance(string expr, params string[] className)
		{
			AssertIsInstance(_entries[DefaultModule], expr, 0, className);
		}

		public void AssertIsInstance(string expr, int index, params string[] className)
		{
			AssertIsInstance(_entries[DefaultModule], expr, index, className);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr, params string[] className)
		{
			AssertIsInstance(module, expr, 0, className);
		}

		public void AssertIsInstance(IPythonProjectEntry module, string expr, int index, params string[] className)
		{
			AnalysisValue[] vars = GetValues(module, expr, index);
			AssertUtil.ContainsAtLeast(vars.Select(v => v.MemberType), PythonMemberType.Instance);
			AssertUtil.ContainsExactly(vars.Select(v => v.ShortDescription), className);
		}
		public void AssertAttrIsType(string variable, string memberName, params PythonMemberType[] types)
		{
			AssertAttrIsType(_entries[DefaultModule], variable, memberName, 0, types);
		}

		public void AssertAttrIsType(string variable, string memberName, int index, params PythonMemberType[] types)
		{
			AssertAttrIsType(_entries[DefaultModule], variable, memberName, index, types);
		}

		public void AssertAttrIsType(IPythonProjectEntry module, string variable, string memberName, params PythonMemberType[] types)
		{
			AssertAttrIsType(module, variable, memberName, 0, types);
		}

		public void AssertAttrIsType(IPythonProjectEntry module, string variable, string memberName, int index, params PythonMemberType[] types)
		{
			AssertUtil.ContainsExactly(GetMember(module, variable, memberName, index).Select(m => m.MemberType), types);
		}

		public void AssertDescription(string expr, string description)
		{
			AssertDescription(_entries[DefaultModule], expr, 0, description);
		}

		public void AssertDescription(IPythonProjectEntry module, string expr, string description)
		{
			AssertDescription(module, expr, 0, description);
		}

		public void AssertDescription(string expr, int index, string description)
		{
			AssertDescription(_entries[DefaultModule], expr, index, description);
		}

		public void AssertDescription(IPythonProjectEntry module, string expr, int index, string description)
		{
			var val = GetValue<AnalysisValue>(module, expr, index);
			if (description != val?.Description && description != val?.ShortDescription)
			{
				Assert.Fail("Expected description of '{0}.{1}' was '{2}'. Actual was '{3}' or '{4}'".FormatInvariant(module.ModuleName, expr, description, val?.ShortDescription ?? "", val?.Description ?? ""));
			}
		}

		public void AssertDescriptionContains(string expr, params string[] description)
		{
			AssertDescriptionContains(_entries[DefaultModule], expr, 0, description);
		}

		public void AssertDescriptionContains(IPythonProjectEntry module, string expr, params string[] description)
		{
			AssertDescriptionContains(module, expr, 0, description);
		}

		public void AssertDescriptionContains(string expr, int index, params string[] description)
		{
			AssertDescriptionContains(_entries[DefaultModule], expr, index, description);
		}

		public void AssertDescriptionContains(IPythonProjectEntry module, string expr, int index, params string[] description)
		{
			var val = GetValue<AnalysisValue>(module, expr, index);
			AssertUtil.Contains(val?.Description, description);
		}

		public void AssertDocumentation(string expr, string documentation)
		{
			AssertDocumentation(_entries[DefaultModule], expr, 0, documentation);
		}

		public void AssertDocumentation(IPythonProjectEntry module, string expr, string documentation)
		{
			AssertDocumentation(module, expr, 0, documentation);
		}

		public void AssertDocumentation(string expr, int index, string documentation)
		{
			AssertDocumentation(_entries[DefaultModule], expr, index, documentation);
		}

		public void AssertDocumentation(IPythonProjectEntry module, string expr, int index, string documentation)
		{
			var val = GetValue<AnalysisValue>(module, expr, index);
			if (documentation != val?.Documentation)
			{
				Assert.Fail("Expected description of '{0}.{1}' was '{2}'. Actual was '{3}' or '{4}'".FormatInvariant(module.ModuleName, expr, documentation, val?.Documentation ?? string.Empty));
			}
		}

		public void AssertConstantEquals(string expr, string value)
		{
			AssertConstantEquals(_entries[DefaultModule], expr, 0, value);
		}

		public void AssertConstantEquals(string expr, int index, string value)
		{
			AssertConstantEquals(_entries[DefaultModule], expr, index, value);
		}

		public void AssertConstantEquals(IPythonProjectEntry module, string expr, string value)
		{
			AssertConstantEquals(module, expr, 0, value);
		}

		public void AssertConstantEquals(IPythonProjectEntry module, string expr, int index, string value)
		{
			var val = GetValue<AnalysisValue>(module, expr, index);
			Assert.AreEqual(value, val?.GetConstantValueAsString(), "{0}.{1}".FormatInvariant(module.ModuleName, expr));
		}

		private static IEnumerable<IAnalysisVariable> UniquifyReferences(IGrouping<ILocationInfo, IAnalysisVariable> source)
		{
			var defn = source.FirstOrDefault(v => v.Type == VariableType.Definition);
			var refr = source.FirstOrDefault(v => v.Type == VariableType.Reference);
			var value = source.FirstOrDefault(v => v.Type == VariableType.Value);
			if (defn != null)
			{
				yield return defn;
			}
			if (refr != null)
			{
				yield return refr;
			}
			if (value != null && defn == null && refr == null)
			{
				yield return value;
			}
		}

		public void AssertReferences(string expr, params VariableLocation[] expectedVars)
		{
			AssertReferences(_entries[DefaultModule], expr, 0, expectedVars);
		}

		public void AssertReferences(string expr, int index, params VariableLocation[] expectedVars)
		{
			AssertReferences(_entries[DefaultModule], expr, index, expectedVars);
		}

		public void AssertReferences(IPythonProjectEntry module, string expr, int index, params VariableLocation[] expectedVars)
		{
			AssertReferencesWorker(module, expr, index, true, expectedVars);
		}

		public void AssertReferencesInclude(string expr, params VariableLocation[] expectedVars)
		{
			AssertReferencesInclude(_entries[DefaultModule], expr, 0, expectedVars);
		}

		public void AssertReferencesInclude(string expr, int index, params VariableLocation[] expectedVars)
		{
			AssertReferencesInclude(_entries[DefaultModule], expr, index, expectedVars);
		}

		public void AssertReferencesInclude(IPythonProjectEntry module, string expr, int index, params VariableLocation[] expectedVars)
		{
			AssertReferencesWorker(module, expr, index, false, expectedVars);
		}

		private sealed class LocationComparer : IEqualityComparer<ILocationInfo>
		{
			public bool Equals(ILocationInfo x, ILocationInfo y)
			{
				return x.StartLine == y.StartLine && x.StartColumn == y.StartColumn;
			}

			public int GetHashCode(ILocationInfo obj)
			{
				return obj.StartLine.GetHashCode() ^ obj.StartColumn.GetHashCode();
			}
		}

		private void AssertReferencesWorker(IPythonProjectEntry module, string expr, int index, bool exact, VariableLocation[] expectedVars)
		{
			var location = module.Tree.IndexToLocation(index);
			var vars = module.Analysis.GetVariables(expr, location)
				.GroupBy(v => v.Location, new LocationComparer())
				.OrderBy(g => g.Key.StartLine)
				.ThenBy(g => g.Key.StartColumn)
				.SelectMany(UniquifyReferences)
				.Select(v => new VariableLocation(v))
				.ToList();

			if (vars.Count == 0)
			{
				Assert.Fail("Got no references to '{0}.{1}'".FormatInvariant(module.ModuleName, expr));
			}

			var expectedNotFound = new List<VariableLocation>();
			var notFoundYet = new HashSet<VariableLocation>(vars);

			foreach (VariableLocation e in expectedVars)
			{
				if (!notFoundYet.Remove(e))
				{
					expectedNotFound.Add(e);
				}
			}
			var foundNotExpected = notFoundYet.OrderBy(v => v.StartLine).ThenBy(v => v.StartCol).ThenBy(v => v.Type).ToList();

			if (!expectedNotFound.Any() && (!exact || !foundNotExpected.Any()))
			{
				return;
			}

			Assert.Fail(
				"References did not match.{0}{0}Actual:{0}{1}{0}{0}Expected but missing:{0}{2}{0}{0}Unexpected:{0}{3}{0}",
				Environment.NewLine,
				string.Join(", " + Environment.NewLine, vars.Select(v => v.ToString())),
				string.Join(", " + Environment.NewLine, expectedNotFound.Select(v => v.ToString())),
				string.Join(", " + Environment.NewLine, foundNotExpected.Select(v => v.ToString()))
			);
		}

		public void AssertDiagnostics(params string[] diagnostics)
		{
			AssertDiagnostics(_entries[DefaultModule], diagnostics);
		}

		public void AssertDiagnostics(IPythonProjectEntry module, params string[] diagnostics)
		{
			global::System.String[] diags = GetDiagnostics(module);
			foreach (global::System.String d in diags)
			{
				Console.WriteLine($"\"{d.Replace("\\", "\\\\")}\",");
			}
			AssertUtil.AreEqual(diags, diagnostics);
		}

		#endregion

		#region Get Expected Result

		private string[] GetMembersOf(BuiltinTypeId typeId)
		{
			if (!_cachedMembers.TryGetValue(typeId, out var members))
			{
				_cachedMembers[typeId] = members = Analyzer.Interpreter.GetBuiltinType(typeId).GetMemberNames(ModuleContext).ToArray();
			}
			return members.ToArray();
		}

		public string[] ObjectMembers => GetMembersOf(BuiltinTypeId.Object);
		public string[] IntMembers => GetMembersOf(BuiltinTypeId.Int);
		public string[] BytesMembers => GetMembersOf(BuiltinTypeId.Bytes);
		public string[] StrMembers => GetMembersOf(BuiltinTypeId_Str);
		public string[] ListMembers => GetMembersOf(BuiltinTypeId.List);
		public string[] FunctionMembers => GetMembersOf(BuiltinTypeId.Function);

		public IPythonType GetBuiltin(string name)
		{
			var builtinName = BuiltinTypeId.Unknown.GetModuleName(_analyzer.LanguageVersion);
			return ((IBuiltinPythonModule)_analyzer.Interpreter.ImportModule(builtinName)).GetAnyMember(name) as IPythonType;
		}

		#endregion
	}

	public class VariableLocation
	{
		public readonly int StartLine;
		public readonly int StartCol;
		public readonly VariableType Type;
		public readonly string FilePath;
		private readonly bool _validFilePath;

		public VariableLocation(int startLine, int startCol, VariableType type)
		{
			StartLine = startLine;
			StartCol = startCol;
			Type = type;
		}

		public VariableLocation(int startLine, int startCol, VariableType type, string filePath)
			: this(startLine, startCol, type)
		{
			FilePath = filePath;
			_validFilePath = true;
		}

		public VariableLocation(IAnalysisVariable variable)
			: this(variable.Location.StartLine, variable.Location.StartColumn, variable.Type, variable.Location.FilePath)
		{
			_validFilePath = false;
		}

		public override string ToString()
		{
			return "new VariableLocation({0}, {1}, VariableType.{2})".FormatInvariant(
				StartLine,
				StartCol,
				Type
			);
		}

		public override bool Equals(object obj)
		{
			VariableLocation other = obj as VariableLocation;
			if (other != null)
			{
				return StartLine == other.StartLine &&
					StartCol == other.StartCol &&
					Type == other.Type &&
					(!_validFilePath || FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase));
			}
			var variable = obj as IAnalysisVariable;
			if (variable != null)
			{
				return StartLine == variable.Location.StartLine &&
					StartCol == variable.Location.StartColumn &&
					Type == variable.Type &&
					(!_validFilePath || FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase));
			}
			return false;
		}

		public override int GetHashCode()
		{
			return StartLine.GetHashCode() ^ StartCol.GetHashCode() ^ Type.GetHashCode();
		}
	}
}
