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
        private readonly bool _multipleScopes;

        public ProvidePythonExecutionModeAttribute(string modeID, string friendlyName, string typeName, bool multipleScopes = true) {
            _modeID = modeID;
            _friendlyName = friendlyName;
            _typeName = typeName;
            _multipleScopes = multipleScopes;
        }

        public override void Register(RegistrationContext context) {
            // ExecutionMode is structured like:
            // HKLM\Software\VisualStudio\Hive\PythonTools:
            //      ReplExecutionModes\
            //          ModeID\
            //              Type
            //              FriendlyName
            //              SupportsMultipleScopes
            //                
            using (var engineKey = context.CreateKey(PythonInteractiveOptionsControl.PythonExecutionModeKey)) {
                using (var subKey = engineKey.CreateSubkey(_modeID)) {
                    subKey.SetValue("Type", _typeName);
                    subKey.SetValue("FriendlyName", _friendlyName);
                    subKey.SetValue("SupportsMultipleScopes", _multipleScopes.ToString());
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
