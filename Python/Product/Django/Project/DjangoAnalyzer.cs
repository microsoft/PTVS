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
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Django.Project {
    partial class DjangoAnalyzer : IDisposable {
        internal Dictionary<string, TagInfo> _tags = new Dictionary<string, TagInfo>();
        internal Dictionary<string, TagInfo> _filters = new Dictionary<string, TagInfo>();
        internal Dictionary<string, TemplateVariables> _templateFiles = new Dictionary<string, TemplateVariables>(StringComparer.OrdinalIgnoreCase);
        private ConditionalWeakTable<Node, ContextMarker> _contextTable = new ConditionalWeakTable<Node, ContextMarker>();
        private readonly Dictionary<string, GetTemplateAnalysisValue> _templateAnalysis = new Dictionary<string, GetTemplateAnalysisValue>();
        private PythonAnalyzer _analyzer;
        private static Dictionary<string, string> _knownTags = MakeKnownTagsTable();
        private static Dictionary<string, string> _knownFilters = MakeKnownFiltersTable();

        public DjangoAnalyzer() {
            foreach (var tagName in DjangoCompletionSource._nestedEndTags) {
                _tags[tagName] = new TagInfo("");
            }
        }

        internal void OnNewAnalyzer(PythonAnalyzer analyzer) {
            if (analyzer == null) {
                throw new ArgumentNullException("analyzer");
            }

            _tags.Clear();
            _filters.Clear();

            foreach (var keyValue in _knownTags) {
                _tags[keyValue.Key] = new TagInfo(keyValue.Value);
            }
            foreach (var keyValue in _knownFilters) {
                _filters[keyValue.Key] = new TagInfo(keyValue.Value);
            }

            HookAnalysis(analyzer);
            _analyzer = analyzer;
        }

        private void HookAnalysis(PythonAnalyzer analyzer) {
            analyzer.SpecializeFunction("django.template.loader", "render_to_string", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.shortcuts", "render_to_response", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.shortcuts", "render", RenderProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_kml", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_kmz", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_text", RenderToStringProcessor, true);

            analyzer.SpecializeFunction("django.template.base.Library", "filter", FilterProcessor, true);
            analyzer.SpecializeFunction("django.template.base.Library", "filter_function", FilterProcessor, true);

            analyzer.SpecializeFunction("django.template.base.Library", "tag", TagProcessor, true);
            analyzer.SpecializeFunction("django.template.base.Library", "tag_function", TagProcessor, true);
            analyzer.SpecializeFunction("django.template.base.Library", "assignment_tag", TagProcessor, true);

            analyzer.SpecializeFunction("django.template.base.Parser", "parse", ParseProcessor, true);
            analyzer.SpecializeFunction("django.template.base", "import_library", "django.template.base.Library", true);

            analyzer.SpecializeFunction("django.template.loader", "get_template", GetTemplateProcessor, true);
            analyzer.SpecializeFunction("django.template.context", "Context", ContextClassProcessor, true);
            analyzer.SpecializeFunction("django.template.base.Template", "render", TemplateRenderProcessor, true);

            // View specializers
            analyzer.SpecializeFunction("django.views.generic.detail.DetailView", "as_view", DetailViewProcessor, true);
            analyzer.SpecializeFunction("django.views.generic.list.ListView", "as_view", ListViewProcessor, true);
        }

        private IAnalysisSet ParseProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // def parse(self, parse_until=None):
            // We want to find closing tags here passed to parse_until...
            if (args.Length >= 2) {
                foreach (var tuple in args[1]) {
                    foreach (var indexValue in tuple.GetItems()) {
                        var values = indexValue.Value;
                        foreach (var value in values) {
                            var str = value.GetConstantValueAsString();
                            if (str != null) {
                                RegisterTag(_tags, str);
                            }
                        }
                    }
                }
            }
            return AnalysisSet.Empty;
        }

        #region IDisposable Members

        public void Dispose() {
            _filters.Clear();
            _tags.Clear();
            _templateAnalysis.Clear();
            _templateFiles.Clear();
        }

        #endregion

        /// <summary>
        /// Specializes "DetailView.as_view"
        /// </summary>
        private IAnalysisSet DetailViewProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return ViewProcessor(node, unit, args, keywordArgNames, "_details.html");
        }

        /// <summary>
        /// Specializes "ListView.as_view"
        /// </summary>
        private IAnalysisSet ListViewProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return ViewProcessor(node, unit, args, keywordArgNames, "_list.html");
        }

        private IAnalysisSet ViewProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames, string defaultTemplateNameSuffix) {
            IAnalysisSet model = null;
            HashSet<string> templateNames = new HashSet<string>();
            HashSet<string> templateNameSuffix = new HashSet<string>();
            HashSet<string> contextObjName = new HashSet<string>();
            for (int i = 0; i < keywordArgNames.Length; i++) {
                switch (keywordArgNames[i].Name) {
                    case "queryset":
                        // TODO: Support this (this requires some analyis improvements as currently we 
                        // typically don't get useful values for queryset
                        break;
                    case "model":
                        model = args[args.Length - keywordArgNames.Length + i];
                        break;
                    case "template_name":
                        GetStringArguments(templateNames, args[args.Length - keywordArgNames.Length + i]);
                        break;
                    case "template_name_suffix":
                        GetStringArguments(templateNameSuffix, args[args.Length - keywordArgNames.Length + i]);
                        break;
                    case "context_object_name":
                        GetStringArguments(contextObjName, args[args.Length - keywordArgNames.Length + i]);
                        break;
                }
            }

            if (model != null) {
                if (templateNames.Count > 0) {
                    foreach (var templateName in templateNames) {
                        AddViewTemplate(unit, model, contextObjName, templateName);
                    }
                } else if (model != null) {
                    // template name is [app]/[modelname]_[template_name_suffix]
                    string appName;
                    int firstDot = unit.Project.ModuleName.IndexOf('.');
                    if (firstDot != -1) {
                        appName = unit.Project.ModuleName.Substring(0, firstDot);
                    } else {
                        appName = unit.Project.ModuleName;
                    }

                    foreach (var modelInst in model) {
                        string baseName = appName + "/" + modelInst.Name.ToLower();
                        if (templateNameSuffix.Count > 0) {
                            foreach (var suffix in templateNameSuffix) {
                                AddViewTemplate(
                                    unit,
                                    model,
                                    contextObjName,
                                    baseName + templateNameSuffix
                                );
                            }
                        } else {
                            AddViewTemplate(
                                unit,
                                model,
                                contextObjName,
                                baseName + defaultTemplateNameSuffix
                            );
                        }
                    }
                }
            }

            return AnalysisSet.Empty;
        }

        private void AddViewTemplate(AnalysisUnit unit, IAnalysisSet model, HashSet<string> contextObjName, string templateName) {
            TemplateVariables tags;
            if (!_templateFiles.TryGetValue(templateName, out tags)) {
                _templateFiles[templateName] = tags = new TemplateVariables();
            }

            foreach (var modelInst in model) {
                if (contextObjName.Count == 0) {
                    tags.UpdateVariable(modelInst.Name.ToLower(), unit, modelInst.GetInstanceType());
                } else {
                    foreach (var name in contextObjName) {
                        tags.UpdateVariable(name, unit, modelInst.GetInstanceType());
                    }
                }
            }
        }

        private static void GetStringArguments(HashSet<string> arguments, IAnalysisSet arg) {
            foreach (var value in arg) {
                string templateName = value.GetConstantValueAsString();
                if (templateName != null) {
                    arguments.Add(templateName);
                }
            }
        }

        private IAnalysisSet FilterProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            ProcessTags(node, unit, args, keywordArgNames, _filters);
            return AnalysisSet.Empty;
        }

        private IAnalysisSet TagProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            ProcessTags(node, unit, args, keywordArgNames, _tags);
            return AnalysisSet.Empty;
        }

        private static void ProcessTags(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames, Dictionary<string, TagInfo> tags) {
            if (args.Length >= 3) {
                // library.filter(name, value)
                foreach (var name in args[1]) {
                    var constName = name.GetConstantValue();
                    if (constName == Type.Missing) {
                        if (name.Name != null) {
                            RegisterTag(tags, name.Name, name.Documentation);
                        }
                    } else {
                        var strName = name.GetConstantValueAsString();
                        if (strName != null) {
                            RegisterTag(tags, strName);
                        }
                    }
                }
                foreach (var func in args[2]) {
                    if (func.Name != null) {
                        RegisterTag(tags, func.Name, func.Documentation);
                    }
                    // TODO: Find a better node
                    var parser = unit.FindAnalysisValueByName(node, "django.template.base.Parser");
                    if (parser != null) {
                        func.Call(node, unit, new[] { parser, null }, null);
                    }
                }
            } else if (args.Length >= 2) {
                // library.filter(value)
                foreach (var name in args[1]) {
                    string tagName = name.Name ?? name.GetConstantValueAsString();
                    if (tagName != null) {
                        RegisterTag(tags, tagName, name.Documentation);
                    }
                    if (name.MemberType != PythonMemberType.Constant) {
                        var parser = unit.FindAnalysisValueByName(node, "django.template.base.Parser");
                        if (parser != null) {
                            name.Call(node, unit, new[] { parser, null }, NameExpression.EmptyArray);
                        }
                    }
                }
            } else if (args.Length == 1) {
                // library.filter(value)
                foreach (var name in args[0]) {
                    if (name.Name != null) {
                        RegisterTag(tags, name.Name, name.Documentation);
                    }
                }
            }
        }

        private static void RegisterTag(Dictionary<string, TagInfo> tags, string name, string documentation = null) {
            TagInfo tag;
            if (!tags.TryGetValue(name, out tag) || (String.IsNullOrWhiteSpace(tag.Documentation) && !String.IsNullOrEmpty(documentation))) {
                tags[name] = tag = new TagInfo(documentation);
            }
        }

        private IAnalysisSet RenderToStringProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2) {
                foreach (var name in args[0]) {
                    var strName = name.GetConstantValueAsString();
                    if (strName != null) {
                        var dictArgs = args[1];

                        AddTemplateMapping(unit, strName, dictArgs);
                    }
                }
            }
            return AnalysisSet.Empty;
        }

        private IAnalysisSet RenderProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 3) {
                foreach (var name in args[1]) {
                    var strName = name.GetConstantValueAsString();
                    if (strName != null) {
                        var dictArgs = args[2];

                        AddTemplateMapping(unit, strName, dictArgs);
                    }
                }
            }
            return AnalysisSet.Empty;
        }

        private void AddTemplateMapping(AnalysisUnit unit, string filename, IEnumerable<AnalysisValue> dictArgs) {
            TemplateVariables tags;
            if (!_templateFiles.TryGetValue(filename, out tags)) {
                _templateFiles[filename] = tags = new TemplateVariables();
            }

            foreach (var dict in dictArgs) {
                foreach (var keyValue in dict.GetItems()) {
                    foreach (var key in keyValue.Key) {
                        var keyName = key.GetConstantValueAsString();
                        if (keyName != null) {
                            tags.UpdateVariable(keyName, unit, keyValue.Value);
                        }
                    }
                }
            }
        }

        class GetTemplateAnalysisValue : AnalysisValue {
            public readonly string Filename;
            public readonly TemplateRenderMethod RenderMethod;
            public readonly DjangoAnalyzer Analyzer;

            public GetTemplateAnalysisValue(DjangoAnalyzer analyzer, string name) {
                Analyzer = analyzer;
                Filename = name;
                RenderMethod = new TemplateRenderMethod(this);
            }

            public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
                if (name == "render") {
                    return RenderMethod;
                }
                return base.GetMember(node, unit, name);
            }
        }

        class TemplateRenderMethod : AnalysisValue {
            public readonly GetTemplateAnalysisValue GetTemplateValue;

            public TemplateRenderMethod(GetTemplateAnalysisValue getTemplateAnalysisValue) {
                this.GetTemplateValue = getTemplateAnalysisValue;
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                if (args.Length == 1) {
                    foreach (var contextArg in args[0]) {
                        var context = contextArg as ContextMarker;

                        if (context != null) {
                            // we now have the template and the context

                            string filename = GetTemplateValue.Filename;

                            GetTemplateValue.Analyzer.AddTemplateMapping(unit, filename, context.Arguments);
                        }
                    }
                }
                return base.Call(node, unit, args, keywordArgNames);
            }
        }

        private IAnalysisSet GetTemplateProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = AnalysisSet.Empty;

            if (args.Length >= 1) {
                foreach (var filename in args[0]) {
                    var file = filename.GetConstantValueAsString();
                    if (file != null) {
                        GetTemplateAnalysisValue value;
                        if (!_templateAnalysis.TryGetValue(file, out value)) {
                            _templateAnalysis[file] = value = new GetTemplateAnalysisValue(this, file);
                        }
                        res = res.Add(value);
                    }
                }
            }

            return res;
        }

        class ContextMarker : AnalysisValue {
            public readonly HashSet<AnalysisValue> Arguments;

            public ContextMarker() {
                Arguments = new HashSet<AnalysisValue>();
            }
        }

        private IAnalysisSet ContextClassProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                ContextMarker contextValue;

                if (!_contextTable.TryGetValue(node, out contextValue)) {
                    contextValue = new ContextMarker();

                    _contextTable.Add(node, contextValue);
                }

                contextValue.Arguments.UnionWith(args[0]);
                return contextValue;
            }

            return AnalysisSet.Empty;
        }

        private IAnalysisSet TemplateRenderProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2) {
                foreach (var selfArg in args[0]) {
                    var templateValue = selfArg as GetTemplateAnalysisValue;

                    if (templateValue != null) {
                        foreach (var contextArg in args[1]) {
                            var context = contextArg as ContextMarker;

                            if (context != null) {
                                // we now have the template and the context

                                string filename = templateValue.Filename;

                                AddTemplateMapping(unit, filename, context.Arguments);
                            }
                        }
                    }
                }
            }

            return AnalysisSet.Empty;
        }

        public Dictionary<string, HashSet<AnalysisValue>> GetVariablesForTemplateFile(string filename) {
            string curLevel = filename;                     // is C:\Foo\Bar\Baz\foo.html
            string curPath = filename = Path.GetFileName(filename);    // is foo.html

            for (; ; ) {
                string curFilename = filename.Replace('\\', '/');
                TemplateVariables res;
                if (_templateFiles.TryGetValue(curFilename, out res)) {
                    return res.GetAllValues();
                }
                curLevel = Path.GetDirectoryName(curLevel);      // C:\Foo\Bar\Baz\foo.html gets us C:\Foo\Bar\Baz
                var fn2 = Path.GetFileName(curLevel);            // Gets us Baz
                if (String.IsNullOrEmpty(fn2)) {
                    break;
                }
                curPath = Path.Combine(fn2, curPath);       // Get us Baz\foo.html
                filename = curPath;
            }

            return null;
        }

    }
}
