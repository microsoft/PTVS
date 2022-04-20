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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class ImportStatement : Statement {
        public ImportStatement(ImmutableArray<ModuleName> names, ImmutableArray<NameExpression> asNames, bool forceAbsolute) {
            Names = names;
            AsNames = asNames;
            ForceAbsolute = forceAbsolute;
        }

        public bool ForceAbsolute { get; }

        public override int KeywordLength => 6;

        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "breaking change")]
        public PythonVariable[] Variables { get; set; }

        public ImmutableArray<ModuleName> Names { get; }
        public ImmutableArray<NameExpression> AsNames { get; }

        // TODO: return names and aliases when they are united into one node
        public override IEnumerable<Node> GetChildNodes() => Enumerable.Empty<Node>();

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await walker.WalkAsync(this, cancellationToken)) {
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (format.ReplaceMultipleImportsWithMultipleStatements) {
                var proceeding = this.GetPreceedingWhiteSpace(ast);
                var additionalProceeding = format.GetNextLineProceedingText(proceeding);

                for (int i = 0, asIndex = 0; i < Names.Count; i++) {
                    if (i == 0) {
                        format.ReflowComment(res, proceeding);
                    } else {
                        res.Append(additionalProceeding);
                    }
                    res.Append("import");

                    Names[i].AppendCodeString(res, ast, format);
                    AppendAs(res, ast, format, asNameWhiteSpace, i, ref asIndex);
                }
                return;
            } else {
                format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                res.Append("import");

                var itemWhiteSpace = this.GetListWhiteSpace(ast);
                for (int i = 0, asIndex = 0; i < Names.Count; i++) {
                    if (i > 0 && itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[i - 1]);
                        res.Append(',');
                    }

                    Names[i].AppendCodeString(res, ast, format);
                    AppendAs(res, ast, format, asNameWhiteSpace, i, ref asIndex);
                }
            }
        }

        private void AppendAs(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string[] asNameWhiteSpace, int i, ref int asIndex) {
            if (AsNames[i] != null) {
                if (asNameWhiteSpace != null) {
                    res.Append(asNameWhiteSpace[asIndex++]);
                }
                res.Append("as");

                if (AsNames[i].Name.Length != 0) {
                    if (asNameWhiteSpace != null) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    }

                    AsNames[i].AppendCodeString(res, ast, format);
                }
            }
        }
    }
}
