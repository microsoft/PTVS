using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class FormatSpecifier : Expression {
        private readonly Node[] _children;

        public FormatSpecifier(Node[] children, string unparsed) {
            _children = children;
            Unparsed = unparsed;
        }

        public override IEnumerable<Node> GetChildNodes() {
            return _children;
        }

        public readonly string Unparsed;

        public override string NodeName => "format specifier";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var child in _children) {
                    child.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public async override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var child in _children) {
                    await child.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            // There is no leading f
            foreach (var child in _children) {
                AppendChild(res, ast, format, child);
            }
        }

        private static void AppendChild(StringBuilder res, PythonAst ast, CodeFormattingOptions format, Node child) {
            if (child is ConstantExpression expr) {
                // Non-Verbatim AppendCodeString for ConstantExpression adds quotes around string
                // Remove those quotes
                var childStrBuilder = new StringBuilder();
                child.AppendCodeString(childStrBuilder, ast, format);
                res.Append(childStrBuilder.ToString().Trim('\''));
            } else {
                child.AppendCodeString(res, ast, format);
            }
        }
    }
}
