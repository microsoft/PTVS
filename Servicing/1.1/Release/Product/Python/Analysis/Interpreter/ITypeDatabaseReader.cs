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

namespace Microsoft.PythonTools.Interpreter {
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
        void ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container, PythonTypeDatabase instanceDb = null);
        void RunFixups();
        void LookupType(object type, Action<IPythonType, bool> assign, PythonTypeDatabase instanceDb = null);
        string GetBuiltinTypeName(BuiltinTypeId id);
    }
}
