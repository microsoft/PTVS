/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    internal class QuickInfoSource : IQuickInfoSource {
        private readonly ITextBuffer _textBuffer;
        private readonly QuickInfoSourceProvider _provider;
        private IQuickInfoSession _curSession;

        public QuickInfoSource(QuickInfoSourceProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        #region IQuickInfoSource Members

        public void AugmentQuickInfoSession(IQuickInfoSession session, System.Collections.Generic.IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            if (_curSession != null && !_curSession.IsDismissed) {
                _curSession.Dismiss();
                _curSession = null;
            }

            _curSession = session;
            _curSession.Dismissed += CurSessionDismissed;

            var vars = _textBuffer.CurrentSnapshot.AnalyzeExpression(
                _provider._serviceProvider,
                session.CreateTrackingSpan(_textBuffer),
                false
            );

            AugmentQuickInfoWorker(vars, quickInfoContent, out applicableToSpan);
        }

        private void CurSessionDismissed(object sender, EventArgs e) {
            _curSession = null;
        }

        internal static void AugmentQuickInfoWorker(ExpressionAnalysis vars, System.Collections.Generic.IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            applicableToSpan = vars.Span;
            if (applicableToSpan == null || String.IsNullOrWhiteSpace(vars.Expression)) {
                return;
            }

            bool first = true;
            var result = new StringBuilder();
            int count = 0;
            List<AnalysisValue> listVars = new List<AnalysisValue>(vars.Values);
            HashSet<string> descriptions = new HashSet<string>();
            bool multiline = false;
            foreach (var v in listVars) {
                string description = null;
                if (listVars.Count == 1) {
                    if (!String.IsNullOrWhiteSpace(v.Description)) {
                        description = v.Description;
                    }
                } else {
                    if (!String.IsNullOrWhiteSpace(v.ShortDescription)) {
                        description = v.ShortDescription;
                    }
                }

                description = description.LimitLines();

                if (description != null && descriptions.Add(description)) {
                    if (first) {
                        first = false;
                    } else {
                        if (result.Length == 0 || result[result.Length - 1] != '\n') {
                            result.Append(", ");
                        } else {
                            multiline = true;
                        }
                    }
                    result.Append(description);
                    count++;
                }
            }

            string expr = vars.Expression;
            if (expr.Length > 4096) {
                expr = expr.Substring(0, 4093) + "...";
            }
            if (multiline) {
                result.Insert(0, expr + ": " + Environment.NewLine);
            } else if (result.Length > 0) {
                result.Insert(0, expr + ": ");
            } else {
                result.Append(expr);
                result.Append(": ");
                result.Append("<unknown type>");
            }

            quickInfoContent.Add(result.ToString());
        }

        #endregion

        public void Dispose() {
        }

    }
}
