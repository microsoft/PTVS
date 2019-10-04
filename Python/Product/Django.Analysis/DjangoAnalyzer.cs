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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;

namespace Microsoft.PythonTools.Django.Analysis {
    [AnalysisExtensionName(Name)]
    partial class DjangoAnalyzer : IDisposable, IAnalysisExtension {
        internal const string Name = "django";
        internal readonly Dictionary<string, TagInfo> _tags = new Dictionary<string, TagInfo>();
        internal readonly Dictionary<string, TagInfo> _filters = new Dictionary<string, TagInfo>();
        internal readonly IList<DjangoUrl> _urls = new List<DjangoUrl>();
        private readonly HashSet<IPythonProjectEntry> _hookedEntries = new HashSet<IPythonProjectEntry>();
        internal readonly Dictionary<string, TemplateVariables> _templateFiles = new Dictionary<string, TemplateVariables>(StringComparer.OrdinalIgnoreCase);
        private ConditionalWeakTable<Node, ContextMarker> _contextTable = new ConditionalWeakTable<Node, ContextMarker>();
        private ConditionalWeakTable<Node, DeferredDecorator> _decoratorTable = new ConditionalWeakTable<Node, DeferredDecorator>();
        private readonly Dictionary<string, GetTemplateAnalysisValue> _templateAnalysis = new Dictionary<string, GetTemplateAnalysisValue>();
        private PythonAnalyzer _analyzer;
        internal static readonly Dictionary<string, string> _knownTags = MakeKnownTagsTable();
        internal static readonly Dictionary<string, string> _knownFilters = MakeKnownFiltersTable();
        
        public DjangoAnalyzer() {
            foreach (var tagName in DjangoNestedTags._nestedEndTags) {
                _tags[tagName] = new TagInfo("", null);
            }
        }


        internal static class Commands {
            public const string GetTags = "getTags";
            public const string GetVariables = "getVariables";
            public const string GetFilters = "getFilters";
            public const string GetUrls = "getUrls";
            public const string GetMembers = "getMembers";

        }

        public string HandleCommand(string commandId, string body) {
            var serializer = new JavaScriptSerializer();
            Dictionary<string, HashSet<AnalysisValue>> variables;
            switch (commandId) {
                case Commands.GetTags:
                    return serializer.Serialize(_tags.Keys.ToArray());
                case Commands.GetVariables:
                    variables = GetVariablesForTemplateFile(body);
                    if (variables != null) {
                        return serializer.Serialize(variables.Keys.ToArray());
                    }

                    return "[]";
                case Commands.GetFilters:
                    Dictionary<string, string> res = new Dictionary<string, string>();
                    foreach (var filter in _filters) {
                        res[filter.Key] = filter.Value.Documentation;
                    }

                    return serializer.Serialize(res);
                case Commands.GetUrls:
                    // GroupBy + Select have the same effect as Distinct with a long EqualityComparer
                    return serializer.Serialize(_urls.GroupBy(url => url.FullName).Select(urlGroup => urlGroup.First()));
                case Commands.GetMembers:
                    string[] args = serializer.Deserialize<string[]>(body);
                    var file = args[0];
                    var varName = args[1];

                    variables = GetVariablesForTemplateFile(file);
                    HashSet<AnalysisValue> values;
                    IProjectEntry projEntry;
                    if (_analyzer.TryGetProjectEntryByPath(file, out projEntry)) {
                        var context = projEntry.AnalysisContext;

                        if (variables != null && variables.TryGetValue(varName, out values)) {
                            var newTags = new Dictionary<string, PythonMemberType>();
                            foreach (var member in values.SelectMany(item => item.GetAllMembers(context))) {
                                string name = member.Key;
                                PythonMemberType type, newType = GetMemberType(member.Value);

                                if (!newTags.TryGetValue(name, out type)) {
                                    newTags[name] = newType;
                                } else if (type != newType && type != PythonMemberType.Unknown && newType != PythonMemberType.Unknown) {
                                    newTags[name] = PythonMemberType.Multiple;
                                }
                            }

                            var dict = newTags.ToDictionary(x => x.Key, x => x.Value.ToString().ToLowerInvariant());
                            return serializer.Serialize(dict);
                        }
                    }
                    return "{}";
                default:
                    return String.Empty;
            }
        }

        private static PythonMemberType GetMemberType(IAnalysisSet values) {
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

        public void Register(PythonAnalyzer analyzer) {
            if (analyzer == null) {
                throw new ArgumentNullException("analyzer");
            }

            _tags.Clear();
            _filters.Clear();
            _urls.Clear();
            foreach (var entry in _hookedEntries) {
                entry.NewParseTree -= OnNewParseTree;
            }
            _hookedEntries.Clear();
            _templateAnalysis.Clear();
            _templateFiles.Clear();
            _contextTable = new ConditionalWeakTable<Node, ContextMarker>();
            _decoratorTable = new ConditionalWeakTable<Node, DeferredDecorator>();

            foreach (var keyValue in _knownTags) {
                _tags[keyValue.Key] = new TagInfo(keyValue.Value, null);
            }
            foreach (var keyValue in _knownFilters) {
                _filters[keyValue.Key] = new TagInfo(keyValue.Value, null);
            }

            HookAnalysis(analyzer);
            _analyzer = analyzer;
        }

        private void OnNewParseTree(object sender, EventArgs e) {
            var entry = sender as IPythonProjectEntry;
            if (entry != null && _hookedEntries.Remove(entry)) {
                var removeTags = _tags.Where(kv => kv.Value.Entry == entry).Select(kv => kv.Key).ToList();
                var removeFilters = _filters.Where(kv => kv.Value.Entry == entry).Select(kv => kv.Key).ToList();
                foreach (var key in removeTags) {
                    _tags.Remove(key);
                }
                foreach (var key in removeFilters) {
                    _filters.Remove(key);
                }
            }
        }

        private void HookAnalysis(PythonAnalyzer analyzer) {
            analyzer.SpecializeFunction("django.template.loader", "render_to_string", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.shortcuts", "render_to_response", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.shortcuts", "render", RenderProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_kml", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_kmz", RenderToStringProcessor, true);
            analyzer.SpecializeFunction("django.contrib.gis.shortcuts", "render_to_text", RenderToStringProcessor, true);

            analyzer.SpecializeFunction("django.template.Library", "filter", FilterProcessor, true);
            analyzer.SpecializeFunction("django.template.Library", "filter_function", FilterProcessor, true);

            analyzer.SpecializeFunction("django.template.Library", "tag", TagProcessor, true);
            analyzer.SpecializeFunction("django.template.Library", "tag_function", TagProcessor, true);
            analyzer.SpecializeFunction("django.template.Library", "assignment_tag", TagProcessor, true);
            analyzer.SpecializeFunction("django.template.Library", "simple_tag", TagProcessor, true);

            // Django >= 1.9
            analyzer.SpecializeFunction("django.template.library", "import_library", "django.template.library.Library", true);

            // Django < 1.9
            analyzer.SpecializeFunction("django.template.base", "import_library", "django.template.base.Library", true);

            analyzer.SpecializeFunction("django.template.base.Parser", "parse", ParseProcessor, true);

            analyzer.SpecializeFunction("django.template.loader", "get_template", GetTemplateProcessor, true);
            analyzer.SpecializeFunction("django.template.context", "Context", ContextClassProcessor, true);
            analyzer.SpecializeFunction("django.template", "RequestContext", RequestContextClassProcessor, true);
            analyzer.SpecializeFunction("django.template.context", "RequestContext", RequestContextClassProcessor, true);
            analyzer.SpecializeFunction("django.template.base.Template", "render", TemplateRenderProcessor, true);

            // View specializers
            analyzer.SpecializeFunction("django.views.generic.detail.DetailView", "as_view", DetailViewProcessor, true);
            analyzer.SpecializeFunction("django.views.generic.DetailView", "as_view", DetailViewProcessor, true);
            analyzer.SpecializeFunction("django.views.generic.list.ListView", "as_view", ListViewProcessor, true);
            analyzer.SpecializeFunction("django.views.generic.ListView", "as_view", ListViewProcessor, true);

            // Urls specializers
            analyzer.SpecializeFunction("django.conf.urls", "url", UrlProcessor, true);
            analyzer.SpecializeFunction("django.urls", "url", UrlProcessor, true);
            analyzer.SpecializeFunction("django.urls", "path", UrlProcessor, true);
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
                                RegisterTag(unit.ProjectEntry, _tags, str);
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
            foreach (var entry in _hookedEntries) {
                entry.NewParseTree -= OnNewParseTree;
            }
            _hookedEntries.Clear();
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
            var templateNames = GetArg(args, keywordArgNames, "template_name", -1, AnalysisSet.Empty)
                .Select(v => v.GetConstantValueAsString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var templateNameSuffix = GetArg(args, keywordArgNames, "template_name_suffix", -1, AnalysisSet.Empty)
                .Select(v => v.GetConstantValueAsString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var contextObjName = GetArg(args, keywordArgNames, "context_object_name", -1, AnalysisSet.Empty)
                .Select(v => v.GetConstantValueAsString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var model = GetArg(args, keywordArgNames, "model", -1);

            // TODO: Support this (this requires some analyis improvements as currently we 
            // typically don't get useful values for queryset
            // Right now, queryset only flows into the template if template_name
            // is also specified.
            var querySet = GetArg(args, keywordArgNames, "queryset", -1);

            if (templateNames.Any()) {
                foreach (var templateName in templateNames) {
                    AddViewTemplate(unit, model, querySet, contextObjName, templateName);
                }
            } else if (model != null) {
                // template name is [app]/[modelname]_[template_name_suffix]
                string appName;
                int firstDot = unit.ProjectEntry.ModuleName.IndexOf('.');
                if (firstDot != -1) {
                    appName = unit.ProjectEntry.ModuleName.Substring(0, firstDot);
                } else {
                    appName = unit.ProjectEntry.ModuleName;
                }

                foreach (var modelInst in model) {
                    string baseName = appName + "/" + modelInst.Name.ToLowerInvariant();
                    foreach (var suffix in templateNameSuffix.DefaultIfEmpty(defaultTemplateNameSuffix)) {
                        AddViewTemplate(unit, model, querySet, contextObjName, baseName + suffix);
                    }
                }
            }

            return AnalysisSet.Empty;
        }

        private void AddViewTemplate(
            AnalysisUnit unit,
            IAnalysisSet model,
            IAnalysisSet querySet,
            IEnumerable<string> contextObjName,
            string templateName
        ) {
            TemplateVariables tags;
            if (!_templateFiles.TryGetValue(templateName, out tags)) {
                _templateFiles[templateName] = tags = new TemplateVariables();
            }

            if (querySet != null) {
                foreach (var name in contextObjName) {
                    tags.UpdateVariable(name, unit, AnalysisSet.Empty);
                }
            } else if (model != null) {
                foreach (var modelInst in model) {
                    foreach (var name in contextObjName.DefaultIfEmpty(modelInst.Name.ToLowerInvariant())) {
                        tags.UpdateVariable(name, unit, modelInst.GetInstanceType());
                    }
                }
            }
        }

        private IAnalysisSet UrlProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // No completion if the url has no name (reverse matching not possible)
            if (keywordArgNames.Length == 0) {
                return AnalysisSet.Empty;
            }

            IAnalysisSet urlNames = GetArg(args, keywordArgNames, "name", -1);
            if (urlNames == null) { // The kwargs do not contain a name arg
                return AnalysisSet.Empty;
            }

            string urlName = urlNames.First().GetConstantValueAsString();
            string urlRegex = args.First().First().GetConstantValueAsString();
            if (urlName != null && urlRegex != null) {
                _urls.Add(new DjangoUrl(urlName, urlRegex));
            }

            return AnalysisSet.Empty;
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
            return ProcessTags(node, unit, args, keywordArgNames, _filters);
        }

        private IAnalysisSet TagProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return ProcessTags(node, unit, args, keywordArgNames, _tags);
        }

        class DeferredDecorator : AnalysisValue {
            private readonly DjangoAnalyzer _analyzer;
            private readonly IAnalysisSet _name;
            private readonly Dictionary<string, TagInfo> _tags;

            public DeferredDecorator(DjangoAnalyzer analyzer, IAnalysisSet name, Dictionary<string, TagInfo> tags) {
                _analyzer = analyzer;
                _name = name;
                _tags = tags;
            }

            public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
                _analyzer.ProcessTags(node, unit, new[] { AnalysisSet.Empty, _name, args[0] }, NameExpression.EmptyArray, _tags);
                return AnalysisSet.Empty;
            }
        }

        private IAnalysisSet ProcessTags(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames, Dictionary<string, TagInfo> tags) {
            if (args.Length >= 3) {
                // library.filter(name, value)
                foreach (var name in args[1]) {
                    var constName = name.GetConstantValue();
                    if (constName == Type.Missing) {
                        if (name.Name != null) {
                            RegisterTag(unit.ProjectEntry, tags, name.Name, name.Documentation);
                        }
                    } else {
                        var strName = name.GetConstantValueAsString();
                        if (strName != null) {
                            RegisterTag(unit.ProjectEntry, tags, strName);
                        }
                    }
                }
                foreach (var func in args[2]) {
                    // TODO: Find a better node
                    var parser = unit.FindAnalysisValueByName(node, "django.template.base.Parser");
                    if (parser != null) {
                        func.Call(node, unit, new[] { parser, AnalysisSet.Empty }, NameExpression.EmptyArray);
                    }
                }
            } else if (args.Length >= 2) {
                // library.filter(value)
                foreach (var name in args[1]) {
                    string tagName = name.Name ?? name.GetConstantValueAsString();
                    if (tagName != null) {
                        RegisterTag(unit.ProjectEntry, tags, tagName, name.Documentation);
                    }
                    if (name.MemberType != PythonMemberType.Constant) {
                        var parser = unit.FindAnalysisValueByName(node, "django.template.base.Parser");
                        if (parser != null) {
                            name.Call(node, unit, new[] { parser, AnalysisSet.Empty }, NameExpression.EmptyArray);
                        }
                    }
                }
            } else if (args.Length == 1) {
                foreach (var name in args[0]) {
                    if (name.MemberType == PythonMemberType.Constant) {
                        // library.filter('name')
                        DeferredDecorator dec;
                        if (!_decoratorTable.TryGetValue(node, out dec)) {
                            dec = new DeferredDecorator(this, name, tags);
                            _decoratorTable.Add(node, dec);
                        }
                        return dec;
                    } else if (name.Name != null) {
                        // library.filter
                        RegisterTag(unit.ProjectEntry, tags, name.Name, name.Documentation);
                    }
                }
            }

            return AnalysisSet.Empty;
        }

        private void RegisterTag(IPythonProjectEntry entry, Dictionary<string, TagInfo> tags, string name, string documentation = null) {
            TagInfo tag;
            if (!tags.TryGetValue(name, out tag) || (String.IsNullOrWhiteSpace(tag.Documentation) && !String.IsNullOrEmpty(documentation))) {
                tags[name] = tag = new TagInfo(documentation, entry);
                if (entry != null && _hookedEntries.Add(entry)) {
                    entry.NewParseTree += OnNewParseTree;
                }
            }
        }

        private IAnalysisSet RenderToStringProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var names = GetArg(args, keywordArgNames, "template_name", 0);
            var context = GetArg(args, keywordArgNames, "context_instance", 2);
            var dictArgs = context == null ? GetArg(args, keywordArgNames, "dictionary", 1) : null;

            if (dictArgs != null || context != null) {
                foreach (var name in names.Select(n => n.GetConstantValueAsString()).Where(n => !string.IsNullOrEmpty(n))) {
                    AddTemplateMapping(unit, name, dictArgs, context);
                }
            }
            return AnalysisSet.Empty;
        }

        private IAnalysisSet RenderProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var names = GetArg(args, keywordArgNames, "template_name", 1);
            var context = GetArg(args, keywordArgNames, "context_instance", 3);
            var dictArgs = context == null ? GetArg(args, keywordArgNames, "dictionary", 2) : null;

            if (dictArgs != null || context != null) {
                foreach (var name in names.Select(n => n.GetConstantValueAsString()).Where(n => !string.IsNullOrEmpty(n))) {
                    AddTemplateMapping(unit, name, dictArgs, context);
                }
            }
            return AnalysisSet.Empty;
        }

        private void AddTemplateMapping(
            AnalysisUnit unit,
            string filename,
            IEnumerable<AnalysisValue> dictArgs,
            IEnumerable<AnalysisValue> context
        ) {
            TemplateVariables tags;
            if (!_templateFiles.TryGetValue(filename, out tags)) {
                _templateFiles[filename] = tags = new TemplateVariables();
            }

            IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> items = null;
            if (context != null) {
                items = context.OfType<ContextMarker>()
                    .SelectMany(ctxt => ctxt.Arguments.SelectMany(v => v.GetItems()));
            } else if (dictArgs != null) {
                items = dictArgs.SelectMany(v => v.GetItems());
            }

            if (items != null) {
                foreach (var keyValue in items) {
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
                    string filename = GetTemplateValue.Filename;
                    GetTemplateValue.Analyzer.AddTemplateMapping(unit, filename, null, args[0]);
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

            public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
                return Arguments.SelectMany(av => av.GetItems());
            }
        }

        private IAnalysisSet ContextClassProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var dict = GetArg(args, keywordArgNames, "dict_", 0);
            if (dict != null && dict.Any()) {
                ContextMarker contextValue;

                if (!_contextTable.TryGetValue(node, out contextValue)) {
                    contextValue = new ContextMarker();

                    _contextTable.Add(node, contextValue);
                }

                contextValue.Arguments.UnionWith(dict);
                return contextValue;
            }

            return AnalysisSet.Empty;
        }

        private IAnalysisSet RequestContextClassProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var dict = GetArg(args, keywordArgNames, "dict_", 1);
            if (dict != null) {
                return ContextClassProcessor(node, unit, new[] { dict }, NameExpression.EmptyArray);
            }
            return AnalysisSet.Empty;
        }

        private IAnalysisSet TemplateRenderProcessor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2) {
                foreach (var templateValue in args[0].OfType<GetTemplateAnalysisValue>()) {
                    AddTemplateMapping(unit, templateValue.Filename, null, args[1]);
                }
            }

            return AnalysisSet.Empty;
        }

        private static IAnalysisSet GetArg(
            IAnalysisSet[] args,
            NameExpression[] keywordArgNames,
            string name,
            int index,
            IAnalysisSet defaultValue = null
        ) {
            for (int i = 0, j = args.Length - keywordArgNames.Length;
                i < keywordArgNames.Length && j < args.Length;
                ++i, ++j) {
                var kwArg = keywordArgNames[i];
                if (kwArg == null) {
                    Debug.Fail("Null keyword argument");
                } else if (kwArg.Name == name) {
                    return args[j];
                }
            }

            if (0 <= index && index < args.Length) {
                return args[index];
            }

            return defaultValue;
        }

        public Dictionary<string, HashSet<AnalysisValue>> GetVariablesForTemplateFile(string filename) {
            string curLevel = filename;                     // is C:\Fob\Oar\Baz\fob.html
            string curPath = filename = Path.GetFileName(filename);    // is fob.html

            for (; ; ) {
                string curFilename = filename.Replace('\\', '/');
                TemplateVariables res;
                if (_templateFiles.TryGetValue(curFilename, out res)) {
                    return res.GetAllValues();
                }
                curLevel = Path.GetDirectoryName(curLevel);      // C:\Fob\Oar\Baz\fob.html gets us C:\Fob\Oar\Baz
                var fn2 = Path.GetFileName(curLevel);            // Gets us Baz
                if (String.IsNullOrEmpty(fn2)) {
                    break;
                }
                curPath = Path.Combine(fn2, curPath);       // Get us Baz\fob.html
                filename = curPath;
            }

            return null;
        }
    }
}
