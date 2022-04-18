using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class FString : Expression {
        private readonly Node[] _children;
        private readonly string _openQuotes;

        public FString(Node[] children, string openQuotes, string unparsed) {
            _children = children;
            _openQuotes = openQuotes;
            Unparsed = unparsed;
        }

        public override IEnumerable<Node> GetChildNodes() {
            return _children;
        }

        public readonly string Unparsed;

        public override string NodeName => "f-string expression";

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
            var verbatimPieces = this.GetVerbatimNames(ast);
            var verbatimComments = this.GetListWhiteSpace(ast);
            if (verbatimPieces != null) {
                // string+ / bytes+, such as "abc" "abc", which can spawn multiple lines, and 
                // have comments in between the peices.
                for (var i = 0; i < verbatimPieces.Length; i++) {
                    if (verbatimComments != null && i < verbatimComments.Length) {
                        format.ReflowComment(res, verbatimComments[i]);
                    }
                    res.Append(verbatimPieces[i]);
                }
            } else {
                format.ReflowComment(res, this.GetPreceedingWhiteSpaceDefaultNull(ast));
                if (this.GetExtraVerbatimText(ast) != null) {
                    res.Append(this.GetExtraVerbatimText(ast));
                } else {
                    RecursiveAppendRepr(res, ast, format);
                }
            }
        }

        private void RecursiveAppendRepr(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            res.Append('f');
            res.Append(_openQuotes);
            foreach (var child in _children) {
                AppendChild(res, ast, format, child);
            }
            res.Append(_openQuotes);
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
