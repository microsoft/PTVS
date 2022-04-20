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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class FromImportStatement : Statement {
        public FromImportStatement(ModuleName/*!*/ root, ImmutableArray<NameExpression> names, ImmutableArray<NameExpression> asNames, bool fromFuture, bool forceAbsolute, int importIndex) {
            Root = root;
            Names = names;
            AsNames = asNames;
            IsFromFuture = fromFuture;
            ForceAbsolute = forceAbsolute;
            ImportIndex = importIndex;
        }

        public ModuleName/*!*/ Root { get; }
        public ImmutableArray<NameExpression> Names { get; }
        public ImmutableArray<NameExpression> AsNames { get; }
        public bool IsFromFuture { get; }
        public bool ForceAbsolute { get; }
        public int ImportIndex { get; }

        public override int KeywordLength => 4;

        /// <summary>
        /// Gets the variables associated with each imported name.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays",
            Justification = "breaking change")]
        public PythonVariable[] Variables { get; set; }

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
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("from");
            Root.AppendCodeString(res, ast, format);

            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("import");
                if (!this.IsAltForm(ast)) {
                    res.Append(this.GetThirdWhiteSpace(ast));
                    res.Append('(');
                }

                var asNameWhiteSpace = this.GetNamesWhiteSpace(ast);
                var asIndex = 0;
                for (var i = 0; i < Names.Count; i++) {
                    if (i > 0) {
                        if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                            res.Append(asNameWhiteSpace[asIndex++]);
                        }
                        res.Append(',');
                    }

                    if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                        res.Append(asNameWhiteSpace[asIndex++]);
                    } else {
                        res.Append(' ');
                    }

                    Names[i].AppendCodeString(res, ast, format);
                    if (AsNames[i] != null) {
                        if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                            res.Append(asNameWhiteSpace[asIndex++]);
                        }
                        res.Append("as");
                        if (AsNames[i].Name.Length != 0) {
                            if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                                res.Append(asNameWhiteSpace[asIndex++]);
                            }
                            AsNames[i].AppendCodeString(res, ast, format);
                        } else {
                            asIndex++;
                        }
                    }
                }

                if (asNameWhiteSpace != null && asIndex < asNameWhiteSpace.Length) {
                    // trailing comma
                    res.Append(asNameWhiteSpace[asNameWhiteSpace.Length - 1]);
                    res.Append(",");
                }

                if (!this.IsAltForm(ast) && !this.IsMissingCloseGrouping(ast)) {
                    res.Append(this.GetFourthWhiteSpace(ast));
                    res.Append(')');
                }
            }
        }
    }
}
