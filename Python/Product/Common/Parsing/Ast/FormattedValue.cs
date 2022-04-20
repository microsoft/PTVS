using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class FormattedValue : Node {
        public FormattedValue(Expression value, char? conversion, Expression formatSpecifier) {
            Value = value;
            FormatSpecifier = formatSpecifier;
            Conversion = conversion;
        }

        public Expression Value { get; }
        public Expression FormatSpecifier { get; }
        public char? Conversion { get; }

        public override IEnumerable<Node> GetChildNodes() {
            yield return Value;
            if (FormatSpecifier != null) {
                yield return FormatSpecifier;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value.Walk(walker);
                FormatSpecifier?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public async override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                await Value.WalkAsync(walker, cancellationToken);
                await FormatSpecifier?.WalkAsync(walker, cancellationToken);
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            res.Append('{');
            Value.AppendCodeString(res, ast, format);
            if (Conversion.HasValue) {
                res.Append('!');
                res.Append(Conversion.Value);
            }
            if (FormatSpecifier != null) {
                res.Append(':');
                FormatSpecifier.AppendCodeString(res, ast, format);
            }
            res.Append('}');
        }
    }
}
