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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.Python {
    public class PythonAnalysis {
        private readonly PythonAnalyzer _analyzer;
        private readonly Dictionary<string, IPythonProjectEntry> _entries;

        private readonly ConcurrentDictionary<IPythonProjectEntry, TaskCompletionSource<CollectingErrorSink>> _tasks;
        private readonly Dictionary<BuiltinTypeId, string[]> _cachedMembers;

        private readonly string _root;

        public PythonAnalysis(PythonLanguageVersion version)
            : this(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion())) { }

        private static IPythonInterpreterFactory TryFindFactory(string idOrDescription) {
            var provider = new CPythonInterpreterFactoryProvider(watchRegistry: false);
            var factory = provider.GetInterpreterFactory(idOrDescription);
            if (factory == null) {
                var config = provider.GetInterpreterConfigurations().FirstOrDefault(c => idOrDescription.Equals(c.Description, StringComparison.OrdinalIgnoreCase));
                if (config != null) {
                    factory = provider.GetInterpreterFactory(config.Id);
                }
            }
            if (factory == null) {
                Assert.Inconclusive("Requested interpreter '{0}' is not installed", idOrDescription);
                return null;
            }

            return factory;
        }

        public PythonAnalysis(string idOrDescription)
            : this(TryFindFactory(idOrDescription)) { }

        public PythonAnalysis(
            IPythonInterpreterFactory factory,
            IPythonInterpreter interpreter = null,
            string builtinName = null
        ) {
            if (factory == null) {
                Assert.Inconclusive("Expected interpreter is not installed");
            }
            _analyzer = PythonAnalyzer.CreateSynchronously(factory, interpreter, builtinName);
            _entries = new Dictionary<string, IPythonProjectEntry>(StringComparer.OrdinalIgnoreCase);
            _tasks = new ConcurrentDictionary<IPythonProjectEntry, TaskCompletionSource<CollectingErrorSink>>();
            _cachedMembers = new Dictionary<BuiltinTypeId, string[]>();
            _root = TestData.GetTempPath(randomSubPath: true);
        }

        public PythonAnalyzer Analyzer => _analyzer;
        public ModuleAnalysis Result {
            get {
                if (DefaultModule == null) {
                    Assert.Fail("No modules available");
                    return null;
                }

                try {
                    return _entries[DefaultModule].Analysis;
                } catch (KeyNotFoundException) {
                    Assert.Fail("Module " + DefaultModule + " was not found");
                    return null;
                }
            }
        }

        public bool CreateProjectOnDisk { get; set; }
        public string DefaultModule { get; set; }
        public IModuleContext ModuleContext { get; set; }
        public bool AssertOnParseErrors { get; set; }

        public virtual BuiltinTypeId BuiltinTypeId_Str => _analyzer.LanguageVersion.Is3x() ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
        public virtual BuiltinTypeId BuiltinTypeId_StrIterator => _analyzer.LanguageVersion.Is3x() ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;


        private IPythonProjectEntry AddModule(string name, string code) {
            var path = Path.Combine(_root, name.Replace('.', '\\'));
            if (CreateProjectOnDisk) {
                Directory.CreateDirectory(PathUtils.GetParent(path));
                File.WriteAllText(path + ".py", code);
            }

            var entry = _analyzer.AddModule(name, path);
            _entries[name] = entry;
            if (DefaultModule == null) {
                DefaultModule = name;
            }

            return entry;
        }

        public IPythonProjectEntry EnqueueModule(string name, string code) {
            var entry = AddModule(name, code);
            UpdateModule(entry, code);
            return entry;
        }

        public async Task<IPythonProjectEntry> AddModuleAsync(string name, string code, CancellationToken? cancel = null) {
            var entry = AddModule(name, code);

            try {
                await UpdateModuleAsync(entry, code, cancel);
                return entry;
            } catch (OperationCanceledException) {
                _entries.Remove(name);
                throw;
            }
        }

        public void UpdateModule(IPythonProjectEntry entry, string code) {
            CollectingErrorSink errors = null;
            if (code != null) {
                PythonAst ast;
                errors = new CollectingErrorSink();
                using (var p = Parser.CreateParser(
                    new StringReader(code),
                    _analyzer.LanguageVersion,
                    new ParserOptions { BindReferences = true, ErrorSink = errors }
                )) {
                    ast = p.ParseFile();
                    entry.UpdateTree(ast, null);
                }
                if (errors.Errors.Any() || errors.Warnings.Any()) {
                    if (AssertOnParseErrors) {
                        Assert.Fail("Errors parsing " + entry.FilePath, MakeMessage(errors));
                    }
                }
            }

            entry.Analyze(CancellationToken.None, false);
        }
        public Task<CollectingErrorSink> UpdateModuleAsync(IPythonProjectEntry entry, string code, CancellationToken? cancel = null) {
            CollectingErrorSink errors = null;
            if (code != null) {
                PythonAst ast;
                errors = new CollectingErrorSink();
                using (var p = Parser.CreateParser(
                    new StringReader(code),
                    _analyzer.LanguageVersion,
                    new ParserOptions { BindReferences = true, ErrorSink = errors }
                )) {
                    ast = p.ParseFile();
                    entry.UpdateTree(ast, null);
                }
                if (errors.Errors.Any() || errors.Warnings.Any()) {
                    if (AssertOnParseErrors) {
                        Assert.Fail("Errors parsing " + entry.FilePath, MakeMessage(errors));
                    }
                }
            }

            var tcs = new TaskCompletionSource<CollectingErrorSink>(errors);
            _tasks[entry] = tcs;
            var realCancel = cancel ?? CancellationTokens.After5s;
            realCancel.Register(() => tcs.TrySetCanceled());
            entry.Analyze(realCancel, true);

            if (!realCancel.IsCancellationRequested) {
                entry.OnNewAnalysis += Entry_OnNewAnalysis;
            }

            return tcs.Task;
        }

        private static string MakeMessage(CollectingErrorSink errors) {
            var sb = new StringBuilder();
            if (errors.Errors.Any()) {
                sb.AppendLine("Errors:");
                foreach (var e in errors.Errors) {
                    sb.AppendLine("  [{0}] {1}".FormatInvariant(e.Span.ToDebugString(), e.Message));
                }
                sb.AppendLine();
            }
            if (errors.Warnings.Any()) {
                sb.AppendLine("Warnings:");
                foreach (var e in errors.Warnings) {
                    sb.AppendLine("  [{0}] {1}".FormatInvariant(e.Span.ToDebugString(), e.Message));
                }
            }
            return sb.ToString();
        }

        public void SetLimits(AnalysisLimits limits) {
            _analyzer.Limits = limits;
        }

        public void SetSearchPaths(params string[] paths) {
            _analyzer.SetSearchPaths(paths);
        }

        public void ReanalyzeAll(CancellationToken? cancel = null) {
            foreach (var entry in _entries.Values) {
                entry.Analyze(CancellationToken.None, false);
            }
            WaitForAnalysis(cancel);
        }

        public void WaitForAnalysis(CancellationToken? cancel = null) {
            _analyzer.AnalyzeQueuedEntries(cancel ?? CancellationTokens.After5s);
        }

        private void Entry_OnNewAnalysis(object sender, EventArgs e) {
            var entry = sender as IPythonProjectEntry;
            Debug.Assert(entry != null);
            entry.OnNewAnalysis -= Entry_OnNewAnalysis;

            TaskCompletionSource<CollectingErrorSink> task;
            if (_tasks.TryRemove(entry, out task)) {
                task.TrySetResult(task.Task.AsyncState as CollectingErrorSink);
            } else {
                Debug.Fail("Unexpected completion event");
            }
        }

        #region Get Analysis Results

        public IEnumerable<string> GetMemberNames(string exprText, int index = 0, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return GetMemberNames(_entries[DefaultModule], exprText, index, options);
        }

        public IEnumerable<string> GetMemberNames(IPythonProjectEntry module, string exprText, int index = 0, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return module.Analysis.GetMembersByIndex(exprText, index, options).Select(m => m.Name);
        }

        public IEnumerable<IPythonType> GetTypes(string exprText, int index = 0) {
            return GetTypes(_entries[DefaultModule], exprText, index);
        }

        public IEnumerable<IPythonType> GetTypes(IPythonProjectEntry module, string exprText, int index = 0) {
            return module.Analysis.GetValuesByIndex(exprText, index).Select(m => m.PythonType);
        }

        public IEnumerable<BuiltinTypeId> GetTypeIds(string exprText, int index = 0) {
            return GetTypeIds(_entries[DefaultModule], exprText, index);
        }

        public IEnumerable<BuiltinTypeId> GetTypeIds(IPythonProjectEntry module, string exprText, int index = 0) {
            return module.Analysis.GetValuesByIndex(exprText, index).Select(m => {
                if (m.PythonType.TypeId != BuiltinTypeId.Unknown) {
                    return m.PythonType.TypeId;
                }

                var state = _analyzer;
                if (m == state._noneInst) {
                    return BuiltinTypeId.NoneType;
                }

                var bci = m as BuiltinClassInfo;
                if (bci == null) {
                    var bii = m as BuiltinInstanceInfo;
                    if (bii != null) {
                        bci = bii.ClassInfo;
                    }
                }
                if (bci != null) {
                    int count = (int)BuiltinTypeIdExtensions.LastTypeId;
                    for (int i = 1; i <= count; ++i) {
                        var bti = (BuiltinTypeId)i;
                        if (!bti.IsVirtualId() && _analyzer.ClassInfos[bti] == bci) {
                            return bti;
                        }
                    }
                }

                return BuiltinTypeId.Unknown;
            });
        }

        public IEnumerable<string> GetDescriptions(string variable, int index = 0) {
            return GetDescriptions(_entries[DefaultModule], variable, index);
        }

        public IEnumerable<string> GetDescriptions(IPythonProjectEntry module, string variable, int index = 0) {
            return module.Analysis.GetValuesByIndex(variable, index).Select(m => m.Description);
        }

        public IEnumerable<string> GetShortDescriptions(string variable, int index = 0) {
            return GetShortDescriptions(_entries[DefaultModule], variable, index);
        }

        public IEnumerable<string> GetShortDescriptions(IPythonProjectEntry module, string variable, int index = 0) {
            return module.Analysis.GetValuesByIndex(variable, index).Select(m => m.ShortDescription);
        }

        public IEnumerable<string> GetCompletionDocumentation(string variable, string memberName, int index = 0) {
            return GetCompletionDocumentation(_entries[DefaultModule], variable, memberName, index);
        }

        public IEnumerable<string> GetCompletionDocumentation(IPythonProjectEntry module, string variable, string memberName, int index = 0) {
            return GetMember(module, variable, memberName, index).Select(m => m.Documentation);
        }

        public IEnumerable<MemberResult> GetMember(string variable, string memberName, int index = 0) {
            return GetMember(_entries[DefaultModule], variable, memberName, index);
        }

        public IEnumerable<MemberResult> GetMember(IPythonProjectEntry module, string variable, string memberName, int index = 0) {
            return module.Analysis.GetMembersByIndex(variable, index).Where(m => m.Name == memberName);
        }

        public IEnumerable<string> GetAllNames(int index = 0) {
            return GetAllNames(_entries[DefaultModule], index);
        }

        public IEnumerable<string> GetAllNames(IPythonProjectEntry module, int index = 0) {
            return module.Analysis.GetAllAvailableMembers(SourceLocation.None).Select(m => m.Name);
        }

        public T GetValue<T>(string variable, int index = 0) where T : AnalysisValue {
            return GetValue<T>(_entries[DefaultModule], variable, index);
        }

        public T GetValue<T>(IPythonProjectEntry module, string variable, int index = 0) where T : AnalysisValue {
            var r = module.Analysis.GetVariablesByIndex(variable, index).SingleOrDefault();
            Assert.IsNotNull(r, "'{0}.{1}' had no variables".FormatInvariant(module.ModuleName, variable));
            Assert.IsInstanceOfType(r, typeof(T), "'{0}.{1}' was not expected type".FormatInvariant(module.ModuleName, variable));
            return (T)r;
        }

        public IOverloadResult[] GetSignatures(string exprText, int index = 0) {
            return GetSignatures(_entries[DefaultModule], exprText, index);
        }

        public IOverloadResult[] GetSignatures(IPythonProjectEntry module, string exprText, int index = 0) {
            return module.Analysis.GetSignaturesByIndex(exprText, index).ToArray();
        }

        #endregion

        #region Assert Analysis Results

        public void AssertHasAttr(string expr, params string[] attrs) {
            AssertHasAttr(_entries[DefaultModule], expr, 0, attrs);
        }

        public void AssertHasAttr(string expr, int index, params string[] attrs) {
            AssertHasAttr(_entries[DefaultModule], expr, index, attrs);
        }

        public void AssertHasAttr(IPythonProjectEntry module, string expr, int index, params string[] attrs) {
            AssertUtil.ContainsAtLeast(GetMemberNames(module, expr, index), attrs);
        }

        public void AssertHasAttrExact(string expr, params string[] attrs) {
            AssertHasAttrExact(_entries[DefaultModule], expr, 0, attrs);
        }

        public void AssertHasAttrExact(string expr, int index, params string[] attrs) {
            AssertHasAttrExact(_entries[DefaultModule], expr, index, attrs);
        }

        public void AssertHasAttrExact(IPythonProjectEntry module, string expr, int index, params string[] attrs) {
            AssertUtil.ContainsExactly(GetMemberNames(module, expr, index), attrs);
        }

        public void AssertHasParameters(string expr, params string[] paramNames) {
            AssertHasParameters(_entries[DefaultModule], expr, 0, paramNames);
        }

        public void AssertHasParameters(string expr, int index, params string[] paramNames) {
            AssertHasParameters(_entries[DefaultModule], expr, index, paramNames);
        }

        public void AssertHasParameters(IPythonProjectEntry module, string expr, int index, params string[] paramNames) {
            AssertUtil.AreEqual(module.Analysis.GetSignaturesByIndex(expr, index).Single().Parameters.Select(p => p.Name), paramNames);
        }

        public void AssertIsInstance(string expr, params BuiltinTypeId[] types) {
            AssertIsInstance(_entries[DefaultModule], expr, 0, types);
        }

        public void AssertIsInstance(IPythonProjectEntry module, string expr, params BuiltinTypeId[] types) {
            AssertIsInstance(module, expr, 0, types);
        }

        public void AssertIsInstance(IPythonProjectEntry module, string expr, int index, params BuiltinTypeId[] types) {
            AssertUtil.ContainsExactly(GetTypeIds(module, expr, index), types);
        }

        private static IEnumerable<IAnalysisVariable> UniquifyReferences(IGrouping<LocationInfo, IAnalysisVariable> source) {
            var res = source.OrderBy(v => v.Type).Where(v => v.Type != VariableType.Value);
            var value = source.FirstOrDefault(v => v.Type == VariableType.Value);
            if (value != null) {
                res = res.DefaultIfEmpty(value);
            }
            return res;
        }

        public void AssertReferences(string expr, params VariableLocation[] expectedVars) {
            AssertReferences(_entries[DefaultModule], expr, 0, expectedVars);
        }

        public void AssertReferences(string expr, int index, params VariableLocation[] expectedVars) {
            AssertReferences(_entries[DefaultModule], expr, index, expectedVars);
        }

        public void AssertReferences(IPythonProjectEntry module, string expr, int index, params VariableLocation[] expectedVars) {
            var vars = module.Analysis.GetVariablesByIndex(expr, index)
                .GroupBy(v => v.Location)
                .OrderBy(g => g.Key.StartLine)
                .ThenBy(g => g.Key.StartColumn)
                .SelectMany(UniquifyReferences)
                .Select(v => new VariableLocation(v))
                .ToList();

            if (vars.Count == 0) {
                Assert.Fail("Got no references to '{0}.{1}'".FormatInvariant(module.ModuleName, expr));
            }

            var expectedNotFound = new List<VariableLocation>();
            var notFoundYet = new HashSet<VariableLocation>(vars);

            foreach (var e in expectedVars) {
                if (!notFoundYet.Remove(e)) {
                    expectedNotFound.Add(e);
                }
            }
            var foundNotExpected = notFoundYet.OrderBy(v => v.StartLine).ThenBy(v => v.StartCol).ThenBy(v => v.Type).ToList();

            if (!expectedNotFound.Any() && !foundNotExpected.Any()) {
                return;
            }

            Assert.Fail(
                "References did not match.{0}{0}Actual:{0}{1}{0}{0}Expected but missing:{0}{2}{0}{0}Unexpected:{0}{3}{0}",
                string.Join(", " + Environment.NewLine, vars.Select(v => v.ToString())),
                string.Join(", " + Environment.NewLine, expectedNotFound.Select(v => v.ToString())),
                string.Join(", " + Environment.NewLine, foundNotExpected.Select(v => v.ToString()))
            );
        }

        #endregion

        #region Get Expected Result

        private string[] GetMembersOf(BuiltinTypeId typeId) {
            string[] members;
            if (!_cachedMembers.TryGetValue(typeId, out members)) {
                _cachedMembers[typeId] = members = Analyzer.Types[typeId].GetMemberNames(ModuleContext).ToArray();
            }
            return members.ToArray();
        }

        public string[] ObjectMembers => GetMembersOf(BuiltinTypeId.Object);
        public string[] IntMembers => GetMembersOf(BuiltinTypeId.Int);
        public string[] BytesMembers => GetMembersOf(BuiltinTypeId.Bytes);
        public string[] ListMembers => GetMembersOf(BuiltinTypeId.List);
        public string[] FunctionMembers => GetMembersOf(BuiltinTypeId.Function);

        public IPythonType GetBuiltin(string name) {
            return ((IBuiltinPythonModule)_analyzer.BuiltinModule.InterpreterModule).GetAnyMember(name) as IPythonType;
        }

        #endregion
    }

    public class VariableLocation {
        public readonly int StartLine;
        public readonly int StartCol;
        public readonly VariableType Type;
        public readonly string FilePath;
        private readonly bool _validFilePath;

        public VariableLocation(int startLine, int startCol, VariableType type) {
            StartLine = startLine;
            StartCol = startCol;
            Type = type;
        }

        public VariableLocation(int startLine, int startCol, VariableType type, string filePath)
            : this(startLine, startCol, type) {
            FilePath = filePath;
            _validFilePath = true;
        }

        public VariableLocation(IAnalysisVariable variable)
            : this(variable.Location.StartLine, variable.Location.StartColumn, variable.Type, variable.Location.FilePath) {
            _validFilePath = false;
        }

        public override string ToString() {
            return "new VariableLocation({0}, {1}, VariableType.{2})".FormatInvariant(
                StartLine,
                StartCol,
                Type
            );
        }

        public override bool Equals(object obj) {
            var other = obj as VariableLocation;
            if (other != null) {
                return StartLine == other.StartLine &&
                    StartCol == other.StartCol &&
                    Type == other.Type &&
                    (!_validFilePath || FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase));
            }
            var variable = obj as IAnalysisVariable;
            if (variable != null) {
                return StartLine == variable.Location.StartLine &&
                    StartCol == variable.Location.StartColumn &&
                    Type == variable.Type &&
                    (!_validFilePath || FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        public override int GetHashCode() {
            return StartLine.GetHashCode() ^ StartCol.GetHashCode() ^ Type.GetHashCode();
        }
    }
}
