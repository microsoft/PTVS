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
using System.Linq;
using Microsoft.Tools.WindowsInstallerXml;

[assembly: AssemblyDefaultWixExtension(typeof(PythonVersions.PythonVersions))]

namespace PythonVersions {
    public class PythonPreprocessorExtension : PreprocessorExtension {
        // As new versions are supported by FastCgiInstaller, they should be added here with new GUIDs.
        // The GUID is used to auto-generate component IDs and must remain stable for each version.
        private static readonly VersionInfo[] Versions = new[] {
            new VersionInfo(2, 7, "{389073E5-F922-4007-9AAE-5F5ACB2A9DEE}", false),
            new VersionInfo(2, 7, "{6FAD4626-E962-4EAC-934E-354BA07F1E5A}", true),
            new VersionInfo(3, 4, "{CDDA8AE3-52A5-4765-9E06-D772B325F62F}", false),
            new VersionInfo(3, 4, "{B8A50E10-3135-45A5-BEA1-3F2702539A18}", true),
        };


        private static readonly string[] _prefixes = new[] { "python" };
        public override string[] Prefixes {
            get { return _prefixes; }
        }
        
        class VersionInfo {
            public VersionInfo(int major, int minor, string guid, bool win64) {
                RegistryVersion = string.Format("{0}.{1}", major, minor);
                DisplayVersion = string.Format("{0}.{1} ({2})", major, minor, win64 ? "64-bit" : "32-bit");
                InternalVersion = string.Format("{0}{1}_{2}", major, minor, win64 ? "AMD64" : "X86");
                Guid = guid;
                Win64 = win64 ? "yes" : "no";
            }
            
            public readonly string Guid;
            public readonly string DisplayVersion;
            public readonly string RegistryVersion;
            public readonly string InternalVersion;
            public readonly string Win64;
        }


        public override string GetVariableValue(string prefix, string name) {
            if (prefix != "python") {
                return null;
            }
            
            if (name == "versions") {
                return string.Join(";", Enumerable.Range(0, Versions.Length).Select(i => i.ToString()));
            }
            
            return null;
        }

        public override string EvaluateFunction(string prefix, string function, string[] args) {
            if (prefix != "python") {
                return null;
            }
            
            int i;
            if (!int.TryParse(args[0], out i)) {
                return null;
            }
            
            if (function == "get_display_version") {
                return Versions[i].DisplayVersion;
            } else if (function == "get_registry_version") {
                return Versions[i].RegistryVersion;
            } else if (function == "get_version") {
                return Versions[i].InternalVersion;
            } else if (function == "get_guid") {
                return Versions[i].Guid;
            } else if (function == "is_win64") {
                return Versions[i].Win64;
            }
            
            return null;
        }
    }
    
    public class PythonVersions : WixExtension {
        private readonly Lazy<PythonPreprocessorExtension> _preprocessorExtension = new Lazy<PythonPreprocessorExtension>();
        public override PreprocessorExtension PreprocessorExtension {
            get { return _preprocessorExtension.Value; }
        }
    }
}
