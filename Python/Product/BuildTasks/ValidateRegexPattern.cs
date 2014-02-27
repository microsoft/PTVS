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
