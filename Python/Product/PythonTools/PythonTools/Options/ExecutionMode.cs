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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Options {
    class ExecutionMode {
        public readonly string Id;
        public readonly string Type;
        public readonly string FriendlyName;
        public readonly bool SupportsMultipleScopes, SupportsMultipleCompleteStatementInputs;
        public const string StandardModeId = "{B542C3C6-ED9B-4C10-91C8-6EBAD6907BA0}";

        public ExecutionMode(string modeID, string type, string friendlyName, bool supportsMultipleScopes, bool supportsMultipleCompleteStatementInputs) {
            Id = modeID;
            Type = type;
            FriendlyName = friendlyName;
            SupportsMultipleScopes = supportsMultipleScopes;
            SupportsMultipleCompleteStatementInputs = supportsMultipleCompleteStatementInputs;
        }

        public static ExecutionMode[] GetRegisteredModes(IServiceProvider serviceProvider) {
            List<ExecutionMode> res = new List<ExecutionMode>();

            // ExecutionMode is structured like:
            // HKLM\Software\VisualStudio\Hive\PythonTools:
            //      ReplExecutionModes\
            //          ModeID\
            //              Type
            //              FriendlyName
            //              SupportsMultipleScopes
            //              SupportsMultipleCompleteStatementInputs
            //  
            var settingsManager = PythonToolsPackage.GetSettings(serviceProvider);
            var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.Configuration);
            var itemCount = store.GetSubCollectionCount(PythonInteractiveOptionsControl.PythonExecutionModeKey);
            foreach (string modeID in store.GetSubCollectionNames(PythonInteractiveOptionsControl.PythonExecutionModeKey)) {
                var value = store.GetString(PythonInteractiveOptionsControl.PythonExecutionModeKey + "\\" + modeID, "SupportsMultipleScopes", "True");
                bool multipleScopes;
                if (!Boolean.TryParse(value, out multipleScopes)) {
                    multipleScopes = true;
                }

                value = store.GetString(PythonInteractiveOptionsControl.PythonExecutionModeKey + "\\" + modeID, "SupportsMultipleScopes", "True");
                bool supportsMultipleCompleteStatementInputs;
                if (!Boolean.TryParse(value, out supportsMultipleCompleteStatementInputs)) {
                    supportsMultipleCompleteStatementInputs = false;
                }

                var type = store.GetString(PythonInteractiveOptionsControl.PythonExecutionModeKey + "\\" + modeID, "Type");
                var friendlyName = store.GetString(PythonInteractiveOptionsControl.PythonExecutionModeKey + "\\" + modeID, "FriendlyName");
                res.Add(
                    new ExecutionMode(
                        modeID,
                        type,
                        friendlyName,
                        multipleScopes,
                        supportsMultipleCompleteStatementInputs
                    )
                );

            }
            res.Sort((x, y) => String.Compare(x.FriendlyName, y.FriendlyName, true));
            return res.ToArray();
        }
    }
}
