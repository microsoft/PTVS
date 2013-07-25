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
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]
    class ProvidePythonExecutionModeAttribute : RegistrationAttribute {
        private readonly string _friendlyName, _typeName, _modeID;
        private readonly bool _supportsMultipleScopes, _supportsMultipleCompleteStatementInputs;

        /// <summary>
        /// Provides a new REPL execution mode.  This registered a friendly name w/ a module which
        /// implements the REPL backend interface.
        /// </summary>
        /// <param name="modeID">The internal ID of the mode</param>
        /// <param name="friendlyName">The friendly display name of the mode</param>
        /// <param name="typeName">The fully qualified name of the class which implements the REPL backend</param>
        /// <param name="supportsMultipleScopes">True if the REPL supports executing in multiple scopes</param>
        /// <param name="supportsMultipleCompleteStatementInputs">True if the REPL evaluator can parse and execute inputs which consist of multiple complete statements</param>
        public ProvidePythonExecutionModeAttribute(string modeID, string friendlyName, string typeName, bool supportsMultipleScopes = true, bool supportsMultipleCompleteStatementInputs = false) {
            _modeID = modeID;
            _friendlyName = friendlyName;
            _typeName = typeName;
            _supportsMultipleScopes = supportsMultipleScopes;
            _supportsMultipleCompleteStatementInputs = supportsMultipleCompleteStatementInputs;
        }

        public override void Register(RegistrationContext context) {
            // ExecutionMode is structured like:
            // HKLM\Software\VisualStudio\Hive\PythonTools:
            //      ReplExecutionModes\
            //          ModeID\
            //              Type
            //              FriendlyName
            //              SupportsMultipleScopes
            //              SupportsMultipleCompleteStatementInputs
            //                
            using (var engineKey = context.CreateKey(PythonInteractiveOptionsControl.PythonExecutionModeKey)) {
                using (var subKey = engineKey.CreateSubkey(_modeID)) {
                    subKey.SetValue("Type", _typeName);
                    subKey.SetValue("FriendlyName", _friendlyName);
                    subKey.SetValue("SupportsMultipleScopes", _supportsMultipleScopes.ToString());
                    subKey.SetValue("SupportsMultipleCompleteStatementInputs", _supportsMultipleCompleteStatementInputs.ToString());
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
