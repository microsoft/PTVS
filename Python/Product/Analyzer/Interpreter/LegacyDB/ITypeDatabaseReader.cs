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

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    /// <summary>
    /// Common internal interface shared between SharedDatabaseState and PythonTypeDatabase.
    /// 
    /// This interface enables splitting of the type database into two portions.  The first is our cached
    /// type database for an interpreter, its standard library, and all of site-packages.  The second
    /// portion is per-project cached intellisense - currently only used for caching the intellisense
    /// against a referenced extension module (.pyd).
    /// 
    /// When 
    /// </summary>
    interface ITypeDatabaseReader {
        void ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container);
        void LookupType(object type, Action<IPythonType> assign);
        string GetBuiltinTypeName(BuiltinTypeId id);
        void OnDatabaseCorrupt();

        bool BeginModuleLoad(IPythonModule module, int millisecondsTimeout);
        void EndModuleLoad(IPythonModule module);
    }
}
