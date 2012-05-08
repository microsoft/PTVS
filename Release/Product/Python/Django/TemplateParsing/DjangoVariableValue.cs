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
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Captures a Django value used in a variable expression.  The value can either be an expression (foo, foo.bar)
    /// with a kind of Variable, a string literal ('foo') of kind Constant, or a numeric value (10, 10.0, -5) of
    /// kind Constant.  
    /// </summary>
    class DjangoVariableValue {
        public readonly string Value;
        public readonly DjangoVariableKind Kind;

        public DjangoVariableValue(string value, DjangoVariableKind kind) {
            Value = value;
            Kind = kind;
        }

        public IEnumerable<BlockClassification> GetSpans(int start) {
            Classification? filterType = null;
            switch (Kind) {
                case DjangoVariableKind.Constant: filterType = Classification.Literal; break;
                case DjangoVariableKind.Number: filterType = Classification.Number; break;
                case DjangoVariableKind.Variable:
                    // variable can have dots in it...

                    if (Value.IndexOf('.') != -1) {
                        var split = Value.Split('.');
                        for (int i = 0; i < split.Length; i++) {
                            yield return new BlockClassification(
                                new Span(start, split[i].Length),
                                Classification.Identifier
                            );
                            start += split[i].Length;
                            yield return new BlockClassification(
                                new Span(start, 1),
                                Classification.Dot
                            );
                            start += 1;
                        }
                    } else {
                        filterType = Classification.Identifier;
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (filterType != null) {
                yield return new BlockClassification(
                    new Span(start, Value.Length),
                    filterType.Value
                );
            }
        }
    }

    enum DjangoVariableKind {
        None,
        Variable,
        Constant,
        Number
    }
}
