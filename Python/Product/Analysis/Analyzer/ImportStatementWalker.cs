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

        readonly IPythonProjectEntry _entry;
        readonly PythonAnalyzer _analyzer;
        private readonly PythonAst _ast;

        public ImportStatementWalker(PythonAst ast, IPythonProjectEntry entry, PythonAnalyzer analyzer) {
            _ast = ast;
            _entry = entry;
            _analyzer = analyzer;
        }

        public override bool Walk(FromImportStatement node) {
            var name = node.Root.MakeString();
            if (!_analyzer.IsModuleResolved(_entry, name, node.ForceAbsolute)) {
                Diagnostics.Add(MakeUnresolvedImport(name, node.Root));
            }
            return base.Walk(node);
        }

        private Diagnostic MakeUnresolvedImport(string name, Node spanNode) {
            var span = spanNode.GetSpan(_ast);
            return new Diagnostic {
                message = ErrorMessages.UnresolvedImport(name),
                range = span,
                severity = DiagnosticSeverity.Warning,
                code = ErrorMessages.UnresolvedImportCode,
                source = "Python"
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
            if (expr is NameExpression name) {
                return name.Name == "Exception" || name.Name == "BaseException" || name.Name == "ImportError";
            }

            if (expr is TupleExpression tuple) {
                return tuple.Items.Any(IsImportError);
            }

            return false;
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
