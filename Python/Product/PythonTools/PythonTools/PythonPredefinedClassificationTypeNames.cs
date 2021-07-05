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


namespace Microsoft.PythonTools {
    static class PythonPredefinedClassificationTypeNames {
        /// <summary>
        /// Open grouping classification.  Used for (, [, {, ), ], and }...  A subtype of the Python
        /// operator grouping.
        /// </summary>
        public const string Grouping = "Python grouping";

        /// <summary>
        /// Classification used for comma characters when used outside of a literal, comment, etc...
        /// </summary>
        public const string Comma = "Python comma";

        /// <summary>
        /// Classification used for . characters when used outside of a literal, comment, etc...
        /// </summary>
        public const string Dot = "Python dot";

        /// <summary>
        /// Classification used for all other operators
        /// </summary>
        public const string Operator = "Python operator";

        /// <summary>
        /// Classification used for classes/types.
        /// </summary>
        public const string Class = "Python class";

        /// <summary>
        /// Classification used for imported modules.
        /// </summary>
        public const string Module = "Python module";

        /// <summary>
        /// Classification used for functions.
        /// </summary>
        public const string Function = "Python function";

        /// <summary>
        /// Classification used for parameters.
        /// </summary>
        public const string Parameter = "Python parameter";

        /// <summary>
        /// Classification used for builtins.
        /// </summary>
        public const string Builtin = "Python builtin";

        /// <summary>
        /// Classification used for docstrings
        /// </summary>
        public const string Documentation = "Python documentation";

        /// <summary>
        /// Classification used for regular expressions
        /// </summary>
        public const string RegularExpression = "Python regex";
    }
}
