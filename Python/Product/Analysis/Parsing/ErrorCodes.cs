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
    public static class ErrorCodes {
        // The error flags
        public const int IncompleteMask = 0x000F;

        /// <summary>
        /// The error involved an incomplete statement due to an unexpected EOF.
        /// </summary>
        public const int IncompleteStatement = 0x0001;

        /// <summary>
        /// The error involved an incomplete token.
        /// </summary>
        public const int IncompleteToken = 0x0002;

        /// <summary>
        /// The mask for the actual error values 
        /// </summary>
        public const int ErrorMask = 0x7FFFFFF0;

        /// <summary>
        /// The error was a general syntax error
        /// </summary>
        public const int SyntaxError = 0x0010;              

        /// <summary>
        /// The error was an indentation error.
        /// </summary>
        public const int IndentationError = 0x0020;      

        /// <summary>
        /// The error was a tab error.
        /// </summary>
        public const int TabError = 0x0030;

        /// <summary>
        /// syntax error shouldn't include a caret (no column offset should be included)
        /// </summary>
        public const int NoCaret = 0x0040;

    }
}
