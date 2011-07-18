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

using Microsoft.PythonTools.Parsing;

namespace AnalysisTest {
    class PythonPaths {
        public static readonly PythonVersion Python24 = new PythonVersion("C:\\Python24\\python.exe", PythonLanguageVersion.V24);
        public static readonly PythonVersion Python25 = new PythonVersion("C:\\Python25\\python.exe", PythonLanguageVersion.V25); 
        public static readonly PythonVersion Python26 = new PythonVersion("C:\\Python26\\python.exe", PythonLanguageVersion.V26);
        public static readonly PythonVersion Python27 = new PythonVersion("C:\\Python27\\python.exe", PythonLanguageVersion.V27);
        public static readonly PythonVersion Python30 = new PythonVersion("C:\\Python30\\python.exe", PythonLanguageVersion.V30);
        public static readonly PythonVersion Python31 = new PythonVersion("C:\\Python31\\python.exe", PythonLanguageVersion.V31);
        public static readonly PythonVersion Python32 = new PythonVersion("C:\\Python32\\python.exe", PythonLanguageVersion.V32);
        public static readonly PythonVersion IronPython27 = new PythonVersion("C:\\Program Files (x86)\\IronPython 2.7\\ipy.exe", PythonLanguageVersion.V27);
    }

    class PythonVersion {
        public readonly string Path;
        public readonly PythonLanguageVersion Version;

        public PythonVersion(string path, PythonLanguageVersion pythonLanguageVersion) {
            Path = path;
            Version = pythonLanguageVersion;
        }
    }
}
