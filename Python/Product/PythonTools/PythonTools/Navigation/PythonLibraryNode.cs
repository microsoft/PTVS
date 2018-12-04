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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;

namespace Microsoft.PythonTools.Navigation {
    internal class PythonLibraryNode : CommonLibraryNode {
        private readonly CompletionResult _value;

        public PythonLibraryNode(LibraryNode parent, CompletionResult value, IVsHierarchy hierarchy, uint itemId, IList<LibraryNode> children)
            : base(parent, value.Name, value.Name, hierarchy, itemId, GetLibraryNodeType(value, parent), children: children) {
            _value = value;
            bool hasLocation = false;
            foreach (var completion in value.Values) {
                if (completion.locations.MaybeEnumerate().Any()) {
                    hasLocation = true;
                }
            }
            if (hasLocation) {
                CanGoToSource = true;
            }
        }

        private static LibraryNodeType GetLibraryNodeType(CompletionResult value, LibraryNode parent) {
            switch (value.MemberType) {
                case PythonMemberType.Class:
                    return LibraryNodeType.Classes;
                case PythonMemberType.Function:
                    //if (parent is PythonFileLibraryNode) {
                    //    return LibraryNodeType.Classes | LibraryNodeType.Members;
                    //}
                    return LibraryNodeType.Members;
                default:
                    return LibraryNodeType.Members;
            }

        }
        protected PythonLibraryNode(PythonLibraryNode node) : base(node) {
            _value = node._value;
        }

        protected PythonLibraryNode(PythonLibraryNode node, string newFullName) : base(node, newFullName) {
            _value = node._value;
        }

        public override LibraryNode Clone() {
            return new PythonLibraryNode(this);
        }

        public override LibraryNode Clone(string newFullName) {
            return new PythonLibraryNode(this, newFullName);
        }

        public override StandardGlyphGroup GlyphType {
            get {
                switch (_value.MemberType) {
                    case PythonMemberType.Class:
                        return StandardGlyphGroup.GlyphGroupClass;
                    case PythonMemberType.Method:
                    case PythonMemberType.Function:
                        return StandardGlyphGroup.GlyphGroupMethod;
                    case PythonMemberType.Field:
                    case PythonMemberType.Instance:
                        return StandardGlyphGroup.GlyphGroupField;
                    case PythonMemberType.Constant:
                        return StandardGlyphGroup.GlyphGroupConstant;
                    case PythonMemberType.Module:
                        return StandardGlyphGroup.GlyphGroupModule;
                    default:
                        return StandardGlyphGroup.GlyphGroupUnknown;
                }
            }
        }

        public override string GetTextRepresentation(VSTREETEXTOPTIONS options) {
            StringBuilder res = new StringBuilder();
            foreach (var value in _value.Values) {
                bool isAlias = false;
                foreach (var desc in value.description) {
                    if (desc.kind == "name") {
                        if (desc.text != Name) {
                            isAlias = true;
                        }
                    }
                }

                var descriptions = new StringBuilder();
                foreach (var desc in value.description) {
                    if (desc.kind == "enddecl") {
                        break;
                    }
                    descriptions.Append(desc.text);
                }

                if (isAlias) {
                    res.Append(Strings.LibraryNodeAliasOf.FormatUI(Name, descriptions));
                } else {
                    res.Append(descriptions);
                }
            }

            if (res.Length == 0) {
                return Name;
            }
            return res.ToString();
        }

        public override void FillDescription(_VSOBJDESCOPTIONS flags, IVsObjectBrowserDescription3 description) {
            description.ClearDescriptionText();
            foreach (var value in _value.Values) {
                foreach (var desc in value.description) {
                    VSOBDESCRIPTIONSECTION kind;
                    switch (desc.kind) {
                        case "enddecl": kind = VSOBDESCRIPTIONSECTION.OBDS_ENDDECL; break;
                        case "name": kind = VSOBDESCRIPTIONSECTION.OBDS_NAME; break;
                        case "param": kind = VSOBDESCRIPTIONSECTION.OBDS_PARAM; break;
                        case "comma": kind = VSOBDESCRIPTIONSECTION.OBDS_COMMA; break;
                        default: kind = VSOBDESCRIPTIONSECTION.OBDS_MISC; break;

                    }
                    description.AddDescriptionText3(desc.text, kind, null);
                }
            }
        }

        public override void GotoSource(VSOBJGOTOSRCTYPE gotoType) {
            // We do not support the "Goto Reference"
            if (VSOBJGOTOSRCTYPE.GS_REFERENCE == gotoType) {
                return;
            }

            foreach (var completion in _value.Values) {
                foreach (var location in completion.locations.MaybeEnumerate()) {
                    if (File.Exists(location.file)) {
                        PythonToolsPackage.NavigateTo(
                            Site,
                            location.file,
                            Guid.Empty,
                            location.startLine - 1,
                            location.startColumn - 1
                        );
                        break;
                    }
                }
            }
        }

        public override IVsSimpleObjectList2 FindReferences() {
            var analyzer = this.Hierarchy.GetPythonProject()?.TryGetAnalyzer();


            List<AnalysisVariable> vars = new List<AnalysisVariable>();
            if (analyzer != null) {
                foreach (var value in _value.Values) {
                    foreach (var reference in value.locations.MaybeEnumerate()) {
                        var entry = analyzer.GetAnalysisEntryFromPath(reference.file);
                        var analysis = analyzer.WaitForRequest(analyzer.AnalyzeExpressionAsync(
                            entry, 
                            Name, 
                            new SourceLocation(reference.startLine, reference.startColumn)
                        ), "PythonLibraryNode.AnalyzeExpression");
                        vars.AddRange(analysis.Variables);
                    }
                }
            }

            return EditFilter.GetFindRefLocations(
                analyzer,
                Site,
                Name,
                vars.ToArray()
            );
        }

        public override int GetLibGuid(out Guid pGuid) {
            pGuid = new Guid(CommonConstants.LibraryGuid);
            return VSConstants.S_OK;
        }
    }
}
