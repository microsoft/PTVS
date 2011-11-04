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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Analysis.Values;
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
        private readonly InterpreterScope[] _scopes;
        private readonly Stack<InterpreterScope> _scopeTree;
        private static Regex _otherPrivateRegex = new Regex("^_[a-zA-Z_]\\w*__[a-zA-Z_]\\w*$");

        internal ModuleAnalysis(AnalysisUnit unit, Stack<InterpreterScope> tree) {
            _unit = unit;
            _scopes = unit.Scopes;
            _scopeTree = tree;
        }

        #region Public API

        /// <summary>
        /// Evaluates the given expression in at the provided line number and returns the values
        /// that the expression can evaluate to.
        /// </summary>
        /// <param name="exprText">The expression to determine the result of.</param>
        /// <param name="lineNumber">The 1-based line number to evaluate at within the module.</param>
        [Obsolete("Use GetValuesByIndex instead")]
        public IEnumerable<IAnalysisValue> GetValues(string exprText, int lineNumber) {
            return GetValuesByIndex(exprText, LineToIndex(lineNumber));
        }

        /// <summary>
        /// Evaluates the given expression in at the provided line number and returns the values
        /// that the expression can evaluate to.
        /// </summary>
        /// <param name="exprText">The expression to determine the result of.</param>
        /// <param name="index">The 0-based absolute index into the file where the expression should be evaluated within the module.</param>
        public IEnumerable<IAnalysisValue> GetValuesByIndex(string exprText, int index) {
            var scopes = FindScopes(index);
            var privatePrefix = GetPrivatePrefixClassName(scopes);
            var expr = Statement.GetExpression(GetAstFromText(exprText, privatePrefix).Body);

            var eval = new ExpressionEvaluator(_unit.CopyForEval(), scopes.ToArray());

            var res = eval.Evaluate(expr);
            foreach (var v in res) {
                MultipleMemberInfo multipleMembers = v as MultipleMemberInfo;
                if (multipleMembers != null) {
                    foreach (var member in multipleMembers.Members) {
                        if (v.IsCurrent) {
                            yield return member;
                        }
                    }
                } else if (v.IsCurrent) {
                    yield return v;
                }                
            }
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
                var start = locatedDef.Node.GetStart(locatedDef.Entry.Tree);
                yield return new AnalysisVariable(VariableType.Definition, new LocationInfo(locatedDef.Entry, start.Line, start.Column));
            }

            VariableDef def = referenceable as VariableDef;
            if (def != null) {
                foreach (var type in def.Types) {
                    foreach (var location in type.Locations) {
                        yield return new AnalysisVariable(VariableType.Value, location);
                    }
                }
            }

            foreach (var reference in referenceable.Definitions) {
                yield return new AnalysisVariable(VariableType.Definition, reference.Value.GetLocationInfo(reference.Key));
            }

            foreach (var reference in referenceable.References) {
                yield return new AnalysisVariable(VariableType.Reference, reference.Value.GetLocationInfo(reference.Key));
            }
        }

        
        /// <summary>
        /// Gets the variables the given expression evaluates to.  Variables include parameters, locals, and fields assigned on classes, modules and instances.
        /// 
        /// Variables are classified as either definitions or references.  Only parameters have unique definition points - all other types of variables
        /// have only one or more references.
        /// 
        /// lineNumber is a 1-based line number.
        /// </summary>
        [Obsolete("Use GetVariablesByIndex instead")]
        public IEnumerable<IAnalysisVariable> GetVariables(string exprText, int lineNumber) {
            return GetVariablesByIndex(exprText, LineToIndex(lineNumber));
        }

        /// <summary>
        /// Gets the variables the given expression evaluates to.  Variables include parameters, locals, and fields assigned on classes, modules and instances.
        /// 
        /// Variables are classified as either definitions or references.  Only parameters have unique definition points - all other types of variables
        /// have only one or more references.
        /// 
        /// index is a 0-based absolute index into the file.
        /// </summary>
        public IEnumerable<IAnalysisVariable> GetVariablesByIndex(string exprText, int index) {
            var scopes = FindScopes(index);
            string privatePrefix = GetPrivatePrefixClassName(scopes);
            var expr = Statement.GetExpression(GetAstFromText(exprText, privatePrefix).Body);

            var eval = new ExpressionEvaluator(_unit.CopyForEval(), scopes.ToArray());
            NameExpression name = expr as NameExpression;
            if (name != null) {
                for (int i = scopes.Count - 1; i >= 0; i--) {
                    VariableDef def;
                    if (IncludeScope(scopes, i, index)) {
                        if (scopes[i].Variables.TryGetValue(name.Name, out def)) {
                            foreach (var res in ToVariables(def)) {
                                yield return res;
                            }

                            // if a variable is imported from another module then also yield the defs/refs for the 
                            // value in the defining module.
                            var linked = scopes[i].GetLinkedVariablesNoCreate(name.Name);
                            if (linked != null) {
                                foreach (var linkedVar in linked) {
                                    foreach (var res in ToVariables(linkedVar)) {
                                        yield return res;
                                    }
                                }
                            }

                            IsInstanceScope isInstScope = scopes[i] as IsInstanceScope;
                            if (isInstScope != null) {
                                VariableDef outerVar;
                                if (isInstScope.OuterVariables.TryGetValue(name.Name, out outerVar)) {
                                    foreach (var res in ToVariables(outerVar)) {
                                        yield return res;
                                    }
                                }
                            }

                            // if the member is defined in a base class as well include the base class member and references
                            if (scopes[i] is ClassScope) {
                                var klass = scopes[i].Namespace as ClassInfo;
                                if (klass.Push()) {
                                    try {
                                        foreach (var baseClass in klass.Bases) {
                                            foreach (var baseNs in baseClass) {
                                                if (baseNs.Push()) {
                                                    try {
                                                        ClassInfo baseClassNs = baseNs as ClassInfo;
                                                        if (baseClassNs != null) {
                                                            if (baseClassNs.Scope.Variables.TryGetValue(name.Name, out def)) {
                                                                foreach (var res in ToVariables(def)) {
                                                                    yield return res;
                                                                }
                                                            }
                                                        }
                                                    } finally {
                                                        baseNs.Pop();
                                                    }
                                                }
                                            }
                                        }
                                    } finally {
                                        klass.Pop();
                                    }
                                }
                            }
                            yield break;
                        }
                    }
                }

                var variables = _unit.ProjectState.BuiltinModule.GetDefinitions(name.Name);
                foreach (var referenceable in variables) {
                    foreach (var res in ToVariables(referenceable)) {
                        yield return res;
                    }
                }
            }

            MemberExpression member = expr as MemberExpression;
            if (member != null) {
                var objects = eval.Evaluate(member.Target);

                foreach (var v in objects) {
                    var container = v as IReferenceableContainer;
                    if (container != null) {
                        foreach (var variable in ReferencablesToVariables(container.GetDefinitions(member.Name))) {
                            yield return variable;
                        }
                    }
                }
            }
        }

        public MemberResult[] GetModules(bool topLevelOnly = false) {
            List<MemberResult> res = new List<MemberResult>(ProjectState.GetModules(topLevelOnly));

            var children = GlobalScope.GetChildrenPackages(InterpreterContext);

            foreach (var child in children) {
                res.Add(new MemberResult(child.Key, PythonMemberType.Module));
            }

            return res.ToArray();
        }

        public MemberResult[] GetModuleMembers(string[] names, bool includeMembers = false) {
            List<MemberResult> res = new List<MemberResult>(ProjectState.GetModuleMembers(InterpreterContext, names, includeMembers));

            var children = GlobalScope.GetChildrenPackages(InterpreterContext);

            foreach (var child in children) {
                var mod = (ModuleInfo)child.Value;
                var childName = mod.Name.Substring(this.GlobalScope.Name.Length + 1);

                if (childName.StartsWith(names[0])) {
                    res.AddRange(PythonAnalyzer.GetModuleMembers(InterpreterContext, names, includeMembers, mod as IModule));
                }
            }

            return res.ToArray();
        }

        private static bool IncludeScope(List<InterpreterScope> scopes, int i, int index) {
            if (scopes[i].VisibleToChildren || i == scopes.Count - 1) {
                return true;
            }

            // if we're on the 1st line of a function include our class def as well
            if (i == scopes.Count - 2 && scopes[scopes.Count - 1] is FunctionScope) {
                var funcScope = (FunctionScope)scopes[scopes.Count - 1];
                var def = funcScope.Function.FunctionDefinition;
                
                // TODO: Fix me to use indexes
                int lineNo = def.GlobalParent.IndexToLocation(index).Line;
                if (lineNo == def.GetStart(def.GlobalParent).Line) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Evaluates a given expression and returns a list of members which exist in the expression.
        /// 
        /// If the expression is an empty string returns all available members at that location.
        /// 
        /// lineNumber is a 1-based line number.
        /// </summary>
        [Obsolete("Use GetMembersByIndex instead")]
        public IEnumerable<MemberResult> GetMembers(string exprText, int lineNumber, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return GetMembersByIndex(exprText, LineToIndex(lineNumber), options);
        }

        /// <summary>
        /// Evaluates a given expression and returns a list of members which exist in the expression.
        /// 
        /// If the expression is an empty string returns all available members at that location.
        /// 
        /// index is a zero-based absolute index into the file.
        /// </summary>
        public IEnumerable<MemberResult> GetMembersByIndex(string exprText, int index, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            if (exprText.Length == 0) {
                return GetAllAvailableMembersByIndex(index);
            }

            var scopes = FindScopes(index).ToArray();
            var privatePrefix = GetPrivatePrefixClassName(scopes);

            var expr = Statement.GetExpression(GetAstFromText(exprText, privatePrefix).Body);
            if (expr is ConstantExpression && ((ConstantExpression)expr).Value is int) {
                // no completions on integer ., the user is typing a float
                return new MemberResult[0];
            }

            var lookup = new ExpressionEvaluator(_unit.CopyForEval(), scopes).Evaluate(expr);
            return GetMemberResults(lookup, scopes, options);
        }

        /// <summary>
        /// Gets information about the available signatures for the given expression.
        /// </summary>
        /// <param name="exprText">The expression to get signatures for.</param>
        /// <param name="lineNumber">The 1-based line number to use for the context of looking up members.</param>
        [Obsolete("Use GetSignaturesByIndex instead")]
        public IEnumerable<IOverloadResult> GetSignatures(string exprText, int lineNumber) {
            return GetSignaturesByIndex(exprText, LineToIndex(lineNumber));
        }

        /// <summary>
        /// Gets information about the available signatures for the given expression.
        /// </summary>
        /// <param name="exprText">The expression to get signatures for.</param>
        /// <param name="index">The 0-based absolute index into the file.</param>
        public IEnumerable<IOverloadResult> GetSignaturesByIndex(string exprText, int index) {
            try {
                
                var eval = new ExpressionEvaluator(_unit.CopyForEval(), FindScopes(index).ToArray());
                using (var parser = Parser.CreateParser(new StringReader(exprText), _unit.ProjectState.LanguageVersion)) {
                    var expr = GetExpression(parser.ParseTopExpression().Body);
                    if (expr is ListExpression ||
                        expr is TupleExpression ||
                        expr is DictionaryExpression) {
                        return new OverloadResult[0];
                    }
                    var lookup = eval.Evaluate(expr);

                    var result = new List<OverloadResult>();

                    // TODO: Include relevant type info on the parameter...
                    foreach (var ns in lookup) {
                        result.AddRange(ns.Overloads);
                    }

                    return result.ToArray();
                }
            } catch (Exception) {
                // TODO: log exception
                return new[] { new SimpleOverloadResult(new ParameterResult[0], "Unknown", "IntellisenseError_Sigs") };
            }
        }

        /// <summary>
        /// Gets the available names at the given location.  This includes built-in variables, global variables, and locals.
        /// </summary>
        /// <param name="lineNumber">The 1-based line number where the available mebmers should be looked up.</param>
        [Obsolete("Use GetAllAvailableMembersByIndex instead")]
        public IEnumerable<MemberResult> GetAllAvailableMembers(int lineNumber, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return GetAllAvailableMembers(LineToIndex(lineNumber), options);
        }

        /// <summary>
        /// Gets the available names at the given location.  This includes built-in variables, global variables, and locals.
        /// </summary>
        /// <param name="index">The 0-based absolute index into the file where the available mebmers should be looked up.</param>
        public IEnumerable<MemberResult> GetAllAvailableMembersByIndex(int index, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            var result = new Dictionary<string, List<Namespace>>();

            // collect builtins
            foreach (var variable in ProjectState.BuiltinModule.GetAllMembers(ProjectState._defaultContext)) {
                result[variable.Key] = new List<Namespace>(variable.Value);
            }

            // collect variables from user defined scopes
            var scopes = FindScopes(index);
            foreach (var scope in scopes) {
                foreach (var kvp in scope.Variables) {
                    result[kvp.Key] = new List<Namespace>(kvp.Value.Types);
                }
            }
            return MemberDictToResultList(GetPrivatePrefix(scopes), options, result);
        }

        #endregion

        /// <summary>
        /// TODO: This should go away, it's only used for tests.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="lineNumber"></param>
        /// <returns></returns>
        internal IEnumerable<IPythonType> GetTypesFromNameByIndex(string name, int index) {
            var chain = FindScopes(index);
            var result = new HashSet<IPythonType>();
            foreach (var scope in chain) {
                if (scope.VisibleToChildren || scope == chain[chain.Count - 1]) {
                    VariableDef v;
                    if (scope.Variables.TryGetValue(name, out v)) {
                        foreach (var ns in v.Types) {
                            if (ns != null && ns.PythonType != null) {
                                result.Add(ns.PythonType);
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a list of valid names available at the given position in the analyzed source code minus the builtin variables.
        /// 
        /// TODO: This should go away, it's only used for tests.
        /// </summary>
        /// <param name="lineNumber">The line number where the available mebmers should be looked up.</param>
        internal IEnumerable<string> GetVariablesNoBuiltinsByIndex(int index) {
            var chain = FindScopes(index);
            foreach (var scope in chain) {
                if (scope.VisibleToChildren || scope == chain[chain.Count - 1]) {
                    foreach (var varName in scope.Variables) {
                        yield return varName.Key;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the top-level scope for the module.
        /// </summary>
        internal ModuleInfo GlobalScope {
            get {
                var result = (Scopes[0] as ModuleScope);
                Debug.Assert(result != null);
                return result.Module;
            }
        }

        public IModuleContext InterpreterContext {
            get {
                return GlobalScope.InterpreterContext;
            }
        }

        /// <summary>
        /// Gets the tree of all scopes for the module.
        /// </summary>
        internal Stack<InterpreterScope> ScopeTree {
            get { return _scopeTree; }
        }

        public PythonAnalyzer ProjectState {
            get { return GlobalScope.ProjectEntry.ProjectState; }
        }

        internal InterpreterScope[] Scopes {
            get { return _scopes; }
        }

        internal IEnumerable<MemberResult> GetMemberResults(IEnumerable<Namespace> vars, InterpreterScope[] scopes, GetMemberOptions options) {
            IList<Namespace> namespaces = new List<Namespace>();
            foreach (var ns in vars) {
                if (ns != null) {
                    namespaces.Add(ns);
                }
            }

            if (namespaces.Count == 1) {
                // optimize for the common case of only a single namespace
                var newMembers = namespaces[0].GetAllMembers(GlobalScope.InterpreterContext);
                if (newMembers == null || newMembers.Count == 0) {
                    return new MemberResult[0];
                }

                return SingleMemberResult(GetPrivatePrefix(scopes), options, newMembers);
            }

            Dictionary<string, List<Namespace>> memberDict = null;
            Dictionary<string, List<Namespace>> ownerDict = null;
            HashSet<string> memberSet = null;
            foreach (Namespace ns in namespaces) {
                if (ProjectState._noneInst == ns) {
                    continue;
                }

                var newMembers = ns.GetAllMembers(GlobalScope.InterpreterContext);
                // IntersectMembers(members, memberSet, memberDict);
                if (newMembers == null || newMembers.Count == 0) {
                    continue;
                }

                if (memberSet == null) {
                    // first namespace, add everything
                    memberSet = new HashSet<string>(newMembers.Keys);
                    memberDict = new Dictionary<string, List<Namespace>>();
                    ownerDict = new Dictionary<string, List<Namespace>>();
                    foreach (var kvp in newMembers) {
                        var tmp = new List<Namespace>(kvp.Value);
                        memberDict[kvp.Key] = tmp;
                        ownerDict[kvp.Key] = new List<Namespace> { ns };
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
                        List<Namespace> values;
                        if (!memberDict.TryGetValue(name, out values)) {
                            memberDict[name] = values = new List<Namespace>();
                        }
                        values.AddRange(newMembers[name]);
                        if (!ownerDict.TryGetValue(name, out values)) {
                            ownerDict[name] = values = new List<Namespace>();
                        }
                        values.Add(ns);
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
            return MemberDictToResultList(GetPrivatePrefix(scopes), options, memberDict, ownerDict, namespaces.Count);
        }

        /// <summary>
        /// Gets the expression for the given text.  
        /// 
        /// This overload shipped in v1 but does not take into account private members 
        /// prefixed with __'s.   Calling the GetExpressionFromText(string exprText, int lineNumber)
        /// overload will take into account the current class and therefore will
        /// work properly with name mangled private members.  
        /// </summary>
        public Expression GetExpressionFromText(string exprText) {
            return Statement.GetExpression(GetAstFromText(exprText, null).Body);
        }

        /// <summary>
        /// Gets the AST for the given text as if it appeared at the specified line number.
        /// 
        /// If the expression is a member expression such as "foo.__bar" and the line number is
        /// inside of a class definition this will return a MemberExpression with the mangled name
        /// like "foo.__ClassName_Bar".
        /// 
        /// index is a 0-based absolute index into the file.
        /// 
        /// New in 1.1.
        /// </summary>
        public PythonAst GetAstFromTextByIndex(string exprText, int index) {
            var scopes = FindScopes(index);
            var privatePrefix = GetPrivatePrefixClassName(scopes);

            return GetAstFromText(exprText, privatePrefix);
        }

        private PythonAst GetAstFromText(string exprText, string privatePrefix) {
            using (var parser = Parser.CreateParser(new StringReader(exprText), _unit.ProjectState.LanguageVersion, new ParserOptions() { PrivatePrefix = privatePrefix, Verbatim = true })) {
                return parser.ParseTopExpression();
            }
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
        private List<InterpreterScope> FindScopes(int index) {
            InterpreterScope curScope = ScopeTree.First();
            InterpreterScope prevScope = null;
            var chain = new List<InterpreterScope> { Scopes[0] };

            while (curScope != prevScope) {
                prevScope = curScope;

                // TODO: Binary search?
                // We currently search backwards because the end positions are sometimes unreliable
                // and go onto the next line overlapping w/ the previous definition.  Therefore searching backwards always 
                // hits the valid method first matching on Start.  For example:
                // def f():  # Starts on 1, ends on 3
                //     pass
                // def g():  # starts on 3, ends on 4
                //     pass
                var parent = _unit.Tree;
                int lastStart = curScope.GetStart(parent) - 1;

                for (int i = curScope.Children.Count - 1; i >= 0; i--) {
                    var scope = curScope.Children[i];
                    var curStart = scope.GetBodyStart(parent);
                    

                    if (curStart < index) {
                        var curEnd = scope.GetStop(parent);

                        if (curEnd >= index ||                                      // we fit in this scope
                            (i == curScope.Children.Count - 1 && curEnd < index) || // last scope, we're implicitly in it
                            index < lastStart) {                                    // gap in scopes, we are in this one.
                            if (!(scope is StatementScope)) {
                                curScope = scope;
                                chain.Add(curScope);
                            }
                            break;
                        }
                    } else if (scope is FunctionScope) {
                        var initialStart = scope.GetStart(parent);
                        if (initialStart < curStart) {
                            // we could be on a parameter or we could be on a default value.
                            // If we're on a parameter then we're logically in the function
                            // scope.  If we're on a default value then we're in the outer
                            // scope.
                            var funcDef = (FunctionDefinition)((FunctionScope)scope).Node;

                            if (funcDef.Parameters != null) {
                                bool isParam = false;
                                foreach (var param in funcDef.Parameters) {
                                    string paramName = param.GetVerbatimImage(_unit.Tree) ?? param.Name;
                                    var nameStart = param.IndexSpan.Start;

                                    if (index >= nameStart && index <= (nameStart + paramName.Length)) {
                                        curScope = scope;
                                        chain.Add(curScope);
                                        isParam = true;
                                        break;
                                    }

                                }

                                if (isParam) {
                                    break;
                                }
                            }

                        }
                    }

                    lastStart = scope.GetStart(parent);
                }
            }
            return chain;
        }

        private static IEnumerable<MemberResult> MemberDictToResultList(string privatePrefix, GetMemberOptions options, Dictionary<string, List<Namespace>> memberDict,
            Dictionary<string, List<Namespace>> ownerDict=null, int maximumOwners=0) {
            foreach (var kvp in memberDict) {
                string name = GetMemberName(privatePrefix, options, kvp.Key);
                string completion = name;
                if (name != null) {
                    List<Namespace> owners;
                    if (ownerDict != null && ownerDict.TryGetValue(name, out owners) && kvp.Value.Count >= 1 && kvp.Value.Count < maximumOwners) {
                        var types = new System.Text.StringBuilder();
                        foreach (var v in owners) {
                            if (!string.IsNullOrWhiteSpace(v.ShortDescription)) {
                                types.Append(v.ShortDescription);
                                types.Append(", ");
                            }
                        }
                        if (types.Length > 2) {
                            types.Length -= 2;
                        }
                        name += " (" + types.ToString() + ")";
                    }
                    yield return new MemberResult(name, completion, kvp.Value, null);
                }
            }
        }

        private static IEnumerable<MemberResult> SingleMemberResult(string privatePrefix, GetMemberOptions options, IDictionary<string, ISet<Namespace>> memberDict) {
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

        private static string GetPrivatePrefixClassName(IList<InterpreterScope> scopes) {
            for (int scope = scopes.Count - 1; scope >= 0; scope--) {
                if (scopes[scope] is ClassScope) {
                    return scopes[scope].Name;
                }
            }
            return null;
        }

        private static string GetPrivatePrefix(IList<InterpreterScope> scopes) {
            string classScopePrefix = GetPrivatePrefixClassName(scopes);
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
            return _unit.Tree._lineLocations[line];
        }
    }
}
