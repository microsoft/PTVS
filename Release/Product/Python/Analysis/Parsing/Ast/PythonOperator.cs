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
            return "";
        }
    }
}
