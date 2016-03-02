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
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;

namespace Microsoft.PythonTools.Navigation {
    internal class PythonLibraryNode : CommonLibraryNode {
        private readonly PythonMemberType _type;
        
        public PythonLibraryNode(LibraryNode parent, string name, IVsHierarchy hierarchy, uint itemId, PythonMemberType memberType)
            : base(parent, name, name, hierarchy, itemId, LibraryNodeType.Classes) {
            _type = memberType;
        }

        protected PythonLibraryNode(PythonLibraryNode node) : base(node) { }

        protected PythonLibraryNode(PythonLibraryNode node, string newFullName) : base(node, newFullName) { }

        public override LibraryNode Clone() {
            return new PythonLibraryNode(this);
        }

        public override LibraryNode Clone(string newFullName) {
            return new PythonLibraryNode(this, newFullName);
        }

        public override StandardGlyphGroup GlyphType {
            get {
                switch (_type) {
                    case PythonMemberType.Class:
                        return StandardGlyphGroup.GlyphGroupClass;
                    case PythonMemberType.Method:
                    case PythonMemberType.Function:
                        return StandardGlyphGroup.GlyphGroupMethod;
                    case PythonMemberType.Field:
                    case PythonMemberType.Instance:
                        return StandardGlyphGroup.GlyphGroupField;
                    default:
                        return StandardGlyphGroup.GlyphGroupUnknown;
                }                
            }
        }

        public override string GetTextRepresentation(VSTREETEXTOPTIONS options) {
#if FALSE
            FunctionScopeNode funcScope = ScopeNode as FunctionScopeNode;
            if (funcScope != null) {
                StringBuilder sb = new StringBuilder();
                GetFunctionDescription(funcScope.Definition, (text, kind, arg) => {
                    sb.Append(text);
                });
                return sb.ToString();
            }
#endif
            return Name;
        }

        public override void FillDescription(_VSOBJDESCOPTIONS flags, IVsObjectBrowserDescription3 description) {
            description.ClearDescriptionText();
#if FALSE
            FunctionScopeNode funcScope = ScopeNode as FunctionScopeNode;
            if (funcScope != null) {
                description.AddDescriptionText3("def ", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                var def = funcScope.Definition;
                GetFunctionDescription(def, (text, kind, arg) => {
                    description.AddDescriptionText3(text, kind, arg);
                });
                description.AddDescriptionText3(null, VSOBDESCRIPTIONSECTION.OBDS_ENDDECL, null);
                if (def.Body.Documentation != null) {
                    description.AddDescriptionText3("    " + def.Body.Documentation, VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                }
            } else {
                var classScope = ScopeNode as ClassScopeNode;
                if (classScope != null) {
                    FillClassDescription(description, classScope);
                } else {
                    var assign = ScopeNode as AssignmentScopeNode;
                    if (assign != null) {
                        description.AddDescriptionText3("field ", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                        description.AddDescriptionText3(assign.Name, VSOBDESCRIPTIONSECTION.OBDS_NAME, null);
                        description.AddDescriptionText3("\n", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                        description.AddDescriptionText3(null, VSOBDESCRIPTIONSECTION.OBDS_ENDDECL, null);
                    }
                }
            }
#endif
        }

        private static void FillClassDescription(IVsObjectBrowserDescription3 description, ClassScopeNode classScope) {
            description.AddDescriptionText3("class ", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
            description.AddDescriptionText3(classScope.Name, VSOBDESCRIPTIONSECTION.OBDS_NAME, null);
            var classDef = classScope.Definition;
            if (classDef.Bases.Count > 0) {
                description.AddDescriptionText3("(", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                bool comma = false;
                foreach (var baseClass in classDef.Bases) {
                    if (comma) {
                        description.AddDescriptionText3(", ", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                    }

                    string baseStr = FormatExpression(baseClass.Expression);
                    if (baseStr != null) {
                        description.AddDescriptionText3(baseStr, VSOBDESCRIPTIONSECTION.OBDS_TYPE, null);
                    }

                    comma = true;
                }
                description.AddDescriptionText3(")", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
            }

            description.AddDescriptionText3("\n", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
            description.AddDescriptionText3(null, VSOBDESCRIPTIONSECTION.OBDS_ENDDECL, null);

            if (!String.IsNullOrWhiteSpace(classDef.Body.Documentation)) {
                description.AddDescriptionText3("    " + classDef.Body.Documentation, VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
            }
        }

        private static string FormatExpression(Expression baseClass) {
            NameExpression ne = baseClass as NameExpression;
            if (ne != null) {
                return ne.Name;
            }

            MemberExpression me = baseClass as MemberExpression;
            if (me != null) {
                string expr = FormatExpression(me.Target);
                if (expr != null) {
                    return expr + "." + me.Name ?? string.Empty;
                }
            }

            return null;
        }

        private void GetFunctionDescription(FunctionDefinition def, Action<string, VSOBDESCRIPTIONSECTION, IVsNavInfo> addDescription) {
            addDescription(Name, VSOBDESCRIPTIONSECTION.OBDS_NAME, null);
            addDescription("(", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);

            for (int i = 0; i < def.Parameters.Count; i++) {
                if (i != 0) {
                    addDescription(", ", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
                }

                var curParam = def.Parameters[i];

                string name = curParam.Name;
                if (curParam.IsDictionary) {
                    name = "**" + name;
                } else if (curParam.IsList) {
                    name = "*" + curParam.Name;
                }

                if (curParam.DefaultValue != null) {
                    // TODO: Support all possible expressions for default values, we should
                    // probably have a PythonAst walker for expressions or we should add ToCodeString()
                    // onto Python ASTs so they can round trip
                    ConstantExpression defaultValue = curParam.DefaultValue as ConstantExpression;
                    if (defaultValue != null) {
                        // FIXME: Use python repr
                        name = name + " = " + defaultValue.GetConstantRepr(def.GlobalParent.LanguageVersion);
                    }
                }

                addDescription(name, VSOBDESCRIPTIONSECTION.OBDS_PARAM, null);
            }
            addDescription(")\n", VSOBDESCRIPTIONSECTION.OBDS_MISC, null);
        }

        public override int GetLibGuid(out Guid pGuid) {
            pGuid = new Guid(CommonConstants.LibraryGuid);
            return VSConstants.S_OK;
        }
    }
}
