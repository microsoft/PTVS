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
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Captures information about a Django template variable including the filter(s) and any arguments.
    /// 
    /// For example for a variable such as {{ foo!bar }} we'll have a DjangoVariable() with an Expression
    /// which is of kind Variable for "foo" and a DjangoFilter with a filter name of bar.  
    /// 
    /// For {{ foo!bar:42 }} we will have the same thing but the filter will have an Arg of kind Number and
    /// the Value "42".  Likewise for {{ foo!bar:'abc' }} the filter will have an Arg of kind Constant and
    /// a value 'abc'.
    /// </summary>
    class DjangoVariable {
        public readonly DjangoVariableValue Expression;
        public readonly int ExpressionStart;
        public readonly DjangoFilter[] Filters;
        const string _doubleQuotedString = @"""[^""\\]*(?:\\.[^""\\]*)*""";
        const string _singleQuotedString = @"'[^'\\]*(?:\\.[^'\\]*)*'";
        const string _numFormat = @"[-+\.]?\d[\d\.e]*";
        const string _constStr = @"
            (?:_\(" + _doubleQuotedString + @"\)|
            _\(" + _singleQuotedString + @"\)|
            " + _doubleQuotedString + @"|
            " + _singleQuotedString + @")";

        private static Regex _filterRegex = new Regex(@"
^(?<constant>" + _constStr + @")|
^(?<num>" + _numFormat + @")|
^(?<var>[\w\.]+)|
 (?:\|
     (?<filter_name>\w*)
         (?:\:
             (?:
              (?<constant_arg>" + _constStr + @")|
              (?<num_arg>" + _numFormat + @")|
              (?<var_arg>[\w\.]+)
             )
         )?
 )", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public DjangoVariable(DjangoVariableValue expression, int expressionStart, params DjangoFilter[] filters) {
            Expression = expression;
            ExpressionStart = expressionStart;
            Filters = filters;
        }

        public static DjangoVariable Variable(string expression, int expressionStart, params DjangoFilter[] filters) {
            return new DjangoVariable(new DjangoVariableValue(expression, DjangoVariableKind.Variable), expressionStart, filters);
        }

        public static DjangoVariable Constant(string expression, int expressionStart, params DjangoFilter[] filters) {
            return new DjangoVariable(new DjangoVariableValue(expression, DjangoVariableKind.Constant), expressionStart, filters);
        }

        public static DjangoVariable Number(string expression, int expressionStart, params DjangoFilter[] filters) {
            return new DjangoVariable(new DjangoVariableValue(expression, DjangoVariableKind.Number), expressionStart, filters);
        }

        public static DjangoVariable Parse(string filterText, int start = 0) {
            if (filterText.StartsWith("{{")) {
                filterText = GetTrimmedFilterText(filterText, ref start);
                if (filterText == null) {
                    return null;
                }
            }

            int varStart = start;
            DjangoVariableValue variable = null;
            List<DjangoFilter> filters = new List<DjangoFilter>();

            foreach (Match match in _filterRegex.Matches(filterText)) {
                if (variable == null) {
                    var constantGroup = match.Groups["constant"];
                    if (constantGroup.Success) {
                        varStart = constantGroup.Index;
                        variable = new DjangoVariableValue(constantGroup.Value, DjangoVariableKind.Constant);
                    } else {
                        var varGroup = match.Groups["var"];
                        if (!varGroup.Success) {
                            var numGroup = match.Groups["num"];
                            if (!numGroup.Success) {
                                return null;
                            }
                            varStart = numGroup.Index;
                            variable = new DjangoVariableValue(numGroup.Value, DjangoVariableKind.Number);
                        } else {
                            varStart = varGroup.Index;
                            variable = new DjangoVariableValue(varGroup.Value, DjangoVariableKind.Variable);
                        }
                    }
                } else {
                    filters.Add(GetFilterFromMatch(match, start));
                }
            }

            return new DjangoVariable(variable, varStart + start, filters.ToArray());
        }

        /// <summary>
        /// Gets the trimmed filter text and passes back the position in the buffer where the first
        /// character of the filter actually starts.
        internal static string GetTrimmedFilterText(string text, ref int start) {
            start = 0;

            string filterText = null;
            int? tmpStart = null;
            for (int i = 2; i < text.Length; i++) {
                if (!Char.IsWhiteSpace(text[i])) {
                    tmpStart = start = i;
                    break;
                }
            }
            if (tmpStart != null) {
                if (text.EndsWith("%}") || text.EndsWith("}}")) {
                    for (int i = text.Length - 3; i >= tmpStart.Value; i--) {
                        if (!Char.IsWhiteSpace(text[i])) {
                            filterText = text.Substring(tmpStart.Value, i + 1 - tmpStart.Value);
                            break;
                        }
                    }
                } else {
                    // unterminated tag, see if we terminate at a new line
                    for (int i = tmpStart.Value; i < text.Length; i++) {
                        if (text[i] == '\r' || text[i] == '\n') {
                            filterText = text.Substring(tmpStart.Value, i + 1 - tmpStart.Value);
                            break;
                        }
                    }

                    if (filterText == null) {
                        filterText = text.Substring(tmpStart.Value);
                    }
                }
            }

            return filterText;
        }

        private static DjangoFilter GetFilterFromMatch(Match match, int start) {
            var filterName = match.Groups["filter_name"];

            if (!filterName.Success) {
                // TODO: Report error
            }
            var filterStart = filterName.Index;
            DjangoVariableValue arg = null;
            int argStart = 0;

            var constantGroup = match.Groups["constant_arg"];
            if (constantGroup.Success) {
                arg = new DjangoVariableValue(constantGroup.Value, DjangoVariableKind.Constant);
                argStart = constantGroup.Index;
            } else {
                var varGroup = match.Groups["var_arg"];
                if (varGroup.Success) {
                    arg = new DjangoVariableValue(varGroup.Value, DjangoVariableKind.Variable);
                    argStart = varGroup.Index;
                } else {
                    var numGroup = match.Groups["num_arg"];
                    if (numGroup.Success) {
                        arg = new DjangoVariableValue(numGroup.Value, DjangoVariableKind.Number);
                        argStart = numGroup.Index;
                    }
                }
            }
            return new DjangoFilter(filterName.Value, filterStart + start, arg, argStart + start);
        }

        public IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {            
            if (Expression != null && position == Expression.Value.Length + ExpressionStart && Expression.Value.EndsWith(".")) {
                // TODO: Handle multiple dots
                string varName = Expression.Value.Substring(0, Expression.Value.IndexOf('.'));
                
                // get the members of this variable
                HashSet<AnalysisValue> values;
                if (context.Variables != null && context.Variables.TryGetValue(varName, out values)) {
                    var newTags = new Dictionary<string, PythonMemberType>();
                    foreach (var member in values.SelectMany(item => item.GetAllMembers())) {
                        string name = member.Key;
                        PythonMemberType type, newType = GetMemberType(member.Value);
                                    
                        if (!newTags.TryGetValue(name, out type)) {
                            newTags[name] = newType;
                        } else if (type != newType && type != PythonMemberType.Unknown && newType != PythonMemberType.Unknown) {
                            newTags[name] = PythonMemberType.Multiple;
                        }
                    }
                    return CompletionInfo.ToCompletionInfo(newTags);
                }

                return Enumerable.Empty<CompletionInfo>();

            } else if (Filters.Length > 0) {
                // we are triggering in the filter or arg area
                foreach (var curFilter in Filters) {
                    if (position >= curFilter.FilterStart && position <= curFilter.FilterStart + curFilter.Filter.Length) {
                        // it's in this filter area
                        return CompletionInfo.ToCompletionInfo(context.Filters, StandardGlyphGroup.GlyphKeyword);
                    } else if (curFilter.Arg != null && position >= curFilter.ArgStart && position < curFilter.ArgStart + curFilter.Arg.Value.Length) {
                        // it's in this argument
                        return CompletionInfo.ToCompletionInfo(context.Variables, StandardGlyphGroup.GlyphGroupField);
                    }
                }

                if (String.IsNullOrWhiteSpace(Filters.Last().Filter)) {
                    // last filter was blank, so provide filters
                    return CompletionInfo.ToCompletionInfo(context.Filters, StandardGlyphGroup.GlyphKeyword);
                } else {
                    // ... else, provide variables
                    return CompletionInfo.ToCompletionInfo(context.Variables, StandardGlyphGroup.GlyphGroupField);
                }
            }

            return CompletionInfo.ToCompletionInfo(context.Variables, StandardGlyphGroup.GlyphGroupField);
        }

        private static PythonMemberType GetMemberType(ISet<AnalysisValue> values) {
            PythonMemberType newType = PythonMemberType.Unknown;
            foreach (var value in values) {
                if (value.MemberType == newType) {
                    continue;
                } else if (newType == PythonMemberType.Unknown) {
                    newType = value.MemberType;
                } else {
                    newType = PythonMemberType.Multiple;
                    break;
                }
            }
            return newType;
        }

        public IEnumerable<BlockClassification> GetSpans() {
            if (Expression != null) {
                foreach (var span in Expression.GetSpans(ExpressionStart)) {
                    yield return span;
                }
            }

            foreach (var filter in Filters) {
                foreach (var span in filter.GetSpans()) {
                    yield return span;
                }
            }
        }
    }

    class CompletionInfo {
        public readonly string DisplayText;
        public readonly StandardGlyphGroup Glyph;
        public readonly string InsertionText;
        public readonly string Documentation;

        public CompletionInfo(string displayText, StandardGlyphGroup glyph, string insertionText = null, string documentation = null) {
            DisplayText = displayText;
            Glyph = glyph;
            InsertionText = insertionText ?? displayText;
            Documentation = documentation ?? "";
        }

        internal static IEnumerable<CompletionInfo> ToCompletionInfo(IEnumerable<string> keys, StandardGlyphGroup glyph) {
            return keys.Select(key => new CompletionInfo(key, glyph, key));
        }

        internal static IEnumerable<CompletionInfo> ToCompletionInfo(Dictionary<string, PythonMemberType> keys) {
            return keys.Select(kv => new CompletionInfo(kv.Key, kv.Value.ToGlyphGroup(), kv.Key));
        }

        internal static IEnumerable<CompletionInfo> ToCompletionInfo<T>(Dictionary<string, T> dictionary, StandardGlyphGroup glyph) {
            if (dictionary == null) {
                return Enumerable.Empty<CompletionInfo>();
            }
            return ToCompletionInfo(dictionary.Keys, glyph);
        }
    }
}
