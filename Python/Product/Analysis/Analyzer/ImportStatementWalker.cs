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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    internal class ImportStatementWalker : PythonWalker {
        public readonly List<Diagnostic> Diagnostics = new List<Diagnostic>();

        private readonly IPythonProjectEntry _entry;
        private readonly PythonAnalyzer _analyzer;
        private readonly DiagnosticSeverity _severity;
        private readonly PythonAst _ast;

        public ImportStatementWalker(PythonAst ast, IPythonProjectEntry entry, PythonAnalyzer analyzer, DiagnosticSeverity severity) {
            _ast = ast;
            _entry = entry;
            _analyzer = analyzer;
            _severity = severity;
        }

        public override bool Walk(FromImportStatement node) {
            var names = ModuleResolver.GetModuleNamesFromImport(_entry, node);
            foreach (var n in names) {
                if (!_analyzer.IsModuleResolved(_entry, n, node.ForceAbsolute)) {
                    Diagnostics.Add(MakeUnresolvedImport(n, node.Root));
                }
            }
            return base.Walk(node);
        }

        private Diagnostic MakeUnresolvedImport(string name, Node spanNode) {
            var span = spanNode.GetSpan(_ast);
            return new Diagnostic {
                message = ErrorMessages.UnresolvedImport(name),
                range = span,
                severity = _severity,
                code = ErrorMessages.UnresolvedImportCode,
                source = PythonAnalyzer.PythonAnalysisSource
            };
        }

        public override bool Walk(ImportStatement node) {
            foreach (var nameNode in node.Names) {
                var name = nameNode.MakeString();
                if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                    Diagnostics.Add(MakeUnresolvedImport(name, nameNode));
                }
            }
            return base.Walk(node);
        }

        private static bool IsImportError(Expression expr) {
            switch (expr) {
                case NameExpression name:
                    return name.Name == "Exception" || name.Name == "BaseException" || name.Name == "ImportError" || name.Name == "ModuleNotFoundError";
                case TupleExpression tuple:
                    return tuple.Items.Any(IsImportError);
                default:
                    return false;
            }
        }

        private static bool ShouldWalkNormally(TryStatement node) {
            if (node.Handlers == null) {
                return true;
            }

            foreach (var handler in node.Handlers) {
                if (handler.Test == null || IsImportError(handler.Test)) {
                    return false;
                }
            }

            return true;
        }

        public override bool Walk(TryStatement node) {
            if (ShouldWalkNormally(node)) {
                return base.Walk(node);
            }

            // Don't walk 'try' body, but walk everything else
            if (node.Handlers != null) {
                foreach (var handler in node.Handlers) {
                    handler.Walk(this);
                }
            }

            node.Else?.Walk(this);
            node.Finally?.Walk(this);

            return false;
        }
    }

}
