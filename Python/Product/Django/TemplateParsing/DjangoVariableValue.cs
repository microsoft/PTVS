// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Captures a Django value used in a variable expression.  The value can either be an expression (fob, fob.oar)
    /// with a kind of Variable, a string literal ('fob') of kind Constant, or a numeric value (10, 10.0, -5) of
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
                            if (i != split.Length - 1) {
                                yield return new BlockClassification(
                                    new Span(start, 1),
                                    Classification.Dot
                                );
                            }
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
