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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Encapsulates all of the information about a single module which has been analyzed.  
    /// 
    /// Can be queried for various information about the resulting analysis.
    /// </summary>
    public sealed class ModuleAnalysis {
        private readonly AnalysisUnit _unit;
        private readonly InterpreterScope _scope;
        private static Regex _otherPrivateRegex = new Regex("^_[a-zA-Z_]\\w*__[a-zA-Z_]\\w*$");

        private static readonly IEnumerable<IOverloadResult> GetSignaturesError =
            new[] { new SimpleOverloadResult(new ParameterResult[0], "Unknown", "IntellisenseError_Sigs") };

        internal ModuleAnalysis(AnalysisUnit unit, InterpreterScope scope) {
            _unit = unit;
            _scope = scope;
        }

        #region Public API

        /// <summary>
        /// Evaluates the given expression in at the provided line number and returns the values
        /// that the expression can evaluate to.
        /// </summary>
        /// <param name="exprText">The expression to determine the result of.</param>
        /// <param name="index">The 0-based absolute index into the file where the expression should be evaluated.</param>
        public IEnumerable<AnalysisValue> GetValuesByIndex(string exprText, int index) {
            return GetValues(exprText, _unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Evaluates the given expression in at the provided line number and returns the values
        /// that the expression can evaluate to.
        /// </summary>
        /// <param name="exprText">The expression to determine the result of.</param>
        /// <param name="location">The location in the file where the expression should be evaluated.</param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<AnalysisValue> GetValues(string exprText, SourceLocation location) {
            var scope = FindScope(location);
            var privatePrefix = GetPrivatePrefixClassName(scope);
            var expr = Statement.GetExpression(GetAstFromText(exprText, privatePrefix).Body);

            var unit = GetNearestEnclosingAnalysisUnit(scope);
            var eval = new ExpressionEvaluator(unit.CopyForEval(), scope, mergeScopes: true);

            var values = eval.Evaluate(expr);
            var res = AnalysisSet.EmptyUnion;
            foreach (var v in values) {
                MultipleMemberInfo multipleMembers = v as MultipleMemberInfo;
                if (multipleMembers != null) {
                    foreach (var member in multipleMembers.Members) {
                        if (member.IsAlive) {
                            res = res.Add(member);
                        }
                    }
                } else if (v.IsAlive) {
                    res = res.Add(v);
                }
            }
            return res;
        }

        internal IEnumerable<AnalysisVariable> ReferencablesToVariables(IEnumerable<IReferenceable> defs) {
            foreach (var def in defs) {
                foreach (var res in ToVariables(def)) {
                    yield return res;
                }
            }
        }

        internal IEnumerable<AnalysisVariable> ToVariables(IReferenceable referenceable) {
            LocatedVariableDef locatedDef = referenceable as LocatedVariableDef;

            if (locatedDef != null &&
                locatedDef.Entry.Tree != null &&    // null tree if there are errors in the file
                locatedDef.DeclaringVersion == locatedDef.Entry.AnalysisVersion) {
                var identifierStart = locatedDef.Node.GetStart(locatedDef.Entry.Tree);
                var identifierEnd = locatedDef.Node.GetEnd(locatedDef.Entry.Tree);

                // For classes and functions, find the ClassDefinition or
                // FunctionDefinition to get the full span of the definition.
                var walker = new DefinitionWalker(locatedDef.Node.StartIndex);
                locatedDef.Entry.Tree.Walk(walker);

                SourceLocation definitionStart;
                SourceLocation definitionEnd;
                if (walker.Definition != null) {
                    definitionStart = walker.Definition.GetStart(locatedDef.Entry.Tree);
                    definitionEnd = walker.Definition.GetEnd(locatedDef.Entry.Tree);
                } else {
                    definitionStart = identifierStart;
                    definitionEnd = locatedDef.Node.GetEnd(locatedDef.Entry.Tree);
                }

                yield return new AnalysisVariable(
                    VariableType.Definition,
                    // Location of the identifier only
                    new LocationInfo(
                        locatedDef.Entry.FilePath,
                        locatedDef.Entry.DocumentUri,
                        identifierStart.Line,
                        identifierStart.Column,
                        identifierEnd.Line,
                        identifierEnd.Column
                    ),
                    // Location of the full definition
                    new LocationInfo(
                        locatedDef.Entry.FilePath,
                        locatedDef.Entry.DocumentUri,
                        definitionStart.Line,
                        definitionStart.Column,
                        definitionEnd.Line,
                        definitionEnd.Column
                    )
                );
            }

            VariableDef def = referenceable as VariableDef;
            if (def != null) {
                foreach (var loc in def.TypesNoCopy.SelectMany(type => type.Locations)) {
                    if (loc != null) {
                        yield return new AnalysisVariable(VariableType.Value, loc);
                    }
                }
            }

            foreach (var reference in referenceable.Definitions) {
                var loc = reference.GetLocationInfo();
                if (loc != null) {
                    yield return new AnalysisVariable(VariableType.Definition, loc);
                }
            }

            foreach (var reference in referenceable.References) {
                var loc = reference.GetLocationInfo();
                if (loc != null) {
                    yield return new AnalysisVariable(VariableType.Reference, loc);
                }
            }
        }

        private class DefinitionWalker : PythonWalkerWithLocation {
            public ScopeStatement Definition;

            public DefinitionWalker(int location) : base(location) {
            }

            public override void PostWalk(FunctionDefinition node) {
                if (Location >= node.NameExpression.StartIndex && Location <= node.NameExpression.EndIndex) {
                    Definition = node;
                }
                base.PostWalk(node);
            }

            public override void PostWalk(ClassDefinition node) {
                if (Location >= node.NameExpression.StartIndex && Location <= node.NameExpression.EndIndex) {
                    Definition = node;
                }
                base.PostWalk(node);
            }
        }

        /// <summary>
        /// Gets the variables the given expression evaluates to.  Variables
        /// include parameters, locals, and fields assigned on classes, modules
        /// and instances.
        /// 
        /// Variables are classified as either definitions or references.  Only
        /// parameters have unique definition points - all other types of
        /// variables have only one or more references.
        /// </summary>
        /// <param name="exprText">The expression to find variables for.</param>
        /// <param name="index">
        /// The 0-based absolute index into the file where the expression should
        /// be evaluated.
        /// </param>
        public VariablesResult GetVariablesByIndex(string exprText, int index) {
            return GetVariables(exprText, _unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets the variables the given expression evaluates to.  Variables
        /// include parameters, locals, and fields assigned on classes, modules
        /// and instances.
        /// 
        /// Variables are classified as either definitions or references.  Only
        /// parameters have unique definition points - all other types of
        /// variables have only one or more references.
        /// </summary>
        /// <param name="exprText">The expression to find variables for.</param>
        /// <param name="location">
        /// The location in the file where the expression should be evaluated.
        /// </param>
        /// <remarks>New in 2.2</remarks>
        public VariablesResult GetVariables(string exprText, SourceLocation location) {
            var scope = FindScope(location);
            string privatePrefix = GetPrivatePrefixClassName(scope);
            var ast = GetAstFromText(exprText, privatePrefix);
            var expr = Statement.GetExpression(ast.Body);

            if (expr == null) {
                return new VariablesResult(Enumerable.Empty<IAnalysisVariable>(), ast);
            }

            return GetVariables(expr, location, exprText);
        }

        internal VariablesResult GetVariables(Expression expr, SourceLocation location, string originalText = null) {
            var scope = FindScope(location);
            var unit = GetNearestEnclosingAnalysisUnit(scope);
            var tree = unit.Tree;

            var eval = new ExpressionEvaluator(unit.CopyForEval(), scope, mergeScopes: true);
            var variables = Enumerable.Empty<IAnalysisVariable>();

            var finder = new ExpressionFinder(unit.Tree, new GetExpressionOptions { Calls = true });
            var callNode = finder.GetExpression(location) as CallExpression;
            if (callNode != null) {
                finder = new ExpressionFinder(unit.Tree, new GetExpressionOptions { NamedArgumentNames = true });
                var argNode = finder.GetExpression(location) as NameExpression;
                if (argNode != null && (string.IsNullOrEmpty(originalText) || argNode.Name == originalText)) {
                    var objects = eval.Evaluate(callNode.Target);

                    return new VariablesResult(objects
                        .Where(v => v.Overloads.MaybeEnumerate().Any(o => o.Parameters.MaybeEnumerate().Any(p => p.Name == argNode.Name)))
                        .Select(v => (v as BoundMethodInfo)?.Function ?? v as FunctionInfo)
                        .Select(f => f?.AnalysisUnit?.Scope)
                        .Where(s => s != null)
                        .SelectMany(s => GetVariablesInScope(argNode, s).Distinct()),
                        unit.Tree);
                }
            }

            if (expr is NameExpression name) {
                var defScope = scope.EnumerateTowardsGlobal.FirstOrDefault(s =>
                    s.ContainsVariable(name.Name) && (s == scope || s.VisibleToChildren || IsFirstLineOfFunction(scope, s, location)));

                if (defScope == null) {
                    variables = _unit.ProjectState.BuiltinModule.GetDefinitions(name.Name)
                        .SelectMany(ToVariables);
                } else {
                    variables = GetVariablesInScope(name, defScope).Distinct();
                }
            } else if (expr is MemberExpression member && !string.IsNullOrEmpty(member.Name)) {
                var objects = eval.Evaluate(member.Target);

                foreach (var v in objects.OfType<IReferenceableContainer>()) {
                    variables = variables.Union(v.GetDefinitions(member.Name).SelectMany(ToVariables));
                }
            }

            return new VariablesResult(variables, unit.Tree);
        }

        private IEnumerable<IAnalysisVariable> GetVariablesInScope(NameExpression name, InterpreterScope scope) {
            var result = new List<IAnalysisVariable>();

            result.AddRange(scope.GetMergedVariables(name.Name).SelectMany(ToVariables));

            // if a variable is imported from another module then also yield the defs/refs for the 
            // value in the defining module.
            result.AddRange(scope.GetLinkedVariables(name.Name).SelectMany(ToVariables));

            var classScope = scope as ClassScope;
            if (classScope != null) {
                // if the member is defined in a base class as well include the base class member and references
                var cls = classScope.Class;
                if (cls.Push()) {
                    try {
                        foreach (var baseNs in cls.Bases.SelectMany()) {
                            if (baseNs.Push()) {
                                try {
                                    ClassInfo baseClassNs = baseNs as ClassInfo;
                                    if (baseClassNs != null) {
                                        result.AddRange(
                                            baseClassNs.Scope.GetMergedVariables(name.Name).SelectMany(ToVariables)
                                        );
                                    }
                                } finally {
                                    baseNs.Pop();
                                }
                            }
                        }
                    } finally {
                        cls.Pop();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the list of modules known by the current analysis.
        /// </summary>
        /// <param name="topLevelOnly">Only return top-level modules.</param>
        [Obsolete]
        public MemberResult[] GetModules(bool topLevelOnly = false) {
            List<MemberResult> res = new List<MemberResult>(ProjectState.GetModules());

            var children = GlobalScope.GetChildrenPackages(InterpreterContext);

            foreach (var child in children) {
                res.Add(new MemberResult(child.Key, PythonMemberType.Module));
            }

            return res.ToArray();
        }

        /// <summary>
        /// Gets the list of modules and members matching the provided names.
        /// </summary>
        /// <param name="names">The dotted name parts to match</param>
        /// <param name="includeMembers">Include module members that match as
        /// well as just modules.</param>
        [Obsolete]
        public MemberResult[] GetModuleMembers(string[] names, bool includeMembers = false) {
            var res = new List<MemberResult>(ProjectState.GetModuleMembers(InterpreterContext, names, includeMembers));
            var children = GlobalScope.GetChildrenPackages(InterpreterContext);

            foreach (var child in children) {
                var mod = (ModuleInfo)child.Value;

                if (string.IsNullOrEmpty(mod.Name)) {
                    // Module does not have an importable name
                    continue;
                }

                var childName = mod.Name.Split('.');
                if (childName.Length >= 2 && childName[0] == GlobalScope.Name && childName[1] == names[0]) {
                    res.AddRange(PythonAnalyzer.GetModuleMembers(InterpreterContext, names, includeMembers, mod as IModule));
                }
            }

            return res.ToArray();
        }

        private static bool IsFirstLineOfFunction(InterpreterScope innerScope, InterpreterScope outerScope, SourceLocation location) {
            if (innerScope.OuterScope == outerScope && innerScope is FunctionScope) {
                var funcScope = (FunctionScope)innerScope;
                var def = funcScope.Function.FunctionDefinition;

                // TODO: Use indexes rather than lines to check location
                if (location.Line == def.GetStart(def.GlobalParent).Line) {
                    return true;
                }
            }
            return false;
        }

        private class ErrorWalker : PythonWalker {
            public bool HasError { get; private set; }

            public override bool Walk(ErrorStatement node) {
                HasError = true;
                return false;
            }

            public override bool Walk(ErrorExpression node) {
                HasError = true;
                return false;
            }
        }

        /// <summary>
        /// Evaluates a given expression and returns a list of members which
        /// exist in the expression.
        /// 
        /// If the expression is an empty string returns all available members
        /// at that location.
        /// </summary>
        /// <param name="exprText">The expression to find members for.</param>
        /// <param name="index">
        /// The 0-based absolute index into the file where the expression should
        /// be evaluated.
        /// </param>
        public IEnumerable<MemberResult> GetMembersByIndex(
            string exprText,
            int index,
            GetMemberOptions options = GetMemberOptions.IntersectMultipleResults
        ) {
            return GetMembers(exprText, _unit.Tree.IndexToLocation(index), options);
        }

        /// <summary>
        /// Evaluates a given expression and returns a list of members which
        /// exist in the expression.
        /// 
        /// If the expression is an empty string returns all available members
        /// at that location.
        /// </summary>
        /// <param name="exprText">The expression to find members for.</param>
        /// </param>
        /// <param name="location">
        /// The location in the file where the expression should be evaluated.
        /// </param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<MemberResult> GetMembers(
            string exprText,
            SourceLocation location,
            GetMemberOptions options = GetMemberOptions.IntersectMultipleResults
        ) {
            if (exprText.Length == 0) {
                return GetAllAvailableMembers(location, options);
            }

            var scope = FindScope(location);
            var privatePrefix = GetPrivatePrefixClassName(scope);

            var ast = GetAstFromText(exprText, privatePrefix).Body;

            var expr = Statement.GetExpression(ast);
            return GetMembers(expr, location, options, scope);
        }

        internal IEnumerable<MemberResult> GetMembers(
            Expression expr,
            SourceLocation location,
            GetMemberOptions options = GetMemberOptions.IntersectMultipleResults,
            InterpreterScope scope = null
        ) {
            if (expr is ConstantExpression && ((ConstantExpression)expr).Value is int) {
                // no completions on integer ., the user is typing a float
                return Enumerable.Empty<MemberResult>();
            }

            if (expr == null) {
                return Enumerable.Empty<MemberResult>();
            }

            var lookup = AnalysisSet.Create();
            var errorWalker = new ErrorWalker();
            expr.Walk(errorWalker);
            if (errorWalker.HasError) {
                return null;
            }

            // Special handling for `__import__('module.name')`
            if (expr is CallExpression ce &&
                ce.Target is NameExpression ne &&
                ne.Name == "__import__" &&
                ce.Args?.Count == 1 &&
                ce.Args[0].Expression is ConstantExpression modNameExpr
            ) {
                string modName = modNameExpr.Value as string ?? (modNameExpr.Value as AsciiString)?.String;
                if (!string.IsNullOrEmpty(modName)) {
                    lookup = ResolveModule(expr, _unit.CopyForEval(), modName);
                }
            }

            if (scope == null) {
                scope = FindScope(location);
            }

            if (!lookup.Any()) {
                var unit = GetNearestEnclosingAnalysisUnit(scope);
                var eval = new ExpressionEvaluator(unit.CopyForEval(), scope, mergeScopes: true);
                if (options.HasFlag(GetMemberOptions.NoMemberRecursion)) {
                    lookup = eval.EvaluateNoMemberRecursion(expr);
                } else {
                    lookup = eval.Evaluate(expr);
                }
            }

            return GetMemberResults(lookup, scope, options);
        }

        private static IAnalysisSet ResolveModule(Node node, AnalysisUnit unit, string moduleName) {
            ModuleReference modRef;
            var modules = unit.ProjectState.Modules;

            if (modules.TryImport(moduleName, out modRef)) {
                return modRef.AnalysisModule ?? AnalysisSet.Empty;
            }

            var attrs = new List<string>();

            var modName = moduleName;
            int i;
            while ((i = modName.LastIndexOf('.')) > 0) {
                attrs.Add(modName.Substring(i + 1));
                modName = modName.Remove(i);

                if (modules.TryImport(modName, out modRef)) {
                    break;
                }
            }

            IAnalysisSet mod = modRef?.AnalysisModule;
            foreach (var attr in attrs.Reverse<string>()) {
                if (mod == null || !mod.Any()) {
                    return AnalysisSet.Empty;
                }

                mod = AnalysisSet.Create(mod.Where(m => m.MemberType == PythonMemberType.Module || m.MemberType == PythonMemberType.Multiple))
                    .GetMember(node, unit, attr);
            }

            return mod ?? AnalysisSet.Empty;
        }

        /// <summary>
        /// Gets information about the available signatures for the given expression.
        /// </summary>
        /// <param name="exprText">The expression to get signatures for.</param>
        /// <param name="index">The 0-based absolute index into the file.</param>
        public IEnumerable<IOverloadResult> GetSignaturesByIndex(string exprText, int index) {
            return GetSignatures(exprText, _unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets information about the available signatures for the given expression.
        /// </summary>
        /// <param name="exprText">The expression to get signatures for.</param>
        /// <param name="location">The location in the file.</param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<IOverloadResult> GetSignatures(string exprText, SourceLocation location) {
            try {
                var parser = Parser.CreateParser(new StringReader(exprText), _unit.ProjectState.LanguageVersion);
                var expr = GetExpression(parser.ParseTopExpression().Body);
                if (expr is ListExpression ||
                    expr is TupleExpression ||
                    expr is DictionaryExpression) {
                    return Enumerable.Empty<IOverloadResult>();
                }

                return GetSignatures(expr, location);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Debug.Fail(ex.ToString());
                return GetSignaturesError;
            }
        }

        internal IEnumerable<IOverloadResult> GetSignatures(Expression expr, SourceLocation location) {
            var scope = FindScope(location);
            var unit = GetNearestEnclosingAnalysisUnit(scope);
            var eval = new ExpressionEvaluator(unit.CopyForEval(), scope, mergeScopes: true);

            var lookup = eval.Evaluate(expr);

            lookup = AnalysisSet.Create(lookup.Where(av => !(av is MultipleMemberInfo)).Concat(
                lookup.OfType<MultipleMemberInfo>().SelectMany(mmi => mmi.Members)
            ));

            var result = new HashSet<OverloadResult>(OverloadResultComparer.Instance);

            result.UnionWith(lookup
                // Exclude constant values first time through
                .Where(av => av.MemberType != PythonMemberType.Constant)
                .SelectMany(av => av.Overloads ?? Enumerable.Empty<OverloadResult>())
            );

            if (!result.Any()) {
                result.UnionWith(lookup
                    .Where(av => av.MemberType == PythonMemberType.Constant)
                    .SelectMany(av => av.Overloads ?? Enumerable.Empty<OverloadResult>()));
            }

            // Take nearly-equivalent overloads and merge them, retaining the maximum
            // possible information.
            return result.GroupBy(o => o, OverloadResultComparer.WeakInstance).Select(OverloadResult.Merge);
        }

        /// <summary>
        /// Gets the hierarchy of class and function definitions at the
        /// specified location.
        /// </summary>
        /// <param name="index">The 0-based absolute index into the file.</param>
        public IEnumerable<MemberResult> GetDefinitionTreeByIndex(int index) {
            return GetDefinitionTree(_unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets the hierarchy of class and function definitions at the
        /// specified location.
        /// </summary>
        /// <param name="location">The location in the file.</param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<MemberResult> GetDefinitionTree(SourceLocation location) {
            try {
                return FindScope(location).EnumerateTowardsGlobal
                    .Select(s => new MemberResult(s.Name, s.GetMergedAnalysisValues()))
                    .ToList();
            } catch (Exception) {
                // TODO: log exception
                Debug.Fail("Failed to find scope. Bad state in analysis");
                return new[] { new MemberResult("Unknown", new AnalysisValue[] { }) };
            }
        }

        /// <summary>
        /// Gets information about methods defined on base classes but not
        /// directly on the current class.
        /// </summary>
        /// <param name="index">The 0-based absolute index into the file.</param>
        public IEnumerable<IOverloadResult> GetOverrideableByIndex(int index) {
            return GetOverrideable(_unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets information about methods defined on base classes but not
        /// directly on the current class.
        /// </summary>
        /// <param name="location">The location in the file.</param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<IOverloadResult> GetOverrideable(SourceLocation location) {
            try {
                var result = new List<IOverloadResult>();

                var scope = FindScope(location);
                var cls = scope as ClassScope;
                if (cls == null) {
                    return result;
                }
                var handled = new HashSet<string>(cls.Children.Select(child => child.Name));

                var cls2 = scope as ClassScope;
                var mro = (cls2 ?? cls).Class.Mro;
                if (mro == null) {
                    return result;
                }

                foreach (var baseClass in mro.Skip(1).SelectMany()) {
                    ClassInfo klass;
                    BuiltinClassInfo builtinClass;
                    IEnumerable<AnalysisValue> source;

                    if ((klass = baseClass as ClassInfo) != null) {
                        source = klass.Scope.Children
                            .Where(child => child != null && child.AnalysisValue != null)
                            .Select(child => child.AnalysisValue);
                    } else if ((builtinClass = baseClass as BuiltinClassInfo) != null) {
                        source = builtinClass.GetAllMembers(InterpreterContext)
                            .SelectMany(kv => kv.Value)
                            .Where(child => child != null && 
                                (child.MemberType == PythonMemberType.Function ||
                                 child.MemberType == PythonMemberType.Method));
                    } else {
                        continue;
                    }

                    foreach (var child in source) {
                        if (!child.Overloads.Any()) {
                            continue;
                        }

                        try {
                            var overload = child.Overloads.Aggregate(
                                (best, o) => o.Parameters.Length > best.Parameters.Length ? o : best
                            );

                            if (handled.Contains(overload.Name)) {
                                continue;
                            }

                            handled.Add(overload.Name);
                            result.Add(overload);
                        } catch {
                            // TODO: log exception
                            // Exceptions only affect the current override. Others may still be offerred.
                        }
                    }
                }

                return result;
            } catch (Exception) {
                // TODO: log exception
                return new IOverloadResult[0];
            }
        }

        /// <summary>
        /// Gets the available names at the given location.  This includes
        /// built-in variables, global variables, and locals.
        /// </summary>
        /// <param name="index">
        /// The 0-based absolute index into the file where the available members
        /// should be looked up.
        /// </param>
        public IEnumerable<MemberResult> GetAllAvailableMembersByIndex(
            int index,
            GetMemberOptions options = GetMemberOptions.IntersectMultipleResults
        ) {
            return GetAllAvailableMembers(_unit.Tree.IndexToLocation(index), options);
        }

        /// <summary>
        /// Gets the available names at the given location.  This includes
        /// built-in variables, global variables, and locals.
        /// </summary>
        /// <param name="location">
        /// The location in the file where the available members should be
        /// looked up.
        /// </param>
        /// <remarks>New in 2.2</remarks>
        public IEnumerable<MemberResult> GetAllAvailableMembers(SourceLocation location, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            var result = new Dictionary<string, IEnumerable<AnalysisValue>>();

            // collect builtins
            if (!options.HasFlag(GetMemberOptions.ExcludeBuiltins)) {
                foreach (var variable in ProjectState.BuiltinModule.GetAllMembers(ProjectState._defaultContext)) {
                    result[variable.Key] = new List<AnalysisValue>(variable.Value);
                }
            }

            // collect variables from user defined scopes
            var scope = FindScope(location);
            foreach (var s in scope.EnumerateTowardsGlobal) {
                var scopeResult = new Dictionary<string, List<AnalysisValue>>();
                foreach (var kvp in s.GetAllMergedVariables()) {
                    if (scopeResult.TryGetValue(kvp.Key, out var values)) {
                        values.AddRange(kvp.Value.TypesNoCopy);
                    } else {
                        scopeResult[kvp.Key] = values = new List<AnalysisValue>(kvp.Value.TypesNoCopy);
                    }
                    values.Add(new SyntheticDefinitionInfo(
                        kvp.Key,
                        null,
                        kvp.Value.Definitions.Select(e => e.GetLocationInfo())
                    ));
                }

                foreach (var kvp in scopeResult) {
                    // deliberately overwrite variables from outer scopes
                    result[kvp.Key] = kvp.Value;
                }
            }

            var res = MemberDictToResultList(GetPrivatePrefix(scope), options, result);
            if (options.Keywords()) {
                res = GetKeywordMembers(options, scope).Union(res);
            }

            return res;
        }


        private IEnumerable<MemberResult> GetKeywordMembers(GetMemberOptions options, InterpreterScope scope) {
            IEnumerable<string> keywords = null;
            
            if (options.ExpressionKeywords()) {
                // keywords available in any context
                keywords = PythonKeywords.Expression(ProjectState.LanguageVersion);
            } else {
                keywords = Enumerable.Empty<string>();
            }

            if (options.StatementKeywords()) {
                keywords = keywords.Union(PythonKeywords.Statement(ProjectState.LanguageVersion));
            }

            if (!(scope is FunctionScope)) {
                keywords = keywords.Except(PythonKeywords.InvalidOutsideFunction(ProjectState.LanguageVersion));
            }

            return keywords.Select(kw => new MemberResult(kw, PythonMemberType.Keyword));
        }

        #endregion

        /// <summary>
        /// Gets the available names at the given location.  This includes
        /// global variables and locals, but not built-in variables.
        /// </summary>
        /// <param name="index">
        /// The 0-based absolute index into the file where the available members
        /// should be looked up.
        /// </param>
        /// <remarks>TODO: Remove; this is only used for tests</remarks>
        internal IEnumerable<string> GetVariablesNoBuiltinsByIndex(int index) {
            return GetVariablesNoBuiltins(_unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets the available names at the given location.  This includes
        /// global variables and locals, but not built-in variables.
        /// </summary>
        /// <param name="location">
        /// The location in the file where the available members should be
        /// looked up.
        /// </param>
        /// <remarks>TODO: Remove; this is only used for tests</remarks>
        /// <remarks>New in 2.2</remarks>
        internal IEnumerable<string> GetVariablesNoBuiltins(SourceLocation location) {
            var result = Enumerable.Empty<string>();
            var chain = FindScope(location);
            foreach (var scope in chain.EnumerateFromGlobal) {
                if (scope.VisibleToChildren || scope == chain) {
                    result = result.Concat(scope.GetAllMergedVariables().Select(val => val.Key));
                }
            }
            return result.Distinct();
        }

        /// <summary>
        /// Gets the top-level scope for the module.
        /// </summary>
        internal ModuleInfo GlobalScope {
            get {
                var result = (ModuleScope)Scope;
                return result.Module;
            }
        }

        public IModuleContext InterpreterContext {
            get {
                return GlobalScope.InterpreterContext;
            }
        }

        public PythonAnalyzer ProjectState {
            get { return GlobalScope.ProjectEntry.ProjectState; }
        }

        internal InterpreterScope Scope {
            get { return _scope; }
        }

        internal IEnumerable<MemberResult> GetMemberResults(
            IEnumerable<AnalysisValue> vars,
            InterpreterScope scope,
            GetMemberOptions options
        ) {
            IList<AnalysisValue> namespaces = new List<AnalysisValue>();
            foreach (var ns in vars) {
                if (ns != null) {
                    namespaces.Add(ns);
                }
            }

            if (namespaces.Count == 1) {
                // optimize for the common case of only a single namespace
                var newMembers = namespaces[0].GetAllMembers(GlobalScope.InterpreterContext, options);
                if (newMembers == null || newMembers.Count == 0) {
                    return new MemberResult[0];
                }

                return SingleMemberResult(GetPrivatePrefix(scope), options, newMembers);
            }

            Dictionary<string, IEnumerable<AnalysisValue>> memberDict = null;
            Dictionary<string, IEnumerable<AnalysisValue>> ownerDict = null;
            HashSet<string> memberSet = null;
            int namespacesCount = namespaces.Count;
            foreach (AnalysisValue ns in namespaces) {
                if (ProjectState._noneInst == ns) {
                    namespacesCount -= 1;
                    continue;
                }

                var newMembers = ns.GetAllMembers(GlobalScope.InterpreterContext, options);
                // IntersectMembers(members, memberSet, memberDict);
                if (newMembers == null || newMembers.Count == 0) {
                    continue;
                }

                if (memberSet == null) {
                    // first namespace, add everything
                    memberSet = new HashSet<string>(newMembers.Keys);
                    memberDict = new Dictionary<string, IEnumerable<AnalysisValue>>();
                    ownerDict = new Dictionary<string, IEnumerable<AnalysisValue>>();
                    foreach (var kvp in newMembers) {
                        var tmp = new List<AnalysisValue>(kvp.Value);
                        memberDict[kvp.Key] = tmp;
                        ownerDict[kvp.Key] = new List<AnalysisValue> { ns };
                    }
                } else {
                    // 2nd or nth namespace, union or intersect
                    HashSet<string> toRemove;
                    IEnumerable<string> adding;

                    if (options.Intersect()) {
                        adding = new HashSet<string>(newMembers.Keys);
                        // Find the things only in memberSet that we need to remove from memberDict
                        // toRemove = (memberSet ^ adding) & memberSet

                        toRemove = new HashSet<string>(memberSet);
                        toRemove.SymmetricExceptWith(adding);
                        toRemove.IntersectWith(memberSet);

                        // intersect memberSet with what we're adding
                        memberSet.IntersectWith(adding);

                        // we're only adding things they both had
                        adding = memberSet;
                    } else {
                        // we're adding all of newMembers keys
                        adding = newMembers.Keys;
                        toRemove = null;
                    }

                    // update memberDict
                    foreach (var name in adding) {
                        IEnumerable<AnalysisValue> values;
                        List<AnalysisValue> valueList;
                        if (!memberDict.TryGetValue(name, out values)) {
                            memberDict[name] = values = new List<AnalysisValue>();
                        }
                        if ((valueList = values as List<AnalysisValue>) == null) {
                            memberDict[name] = valueList = new List<AnalysisValue>(values);
                        }
                        valueList.AddRange(newMembers[name]);

                        if (!ownerDict.TryGetValue(name, out values)) {
                            ownerDict[name] = values = new List<AnalysisValue>();
                        }
                        if ((valueList = values as List<AnalysisValue>) == null) {
                            ownerDict[name] = valueList = new List<AnalysisValue>(values);
                        }
                        valueList.Add(ns);
                    }

                    if (toRemove != null) {
                        foreach (var name in toRemove) {
                            memberDict.Remove(name);
                            ownerDict.Remove(name);
                        }
                    }
                }
            }

            if (memberDict == null) {
                return new MemberResult[0];
            }
            if (options.Intersect()) {
                // No need for this information if we're only showing the
                // intersection. Setting it to null saves lookups later.
                ownerDict = null;
            }
            return MemberDictToResultList(GetPrivatePrefix(scope), options, memberDict, ownerDict, namespacesCount);
        }

        /// <summary>
        /// Gets the AST for the given text as if it appeared at the specified
        /// location.
        /// 
        /// If the expression is a member expression such as "fob.__bar" and the
        /// line number is inside of a class definition this will return a
        /// MemberExpression with the mangled name like "fob.__ClassName_Bar".
        /// </summary>
        /// <param name="exprText">The expression to evaluate.</param>
        /// <param name="index">
        /// The 0-based index into the file where the expression should be
        /// evaluated.
        /// </param>
        /// <remarks>New in 1.1</remarks>
        public PythonAst GetAstFromTextByIndex(string exprText, int index) {
            return GetAstFromText(exprText, _unit.Tree.IndexToLocation(index));
        }

        /// <summary>
        /// Gets the AST for the given text as if it appeared at the specified
        /// location.
        /// 
        /// If the expression is a member expression such as "fob.__bar" and the
        /// line number is inside of a class definition this will return a
        /// MemberExpression with the mangled name like "fob._ClassName__bar".
        /// </summary>
        /// <param name="exprText">The expression to evaluate.</param>
        /// <param name="index">
        /// The 0-based index into the file where the expression should be
        /// evaluated.
        /// </param>
        /// <remarks>New in 2.2</remarks>
        public PythonAst GetAstFromText(string exprText, SourceLocation location) {
            var scopes = FindScope(location);
            var privatePrefix = GetPrivatePrefixClassName(scopes);

            return GetAstFromText(exprText, privatePrefix);
        }

        public string ModuleName {
            get {
                return _scope.GlobalScope.Name;
            }
        }

        internal PythonAst GetAstFromText(string exprText, string privatePrefix) {
            var parser = Parser.CreateParser(new StringReader(exprText), _unit.ProjectState.LanguageVersion, new ParserOptions() { PrivatePrefix = privatePrefix, Verbatim = true });
            return parser.ParseTopExpression();
        }

        internal static Expression GetExpression(Statement statement) {
            if (statement is ExpressionStatement) {
                return ((ExpressionStatement)statement).Expression;
            } else if (statement is ReturnStatement) {
                return ((ReturnStatement)statement).Expression;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Gets the chain of scopes which are associated with the given position in the code.
        /// </summary>
        private InterpreterScope FindScope(SourceLocation location) {
            var res = FindScope(Scope, _unit.Tree, location);
            Debug.Assert(res != null, "Should never return null from FindScope");
            return res;
        }

        private static bool IsInFunctionParameter(InterpreterScope scope, PythonAst tree, SourceLocation location) {
            var function = scope.Node as FunctionDefinition;
            if (function == null) {
                // Not a function
                return false;
            }

            int index = tree.LocationToIndex(location);
            if (index < function.StartIndex || index >= function.Body.StartIndex) {
                // Not within the def line
                return false;
            }

            return function.Parameters != null &&
                function.Parameters.Any(p => {
                    var paramName = p.GetVerbatimImage(tree) ?? p.Name;
                    return index >= p.StartIndex && index <= p.StartIndex + paramName.Length;
                });
        }

        private static int GetParentScopeIndent(InterpreterScope scope, PythonAst tree) {
            if (scope is ClassScope) {
                // Return column of "class" statement
                return tree.IndexToLocation(scope.GetStart(tree)).Column;
            }

            var function = scope as FunctionScope;
            if (function != null && !((FunctionDefinition)function.Node).IsLambda) {
                // Return column of "def" statement
                return tree.IndexToLocation(scope.GetStart(tree)).Column;
            }

            var isinstance = scope as IsInstanceScope;
            if (isinstance != null && isinstance._effectiveSuite != null) {
                int col = tree.IndexToLocation(isinstance._startIndex).Column;
                if (isinstance._effectiveSuite.StartIndex < isinstance._startIndex) {
                    // "assert isinstance", so scope is before the test
                    return col - 1;
                } else {
                    // "if isinstance", so scope is at the test
                    return col;
                }
            }

            return -1;
        }

        private static InterpreterScope FindScope(InterpreterScope parent, PythonAst tree, SourceLocation location) {
            var children = parent.Children;

            var index = tree.LocationToIndex(location);

            InterpreterScope candidate = null;

            for (int i = 0; i < children.Count; ++i) {
                if (IsInFunctionParameter(children[i], tree, location)) {
                    // In parameter name scope, so consider the function scope.
                    candidate = children[i];
                    continue;
                }

                int start = children[i].GetBodyStart(tree);

                if (start > index) {
                    // We've gone past index completely so our last candidate is
                    // the best one.
                    break;
                }

                int end = children[i].GetStop(tree);
                if (i + 1 < children.Count) {
                    int nextStart = children[i + 1].GetStart(tree);
                    if (nextStart > end) {
                        end = nextStart;
                    }
                }

                if (index <= end || (candidate == null && i + 1 == children.Count)) {
                    candidate = children[i];
                }
            }

            if (candidate == null || candidate is StatementScope) {
                // No children, so we must belong in our parent
                return parent;
            }

            int scopeIndent = GetParentScopeIndent(candidate, tree);
            if (location.Column <= scopeIndent) {
                // Candidate is at deeper indentation than location and the
                // candidate is scoped, so return the parent instead.
                return parent;
            }

            // Recurse to check children of candidate scope
            var child = FindScope(candidate, tree, location);

            var funcChild = child as FunctionScope;
            if (funcChild != null &&
                funcChild.Function.FunctionDefinition.IsLambda &&
                child.GetStop(tree) < index) {
                // Do not want to extend a lambda function's scope to the end of
                // the parent scope.
                return parent;
            }

            return child;
        }


        private static IEnumerable<MemberResult> MemberDictToResultList(
            string privatePrefix,
            GetMemberOptions options,
            Dictionary<string, IEnumerable<AnalysisValue>> memberDict,
            Dictionary<string, IEnumerable<AnalysisValue>> ownerDict = null,
            int maximumOwners = 0
        ) {
            foreach (var kvp in memberDict) {
                string name = GetMemberName(privatePrefix, options, kvp.Key);
                string completion = name;
                if (name != null) {
                    IEnumerable<AnalysisValue> owners;
                    if (ownerDict != null && ownerDict.TryGetValue(kvp.Key, out owners) &&
                        owners.Any() && owners.Count() < maximumOwners) {
                        // This member came from less than the full set of types.
                        var seenNames = new HashSet<string>();
                        var newName = new StringBuilder(name);
                        newName.Append(" (");
                        foreach (var v in owners) {
                            if (!string.IsNullOrWhiteSpace(v.ShortDescription) && seenNames.Add(v.ShortDescription)) {
                                // Restrict each displayed type to 25 characters
                                if (v.ShortDescription.Length > 25) {
                                    newName.Append(v.ShortDescription.Substring(0, 22));
                                    newName.Append("...");
                                } else {
                                    newName.Append(v.ShortDescription);
                                }
                                newName.Append(", ");
                            }
                            if (newName.Length > 200) break;
                        }
                        // Restrict the entire completion string to 200 characters
                        if (newName.Length > 200) {
                            newName.Length = 197;
                            // Avoid showing more than three '.'s in a row
                            while (newName[newName.Length - 1] == '.') {
                                newName.Length -= 1;
                            }
                            newName.Append("...");
                        } else {
                            newName.Length -= 2;
                        }
                        newName.Append(")");
                        name = newName.ToString();
                    }
                    yield return new MemberResult(name, completion, kvp.Value, null);
                }
            }
        }

        private static IEnumerable<MemberResult> SingleMemberResult(string privatePrefix, GetMemberOptions options, IDictionary<string, IAnalysisSet> memberDict) {
            foreach (var kvp in memberDict) {
                string name = GetMemberName(privatePrefix, options, kvp.Key);
                if (name != null) {
                    yield return new MemberResult(name, kvp.Value);
                }
            }
        }

        private static string GetMemberName(string privatePrefix, GetMemberOptions options, string name) {
            if (privatePrefix != null && name.StartsWith(privatePrefix) && !name.EndsWith("__")) {
                // private prefix inside of the class, filter out the prefix.
                return name.Substring(privatePrefix.Length - 2);
            } else if (!_otherPrivateRegex.IsMatch(name) || !options.HideAdvanced()) {
                return name;
            }
            return null;
        }

        internal string GetPrivatePrefix(SourceLocation sourceLocation) {
            return GetPrivatePrefix(FindScope(sourceLocation));
        }

        private static string GetPrivatePrefixClassName(InterpreterScope scope) {
            var klass = scope.EnumerateTowardsGlobal.OfType<ClassScope>().FirstOrDefault();
            return klass == null ? null : klass.Name;
        }

        private static string GetPrivatePrefix(InterpreterScope scope) {
            string classScopePrefix = GetPrivatePrefixClassName(scope);
            if (classScopePrefix != null) {
                return "_" + classScopePrefix + "__";
            }
            return null;
        }

        private int LineToIndex(int line) {
            if (line <= 1) {    // <= because v1 allowed zero even though we take 1 based lines.
                return 0;
            }

            // line is 1 based, and index 0 in the array is the position of the 2nd line in the file.
            line -= 2;
            return _unit.Tree._lineLocations[line].EndIndex;
        }

        /// <summary>
        /// Finds the best available analysis unit for lookup. This will be the one that is provided
        /// by the nearest enclosing scope that is capable of providing one.
        /// </summary>
        private AnalysisUnit GetNearestEnclosingAnalysisUnit(InterpreterScope scopes) {
            var units = from scope in scopes.EnumerateTowardsGlobal
                        let ns = scope.AnalysisValue
                        where ns != null
                        let unit = ns.AnalysisUnit
                        where unit != null
                        select unit;
            return units.FirstOrDefault() ?? _unit;
        }
    }
}
