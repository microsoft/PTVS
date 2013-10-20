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

namespace Microsoft.PythonTools.Interpreter {
    public static class InterpreterExtensions {
        public static string GetInterpreterDisplay(this IPythonInterpreterFactory interpreterFactory) {
            var configurable = interpreterFactory as ConfigurablePythonInterpreterFactory;
            if (configurable != null) {
                return configurable.Description ?? "";
            }

            return String.Format("{0} {1}", interpreterFactory.Description, FormatVersion(interpreterFactory.Configuration.Version));
        }

        /// <summary>
        /// Gets a path which is unique for this interpreter (based upon the Id and version).
        /// </summary>
        public static string GetInterpreterPath(this IPythonInterpreterFactory interpreterFactory) {
            return interpreterFactory.Id.ToString("B") + "\\" + interpreterFactory.Configuration.Version;
        }

        internal static string FormatVersion(Version version) {
            if (version.Revision == 0) {
                if (version.Build == 0) {
                    if (version.Minor == 0) {
                        return version.Major.ToString();
                    } else {
                        return string.Format("{0}.{1}", version.Major, version.Minor);
                    }
                } else {
                    return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
                }
            } else {
                return version.ToString();
            }
        }
    }
}
