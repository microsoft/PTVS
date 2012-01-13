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

namespace Microsoft.PythonTools.Options {
    class ExecutionMode {
        public readonly string Id;
        public readonly string Type;
        public readonly string FriendlyName;
        public readonly bool SupportsMultipleScopes;
        public const string StandardModeId = "{B542C3C6-ED9B-4C10-91C8-6EBAD6907BA0}";

        public ExecutionMode(string modeID, string type, string friendlyName, bool multipleScopes) {
            Id = modeID;
            Type = type;
            FriendlyName = friendlyName;
            SupportsMultipleScopes = multipleScopes;
        }

        public static ExecutionMode[] GetRegisteredModes() {
            List<ExecutionMode> res = new List<ExecutionMode>();

            // ExecutionMode is structured like:
            // HKLM\Software\VisualStudio\Hive\PythonTools:
            //      ReplExecutionModes\
            //          ModeID\
            //              Type
            //              FriendlyName
            //              SupportsMultipleScopes
            //  
            var key = PythonToolsPackage.ApplicationRegistryRoot.OpenSubKey(PythonInteractiveOptionsControl.PythonExecutionModeKey);
            if (key != null) {
                foreach (string modeID in key.GetSubKeyNames()) {
                    var modeKey = key.OpenSubKey(modeID);

                    bool multipleScopes;
                    if (!Boolean.TryParse(modeKey.GetValue("SupportsMultipleScopes", "True").ToString(), out multipleScopes)) {
                        multipleScopes = true;
                    }

                    res.Add(
                        new ExecutionMode(
                            modeID,
                            modeKey.GetValue("Type").ToString(),
                            modeKey.GetValue("FriendlyName").ToString(),
                            multipleScopes
                        )
                    );
                }
            }
            res.Sort((x, y) => String.Compare(x.FriendlyName, y.FriendlyName, true));
            return res.ToArray();
        }
    }
}
