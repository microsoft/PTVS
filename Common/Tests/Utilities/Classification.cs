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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Classification;

namespace TestUtilities {
    public class Classification {
        public readonly int Start, End;
        public readonly string Text;
        public readonly string ClassificationType;

        public Classification(string classificationType, int start, int end, string text) {
            ClassificationType = classificationType;
            Start = start;
            End = end;
            Text = text;
        }

        public static void Verify(IList<ClassificationSpan> spans, params Classification[] expected) {
            bool passed = false;
            try {
                Assert.AreEqual(expected.Length, spans.Count);

                for (int i = 0; i < spans.Count; i++) {
                    var curSpan = spans[i];


                    int start = curSpan.Span.Start.Position;
                    int end = curSpan.Span.End.Position;

                    Assert.AreEqual(expected[i].Start, start);
                    Assert.AreEqual(expected[i].End, end);
                    Assert.AreEqual(expected[i].Text, curSpan.Span.GetText());
                    Assert.AreEqual(expected[i].ClassificationType, curSpan.ClassificationType.Classification);
                }
                passed = true;
            } finally {
                if (!passed) {
                    // output results for easy test creation...
                    Console.WriteLine(
                        String.Join(",\r\n",
                            spans.Select(curSpan =>
                                String.Format("new Classification(\"{0}\", {1}, {2}, \"{3}\")",
                                    curSpan.ClassificationType.Classification,
                                    curSpan.Span.Start.Position,
                                    curSpan.Span.End.Position,
                                    FormatString(curSpan.Span.GetText())
                                )
                            )
                        )
                    );
                }
            }
        }

        public static string FormatString(string p) {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < p.Length; i++) {
                switch (p[i]) {
                    case '\\': res.Append("\\\\"); break;
                    case '\n': res.Append("\\n"); break;
                    case '\r': res.Append("\\r"); break;
                    case '\t': res.Append("\\t"); break;
                    case '"': res.Append("\\\""); break;
                    default: res.Append(p[i]); break;
                }
            }
            return res.ToString();
        }

    }

}
