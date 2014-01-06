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
using CommonSR = Microsoft.VisualStudioTools.Project.SR;

namespace Microsoft.PythonTools.Project {
    internal class SR : CommonSR {
        public const string CanNotCreateWindow = "CanNotCreateWindow";
        public const string ExecutingStartupScript = "ExecutingStartupScript";
        public const string InitializingFromProject = "InitializingFromProject";
        public const string MissingStartupScript = "MissingStartupScript";
        public const string SettingWorkDir = "SettingWorkDir";
        public const string ToolWindowTitle = "ToolWindowTitle";
        public const string UpdatingSearchPath = "UpdatingSearchPath";
        public const string WarningAnalysisNotCurrent = "WarningAnalysisNotCurrent";

        public const string SearchPaths = "SearchPaths";
        public const string SearchPathContainerProperties = "SearchPathProperties";
        public const string SearchPathProperties = "SearchPathProperties";
        public const string SearchPathsDescription = "SearchPathsDescription";
        public const string SearchPathRemoveConfirmation = "SearchPathRemoveConfirmation";

        public const string Environments = "Environments";
        public const string EnvironmentRemoveConfirmation = "EnvironmentRemoveConfirmation";
        public const string EnvironmentDeleteConfirmation = "EnvironmentDeleteConfirmation";
        public const string EnvironmentDeleteError = "EnvironmentDeleteError";

        public const string PackageFullName = "PackageFullName";
        public const string PackageFullNameDescription = "PackageFullNameDescription";

        public const string EnvironmentIdDisplayName = "EnvironmentIdDisplayName";
        public const string EnvironmentIdDescription = "EnvironmentIdDescription";
        public const string EnvironmentVersionDisplayName = "EnvironmentVersionDisplayName";
        public const string EnvironmentVersionDescription = "EnvironmentVersionDescription";

        public const string BaseInterpreterDisplayName = "BaseInterpreterDisplayName";
        public const string BaseInterpreterDescription = "BaseInterpreterDescription";
        public const string InstallVirtualEnvAndPip = "InstallVirtualEnvAndPip";
        public const string InstallVirtualEnv = "InstallVirtualEnv";
        public const string InstallPip = "InstallPip";
        public const string InstallEasyInstall = "InstallEasyInstall";
        public const string UninstallPackage = "UninstallPackage";
        public const string PythonToolsForVisualStudio = "PythonToolsForVisualStudio";

        public const string PackageInstalling = "PackageInstalling";
        public const string PackageInstallingSeeOutputWindow = "PackageInstallingSeeOutputWindow";
        public const string PackageInstallSucceeded = "PackageInstallSucceeded";
        public const string PackageInstallFailed = "PackageInstallFailed";
        public const string PackageInstallFailedExitCode = "PackageInstallFailedExitCode";

        public const string PackageUninstalling = "PackageUninstalling";
        public const string PackageUninstallingSeeOutputWindow = "PackageUninstallingSeeOutputWindow";
        public const string PackageUninstallSucceeded = "PackageUninstallSucceeded";
        public const string PackageUninstallFailed = "PackageUninstallFailed";
        public const string PackageUninstallFailedExitCode = "PackageUninstallFailedExitCode";

        public const string PipInstalling = "PipInstalling";
        public const string PipInstallSucceeded = "PipInstallSucceeded";
        public const string PipInstallFailed = "PipInstallFailed";
        public const string PipInstallFailedExitCode = "PipInstallFailedExitCode";

        public const string VirtualEnvCreating = "VirtualEnvCreating";
        public const string VirtualEnvCreationSucceeded = "VirtualEnvCreationSucceeded";
        public const string VirtualEnvCreationFailed = "VirtualEnvCreationFailed";
        public const string VirtualEnvCreationFailedExitCode = "VirtualEnvCreationFailedExitCode";

        public const string ErrorRunningCustomCommand = "ErrorRunningCustomCommand";
        public const string ErrorBuildingCustomCommand = "ErrorBuildingCustomCommand";
        public const string ErrorCommandAlreadyRunning = "ErrorCommandAlreadyRunning";
        public const string FailedToReadResource = "FailedToReadResource";

        public const string CustomCommandReplTitle = "CustomCommandReplTitle";
        public const string CustomCommandPrerequisitesInstallPrompt = "CustomCommandPrerequisitesInstallPrompt";
        public const string PythonMenuLabel = "PythonMenuLabel";

        public const string NoInterpretersAvailable = "NoInterpretersAvailable";
        
        public const string ErrorImportWizardUnauthorizedAccess = "ErrorImportWizardUnauthorizedAccess";
        public const string ErrorImportWizardException = "ErrorImportWizardException";
        public const string StatusImportWizardError = "StatusImportWizardError";
        public const string StatusImportWizardStarting = "StatusImportWizardStarting";
        public const string ImportWizardProjectExists = "ImportWizardProjectExists";

        public const string ReplInitializationMessage = "ReplInitializationMessage";
        public const string ReplEvaluatorInterpreterNotFound = "ReplEvaluatorInterpreterNotFound";
        public const string ReplEvaluatorInterpreterNotConfigured = "ReplEvaluatorInterpreterNotConfigured";

        public const string DefaultLauncherName = "DefaultLauncherName";
        public const string DefaultLauncherDescription = "DefaultLauncherDescription";
        
        public const string PythonWebLauncherName = "PythonWebLauncherName";
        public const string PythonWebLauncherDescription = "PythonWebLauncherDescription";
        public const string ErrorInvalidLaunchUrl = "ErrorInvalidLaunchUrl";

        internal static new string GetString(string value) {
            string result = Microsoft.PythonTools.Resources.ResourceManager.GetString(value, CultureInfo.CurrentUICulture) ?? CommonSR.GetString(value);
            if (result == null) {
                Debug.Assert(false, "String resource '" + value + "' is missing");
                result = value;
            }
            return result;
        }

        internal static new string GetString(string value, params object[] args) {
            string result = Microsoft.PythonTools.Resources.ResourceManager.GetString(value, CultureInfo.CurrentUICulture) ?? CommonSR.GetString(value);
            if (result == null) {
                Debug.Assert(false, "String resource '" + value + "' is missing");
                result = value;
            }
            return string.Format(CultureInfo.CurrentUICulture, result, args);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class LocDisplayNameAttribute : DisplayNameAttribute {
        readonly string value;

        public LocDisplayNameAttribute(string name) {
            value = name;
        }

        public override string DisplayName {
            get {
                return SR.GetString(value);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class SRCategoryAttribute : CategoryAttribute {
        public SRCategoryAttribute(string name) : base(name) { }

        protected override string GetLocalizedString(string value) {
            return SR.GetString(value);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class SRDescriptionAttribute : DescriptionAttribute {
        readonly string value;

        public SRDescriptionAttribute(string name) {
            value = name;
        }

        public override string Description {
            get {
                return SR.GetString(value);
            }
        }
    }
}
