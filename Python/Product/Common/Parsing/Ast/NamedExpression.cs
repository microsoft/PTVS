using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class NamedExpression : Expression {
        public NamedExpression(Expression lhs, Expression rhs) {
            Target = lhs;
            Value = rhs;
        }

        public Expression Target { get; }
        public Expression Value { get; }

        public override string NodeName => "named expression";

        public override IEnumerable<Node> GetChildNodes() {
            yield return Target;
            if (Value != null) yield return Value;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target.Walk(walker);
                Value.Walk(walker);
            }
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this)) {
                await Target.WalkAsync(walker);
                await Value.WalkAsync(walker);
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            res.Append(" := ");
            Value.AppendCodeString(res, ast, format);
        }
    }
}
