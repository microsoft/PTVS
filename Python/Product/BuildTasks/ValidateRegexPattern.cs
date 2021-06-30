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

using System;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.PythonTools.BuildTasks {
    /// <summary>
    /// Checks whether a pattern is valid regex.
    /// </summary>
    public class ValidateRegexPattern : Task {
        /// <summary>
        /// The pattern to validate.
        /// </summary>
        [Required]
        public string Pattern { get; set; }

        /// <summary>
        /// The message to display if the pattern is invalid. If the message
        /// contains "{0}", it will be replaced with the exception message.
        /// 
        /// If the message is empty, no error will be raised.
        /// </summary>
        public string Message { get; set; }

        [Output]
        public string IsValid { get; private set; }

        public override bool Execute() {
            try {
                var regex = new Regex(Pattern);
                IsValid = bool.TrueString;
            } catch (ArgumentException ex) {
                IsValid = bool.FalseString;
                if (!string.IsNullOrEmpty(Message)) {
                    Log.LogError(string.Format(Message, ex.Message));
                }
            }
            return !Log.HasLoggedErrors;
        }
    }
}
