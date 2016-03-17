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
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Options {
    //class ExecutionMode {
    //    public readonly string Id;
    //    public readonly string Type;
    //    public readonly string FriendlyName;
    //    public readonly bool SupportsMultipleScopes, SupportsMultipleCompleteStatementInputs;
    //    public const string StandardModeId = "{B542C3C6-ED9B-4C10-91C8-6EBAD6907BA0}";

    //    public ExecutionMode(string modeID, string type, string friendlyName, bool supportsMultipleScopes, bool supportsMultipleCompleteStatementInputs) {
    //        Id = modeID;
    //        Type = type;
    //        FriendlyName = friendlyName;
    //        SupportsMultipleScopes = supportsMultipleScopes;
    //        SupportsMultipleCompleteStatementInputs = supportsMultipleCompleteStatementInputs;
    //    }

    //    public static ExecutionMode[] GetRegisteredModes(IServiceProvider serviceProvider) {
    //        List<ExecutionMode> res = new List<ExecutionMode>();

    //        // ExecutionMode is structured like:
    //        // HKLM\Software\VisualStudio\Hive\PythonTools:
    //        //      ReplExecutionModes\
    //        //          ModeID\
    //        //              Type
    //        //              FriendlyName
    //        //              SupportsMultipleScopes
    //        //              SupportsMultipleCompleteStatementInputs
    //        //  
    //        var settingsManager = PythonToolsPackage.GetSettings(serviceProvider);
    //        var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.Configuration);
    //        var itemCount = store.GetSubCollectionCount(PythonInteractiveOptionsControlHost.PythonExecutionModeKey);
    //        foreach (string modeID in store.GetSubCollectionNames(PythonInteractiveOptionsControlHost.PythonExecutionModeKey)) {
    //            var value = store.GetString(PythonInteractiveOptionsControlHost.PythonExecutionModeKey + "\\" + modeID, "SupportsMultipleScopes", "True");
    //            bool multipleScopes;
    //            if (!Boolean.TryParse(value, out multipleScopes)) {
    //                multipleScopes = true;
    //            }

    //            value = store.GetString(PythonInteractiveOptionsControlHost.PythonExecutionModeKey + "\\" + modeID, "SupportsMultipleCompleteStatementInputs", "True");
    //            bool supportsMultipleCompleteStatementInputs;
    //            if (!Boolean.TryParse(value, out supportsMultipleCompleteStatementInputs)) {
    //                supportsMultipleCompleteStatementInputs = false;
    //            }

    //            var type = store.GetString(PythonInteractiveOptionsControlHost.PythonExecutionModeKey + "\\" + modeID, "Type");
    //            var friendlyName = store.GetString(PythonInteractiveOptionsControlHost.PythonExecutionModeKey + "\\" + modeID, "FriendlyName");
    //            res.Add(
    //                new ExecutionMode(
    //                    modeID,
    //                    type,
    //                    friendlyName,
    //                    multipleScopes,
    //                    supportsMultipleCompleteStatementInputs
    //                )
    //            );

    //        }
    //        res.Sort((x, y) => String.Compare(x.FriendlyName, y.FriendlyName, true));
    //        return res.ToArray();
    //    }
    //}
}
