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
using System.DirectoryServices;
using System.Runtime.InteropServices;

namespace Microsoft.PythonTools.Hpc {
    class Cluster {
        public static string[] GetClusters() {
            List<string> names = new List<string>();
            // Search the directory for MicrosoftComputeCluster ServiceConnectionPoints
            using (DirectorySearcher mySearcher = new DirectorySearcher()) {
                String propvalue = "servicednsname";
                mySearcher.Filter = ("(&(objectClass=ServiceConnectionPoint)(serviceClassName=MicrosoftComputeCluster)(keywords=Version2))");
                mySearcher.PropertiesToLoad.Add(propvalue);

                try {
                    foreach (SearchResult result in mySearcher.FindAll()) {
                        foreach (object obj in result.Properties[propvalue]) {
                            names.Add(obj.ToString());
                        }
                    }
                } catch (COMException) {
                    // this exception occurs when user account isn't in any domain.
                    // error mesage is "The specified domain either does not exist or could not be contacted."
                    // swallow this exception, an empty list will be returned.
                }
            }
            return names.ToArray();
        }
    }
}
