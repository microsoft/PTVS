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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Specifies the localizable display name for a property, event, or public void method which takes no arguments. 
    /// First looks up the name in local string resources than falls back to MPFProj resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class LocalizableDisplayNameAttribute : DisplayNameAttribute {
        private string _name;

        public LocalizableDisplayNameAttribute(string name) {
            _name = name;
        }

        public override string DisplayName {
            get {
                string result = DynamicProjectSR.GetString(_name);
                if (result == null) {
                    result = SR.GetString(_name);
                    if (result == null) {
                        Debug.Assert(false, "String resource '" + _name + "' is missing");
                        result = _name;
                    }
                }
                return result;
            }
        }
    }

    public class DynamicProjectSR {
        public const string InterpreterPath = "InterpreterPath";
        public const string InterpreterPathDescription = "InterpreterPathDescription";
        public const string ProjectReferenceError = "ProjectReferenceError";
        public const string ProjectReferenceError2 = "ProjectReferenceError2";
        public const string Application = "Application";
        public const string GeneralCaption = "GeneralCaption";
        public const string Project = "Project";
        public const string ProjectFile = "ProjectFile";
        public const string ProjectFileDescription = "ProjectFileDescription";
        public const string ProjectFileExtensionFilter = "ProjectFileExtensionFilter";
        public const string ProjectFolder = "ProjectFolder";
        public const string ProjectFolderDescription = "ProjectFolderDescription";
        public const string StartupFile = "StartupFile";
        public const string StartupFileDescription = "StartupFileDescription";
        public const string SelectFolderForSearchPath = "SelectFolderForSearchPath";
        public const string SelectZipFileForSearchPath = "SelectZipFileForSearchPath";
        public const string WorkingDirectory = "WorkingDirectory";
        public const string WorkingDirectoryDescription = "WorkingDirectoryDescription";
        public const string CommandLineArguments = "CommandLineArguments";
        public const string CommandLineArgumentsDescription = "CommandLineArgumentsDescription";
        public const string IsWindowsApplication = "IsWindowsApplication";
        public const string IsWindowsApplicationDescription = "IsWindowsApplicationDescription";

        private static DynamicProjectSR _loader = null;
        private ResourceManager _resources;
        private static Object _internalSyncObject;

        private static Object InternalSyncObject {
            get {
                if (_internalSyncObject == null) {
                    Object o = new Object();
                    Interlocked.CompareExchange(ref _internalSyncObject, o, null);
                }
                return _internalSyncObject;
            }
        }

        internal DynamicProjectSR() {
            _resources = new ResourceManager("Microsoft.VisualStudioTools.SharedProject", this.GetType().Assembly);
        }

        private static DynamicProjectSR GetLoader() {
            if (_loader == null) {
                lock (InternalSyncObject) {
                    if (_loader == null) {
                        _loader = new DynamicProjectSR();
                    }
                }
            }

            return _loader;
        }

        private static CultureInfo Culture {
            get { return null/*use ResourceManager default, CultureInfo.CurrentUICulture*/; }
        }

        public static ResourceManager Resources {
            get {
                return GetLoader()._resources;
            }
        }

        public static string GetString(string name, params object[] args) {
            DynamicProjectSR sys = GetLoader();
            if (sys == null)
                return null;
            string res = sys._resources.GetString(name, DynamicProjectSR.Culture);

            if (args != null && args.Length > 0) {
                return String.Format(CultureInfo.CurrentCulture, res, args);
            } else {
                return res;
            }
        }

        public static string GetString(string name) {
            DynamicProjectSR sys = GetLoader();
            if (sys == null)
                return null;
            return sys._resources.GetString(name, DynamicProjectSR.Culture);
        }

        public static object GetObject(string name) {
            DynamicProjectSR sys = GetLoader();
            if (sys == null)
                return null;
            return sys._resources.GetObject(name, DynamicProjectSR.Culture);
        }
    }
}
