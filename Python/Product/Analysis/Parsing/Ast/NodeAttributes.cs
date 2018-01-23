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


using System;

namespace Microsoft.PythonTools.Parsing.Ast {
    internal static class NodeAttributes {
        /// <summary>
        /// Value is a string which proceeds a token in the node.
        /// </summary>
        public static readonly object PreceedingWhiteSpace = new object();
        /// <summary>
        /// Value is a string which proceeds a second token in the node.
        /// </summary>
        public static readonly object SecondPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a third token in the node.
        /// </summary>
        public static readonly object ThirdPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a fourth token in the node.
        /// </summary>
        public static readonly object FourthPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is a string which proceeds a fifth token in the node.
        /// </summary>
        public static readonly object FifthPreceedingWhiteSpace = new object();

        /// <summary>
        /// Value is an array of strings which proceeed items in the node.
        /// </summary>
        public static readonly object ListWhiteSpace = new object();

        /// <summary>
        /// Value is an array of strings which proceeed items names in the node.
        /// </summary>
        public static readonly object NamesWhiteSpace = new object();

        /// <summary>
        /// Value is a string which is the name as it appeared verbatim in the source code (for mangled name).
        /// </summary>
        public static readonly object VerbatimImage = new object();

        /// <summary>
        /// Value is a string which represents extra node specific verbatim text.
        /// </summary>
        public static readonly object ExtraVerbatimText = new object();

        /// <summary>
        /// The tuple expression was constructed without parenthesis.  The value doesn't matter, only the
        /// presence of the metadata indicates the value is set.
        /// </summary>
        public static readonly object IsAltFormValue = new object();

        /// <summary>
        /// Provides an array of strings which are used for verbatim names when multiple names are
        /// involved (e.g. del, global, import, etc...)
        /// </summary>
        public static readonly object VerbatimNames = new object();

        /// <summary>
        /// The node is missing a closing grouping (close paren, close brace, close bracket).
        /// </summary>
        public static readonly object ErrorMissingCloseGrouping = new object();

        /// <summary>
        /// The node is incomplete.  Where the node ends is on a node-by-node basis but it's
        /// well defined for each individual node.
        /// </summary>
        public static readonly object ErrorIncompleteNode = new object();

        public static readonly object VariableReference = new object();

        public static readonly object Variable = new object();
       
        public static void AddVariableReference(this Node node, PythonAst ast, bool bindNames, object reference) {
            if (bindNames) {
                ast.SetAttribute(node, VariableReference, reference);
            }
        }

        public static void AddVariable(this Parameter node, PythonAst ast, bool bindNames, PythonVariable variable) {
            if (bindNames) {
                ast.SetAttribute(node, Variable, variable);
            }
        }

        public static string GetPreceedingWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.PreceedingWhiteSpace);
        }

        public static string GetPreceedingWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.PreceedingWhiteSpace, null);
        }

        internal static string GetWhiteSpace(Node node, PythonAst ast, object kind, string defaultValue = " ") {
            object whitespace;
            if (ast.TryGetAttribute(node, kind, out whitespace)) {
                return (string)whitespace;
            } else {
                return defaultValue;
            }
        }

        public static string GetSecondWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.SecondPreceedingWhiteSpace);
        }

        public static string GetSecondWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.SecondPreceedingWhiteSpace, null);
        }

        public static string GetThirdWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.ThirdPreceedingWhiteSpace);
        }

        public static string GetThirdWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.ThirdPreceedingWhiteSpace, null);
        }

        public static string GetFourthWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.FourthPreceedingWhiteSpace);
        }

        public static string GetFourthWhiteSpaceDefaultNull(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.FourthPreceedingWhiteSpace, null);
        }

        public static string GetFifthWhiteSpace(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.FifthPreceedingWhiteSpace);
        }

        public static string GetExtraVerbatimText(this Node node, PythonAst ast) {
            return GetWhiteSpace(node, ast, NodeAttributes.ExtraVerbatimText, null);
        }

        public static bool IsAltForm(this Node node, PythonAst ast) {
            object dummy;
            if (ast.TryGetAttribute(node, NodeAttributes.IsAltFormValue, out dummy)) {
                return true;
            } else {
                return false;
            }
        }

        public static bool IsMissingCloseGrouping(this Node node, PythonAst ast) {
            object dummy;
            if (ast.TryGetAttribute(node, NodeAttributes.ErrorMissingCloseGrouping, out dummy)) {
                return true;
            } else {
                return false;
            }
        }

        public static bool IsIncompleteNode(this Node node, PythonAst ast) {
            object dummy;
            if (ast.TryGetAttribute(node, NodeAttributes.ErrorIncompleteNode, out dummy)) {
                return true;
            } else {
                return false;
            }
        }

        public static string[] GetListWhiteSpace(this Node node, PythonAst ast) {
            object whitespace;
            if (ast.TryGetAttribute(node, NodeAttributes.ListWhiteSpace, out whitespace)) {
                return (string[])whitespace;
            } else {
                return null;
            }
        }

        public static string[] GetNamesWhiteSpace(this Node node, PythonAst ast) {
            object whitespace;
            if (ast.TryGetAttribute(node, NodeAttributes.NamesWhiteSpace, out whitespace)) {
                return (string[])whitespace;
            } else {
                return null;
            }
        }

        public static string[] GetVerbatimNames(this Node node, PythonAst ast) {
            object names;
            if (ast.TryGetAttribute(node, NodeAttributes.VerbatimNames, out names)) {
                return (string[])names;
            } else {
                return null;
            }
        }

        public static string GetVerbatimImage(this Node node, PythonAst ast) {
            object image;
            if (ast.TryGetAttribute(node, NodeAttributes.VerbatimImage, out image)) {
                return (string)image;
            } else {
                return null;
            }
        }
    }
}
