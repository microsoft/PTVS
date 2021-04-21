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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Provides a base class for logging complicated event data.
    /// </summary>
    abstract class PythonToolsLoggerData {
        public static IDictionary<string, object> AsDictionary(object obj) {
            if (obj == null) {
                return null;
            }

            if (obj is IDictionary<string, object> res) {
                return res;
            }

            if (!(obj is PythonToolsLoggerData)) {
                return null;
            }

            res = new Dictionary<string, object>();
            foreach (var propInfo in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                try {
                    var value = propInfo.GetValue(obj);
                    if (propInfo.GetCustomAttributes().OfType<PiiPropertyAttribute>().Any()) {
                        value = new TelemetryPiiProperty(value);
                    }
                    res[propInfo.Name] = value;
                } catch (Exception ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(PythonToolsLoggerData)));
                }
            }
            return res;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class PiiPropertyAttribute : Attribute {
        public PiiPropertyAttribute() { }
    }

    sealed class PackageInfo : PythonToolsLoggerData {
        [PiiProperty]
        public string Name { get; set; }
    }

    sealed class AnalysisInitialize : PythonToolsLoggerData {
        [PiiProperty]
        public string InterpreterId { get; set; }
        public string Architecture { get; set; }
        public string Version { get; set; }
        public string Reason { get; set; }
        public bool IsIronPython { get; set; }
    }

    static class AnalysisInitializeReasons {
        public const string Project = "Project";
        public const string Workspace = "Workspace";
        public const string Interactive = "Interactive";
        public const string Default = "Default";
    }

    sealed class AnalysisInfo : PythonToolsLoggerData {
        [PiiProperty]
        public string InterpreterId { get; set; }
        public int AnalysisSeconds { get; set; }
    }

    sealed class LaunchInfo : PythonToolsLoggerData {
        public bool IsDebug { get; set; }
        public bool IsWeb { get; set; }
        public string Version { get; set; }
    }

    sealed class AnalysisTimingInfo : PythonToolsLoggerData {
        public string RequestName { get; set; }
        public int Milliseconds { get; set; }
        public bool Timeout { get; set; }
    }

    sealed class DebugReplInfo : PythonToolsLoggerData {
        public bool RemoteProcess { get; set; }
        public string Version { get; set; }
    }

    sealed class GetExpressionAtPointInfo : PythonToolsLoggerData {
        public int Milliseconds { get; set; }
        public int PartialAstLength { get; set; }
        public bool Success { get; set; }
        public bool ExpressionFound { get; set; }
    }

    static class InfoBarContexts {
        public const string Project = "Project";
        public const string Workspace = "Workspace";
    }

    static class CondaEnvCreateInfoBarActions {
        public const string Prompt = "Prompt";
        public const string Create = "Create";
        public const string Ignore = "Ignore";
    }

    static class CondaEnvCreateInfoBarReasons {
        public const string MissingEnv = "MissingEnv";
        public const string NoEnv = "NoEnv";
    }

    sealed class CondaEnvCreateInfoBarInfo : PythonToolsLoggerData {
        public string Reason { get; set; }
        public string Action { get; set; }
        public string Context { get; set; }
    }

    static class VirtualEnvCreateInfoBarActions {
        public const string Prompt = "Prompt";
        public const string Create = "Create";
        public const string Ignore = "Ignore";
    }

    sealed class VirtualEnvCreateInfoBarInfo : PythonToolsLoggerData {
        public string Action { get; set; }
        public string Context { get; set; }
    }

    sealed class ConfigureTestFrameworkInfoBarInfo : PythonToolsLoggerData {
        public string Action { get; set; }
        public string Context { get; set; }
    }

    static class ConfigureTestFrameworkInfoBarActions {
        public const string Prompt = "Prompt";
        public const string InstallPytest = "InstallPytest";
        public const string EnablePytest = "EnablePytest";
        public const string EnableUnitTest = "EnableUnittest";
        public const string EnableAndInstallPytest = "EnableAndInstallPytest";
        public const string Ignore = "Ignore";
    }

    static class PackageInstallInfoBarActions {
        public const string Prompt = "Prompt";
        public const string Install = "Install";
        public const string Ignore = "Ignore";
    }

    sealed class PythonVersionNotSupportedInfoBarInfo : PythonToolsLoggerData {
        public string Action { get; set; }
        public string Context { get; set; }
        public string PythonVersion { get; set; }
    }

    static class PythonVersionNotSupportedInfoBarAction {
        public const string Prompt = "Prompt";
        public const string MoreInfo = "MoreInfo";
        public const string Ignore = "Ignore";
    }

    sealed class PackageInstallInfoBarInfo : PythonToolsLoggerData {
        public string Action { get; set; }
        public string Context { get; set; }
    }

    sealed class CreateCondaEnvInfo : PythonToolsLoggerData {
        public bool Failed { get; set; }
        public bool FromEnvironmentFile { get; set; }
        public bool SetAsDefault { get; set; }
        public bool SetAsCurrent { get; set; }
        public bool OpenEnvironmentsWindow { get; set; }
    }

    sealed class CreateVirtualEnvInfo : PythonToolsLoggerData {
        public bool Failed { get; set; }
        public string LanguageVersion { get; set; }
        public string Architecture { get; set; }
        public bool InstallRequirements { get; set; }
        public bool UseVEnv { get; set; }
        public bool Global { get; set; }
        public bool SetAsDefault { get; set; }
        public bool SetAsCurrent { get; set; }
        public bool OpenEnvironmentsWindow { get; set; }
    }

    sealed class AddExistingEnvInfo : PythonToolsLoggerData {
        public bool Failed { get; set; }
        public string LanguageVersion { get; set; }
        public string Architecture { get; set; }
        public bool Custom { get; set; }
        public bool Global { get; set; }
    }

    sealed class SelectEnvFromToolbarInfo : PythonToolsLoggerData {
        [PiiProperty]
        public string InterpreterId { get; set; }
        public string Architecture { get; set; }
        public string Version { get; set; }
        public bool IsIronPython { get; set; }
    }

    sealed class FormatDocumentInfo : PythonToolsLoggerData {
        public string Version { get; set; }
        public string Formatter { get; set; }
        public long TimeMilliseconds { get; set; }
        public bool IsError { get; set; }
        public bool IsErrorModuleNotInstalled { get; set; }
        public bool IsErrorInstallingModule { get; set; }
        public bool IsErrorRangeNotSupported { get; set; }
        public bool IsRange { get; set; }
    }
    
    sealed class UntrustedWorkspaceInfoBarInfo : PythonToolsLoggerData {
        public string Action { get; set; }
    }

    static class UntrustedWorkspaceInfoBarAction {
        public const string Prompt = "Prompt";
        public const string AlwaysTrust = "AlwaysTrust";
        public const string TrustOnce = "TrustOnce";
        public const string DontTrust = "DontTrust";
    }
}
