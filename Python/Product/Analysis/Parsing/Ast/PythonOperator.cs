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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Parsing {
    public enum PythonOperator {
        None,

        // Unary
        Not,
        Pos,
        Invert,
        Negate,

        // Binary

        Add,
        Subtract,
        Multiply,
        Divide,
        TrueDivide,
        Mod,
        BitwiseAnd,
        BitwiseOr,
        Xor,
        LeftShift,
        RightShift,
        Power,
        FloorDivide,

        // Comparisons

        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        In,
        NotIn,
        IsNot,
        Is,

        // Matrix Multiplication (new in 2.2)
        MatMultiply,

        // Aliases
        ExclusiveOr = Xor,
        Equals = Equal,
        NotEquals = NotEqual,
    }

    internal static class PythonOperatorExtensions {
        internal static string ToCodeString(this PythonOperator self) {
            switch (self) {
                case PythonOperator.Not: return "not";
                case PythonOperator.Pos: return "+";
                case PythonOperator.Invert: return "~";
                case PythonOperator.Negate: return "-";
                case PythonOperator.Add: return "+";
                case PythonOperator.Subtract: return "-";
                case PythonOperator.Multiply: return "*";
                case PythonOperator.MatMultiply: return "@";
                case PythonOperator.Divide: return "/";
                case PythonOperator.TrueDivide: return "/";
                case PythonOperator.Mod: return "%";
                case PythonOperator.BitwiseAnd: return "&";
                case PythonOperator.BitwiseOr: return "|";
                case PythonOperator.Xor: return "^";
                case PythonOperator.LeftShift: return "<<";
                case PythonOperator.RightShift: return ">>";
                case PythonOperator.Power: return "**";
                case PythonOperator.FloorDivide: return "//";
                case PythonOperator.LessThan: return "<";
                case PythonOperator.LessThanOrEqual: return "<=";
                case PythonOperator.GreaterThan: return ">";
                case PythonOperator.GreaterThanOrEqual: return ">=";
                case PythonOperator.Equal: return "==";
                case PythonOperator.NotEqual: return "!=";
                case PythonOperator.In: return "in";
                case PythonOperator.NotIn: return "not in";
                case PythonOperator.IsNot: return "is not";
                case PythonOperator.Is: return "is";
            }
            return string.Empty;
        }

        internal static bool IsComparison(this PythonOperator self) {
            return self == PythonOperator.LessThan ||
                    self == PythonOperator.LessThanOrEqual ||
                    self == PythonOperator.GreaterThan ||
                    self == PythonOperator.GreaterThanOrEqual ||
                    self == PythonOperator.Equal ||
                    self == PythonOperator.NotEqual ||
                    self == PythonOperator.In ||
                    self == PythonOperator.NotIn ||
                    self == PythonOperator.IsNot ||
                    self == PythonOperator.Is;
        }
    }
}
