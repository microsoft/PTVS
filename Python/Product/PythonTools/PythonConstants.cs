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
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools {
    public static class PythonConstants {
        //Language name
        public const string LanguageName = "Python";
        internal const string TextEditorSettingsRegistryKey = LanguageName;
        internal const string FileExtension = ".py";
        internal const string ProjectFileFilter = "Python Project File (*.pyproj)\n*.pyproj\nAll Files (*.*)\n*.*\n";
        /// <summary>
        /// The extension for Python files which represent Windows applications.
        /// </summary>
        internal const string WindowsFileExtension = ".pyw";
#if DEV11_OR_LATER
        internal const string ProjectImageList = "Microsoft.PythonImageList.png";
#else
        internal const string ProjectImageList = "Microsoft.PythonImageList.bmp";
#endif

        internal const string IssueTrackerUrl = "http://go.microsoft.com/fwlink/?LinkId=402428";

        internal const string LibraryManagerGuid = "888888e5-b976-4366-9e98-e7bc01f1842c";
        internal const string LibraryManagerServiceGuid = "88888859-2f95-416e-9e2b-cac4678e5af7";
        public const string ProjectFactoryGuid = "888888a0-9f3d-457c-b088-3a5042f75d52";
        internal const string WebProjectFactoryGuid = "1b580a1a-fdb3-4b32-83e1-6407eb2722e6";
        internal const string EditorFactoryGuid = "888888c4-36f9-4453-90aa-29fa4d2e5706";
        internal const string ProjectNodeGuid = "8888881a-afb8-42b1-8398-e60d69ee864d";
        public const string GeneralPropertyPageGuid = "888888fd-3c4a-40da-aefb-5ac10f5e8b30";
        public const string DebugPropertyPageGuid = "9A46BC86-34CB-4597-83E5-498E3BDBA20A";
        public const string PublishPropertyPageGuid = "63DF0877-CF53-4975-B200-2B11D669AB00";
        internal const string WebPropertyPageGuid = "76EED3B5-14B1-413B-937A-F6F79AC1F8C8";
        internal const string EditorFactoryPromptForEncodingGuid = "CA887E0B-55C6-4AE9-B5CF-A2EEFBA90A3E";

        internal const string InterpreterItemType = "32235F49-CF87-4F2C-A986-B38D229976A3";
        internal const string InterpretersPackageItemType = "64D8C685-F085-4E04-B759-3DF715EBA3FA";
        internal static readonly Guid InterpreterItemTypeGuid = new Guid(InterpreterItemType);
        internal static readonly Guid InterpretersPackageItemTypeGuid = new Guid(InterpretersPackageItemType);

        internal const string InterpretersPropertiesGuid = "45D3DC23-F419-4744-B55B-B897FAC1F4A2";
        internal const string InterpretersWithBaseInterpreterPropertiesGuid = "F86C3C5B-CF94-4184-91F8-29687D3B9227";
        internal const string InterpretersPackagePropertiesGuid = "BBF56A45-B037-4CC2-B710-F2CE304CCF32";
        internal const string InterpreterListToolWindowGuid = "75504045-D02F-44E5-BF60-5F60DF380E8B";

        // Do not change below info without re-requesting PLK:
        internal const string ProjectSystemPackageGuid = "15490272-3C6B-4129-8E1D-795C8B6D8E9F"; //matches PLK

        // IDs of the icons for product registration (see Resources.resx)
        internal const int IconIfForSplashScreen = 300;
        internal const int IconIdForAboutBox = 400;
#if !DEV14_OR_LATER
        internal const int ResourceIdForReplImages = 407;
#endif

        // Command IDs
        internal const int AddEnvironment = 0x4006;
        internal const int AddVirtualEnv = 0x4007;
        internal const int AddExistingVirtualEnv = 0x4008;
        internal const int ActivateEnvironment = 0x4009;
        internal const int InstallPythonPackage = 0x400A;
        internal const int InstallRequirementsTxt = 0x4033;
        internal const int GenerateRequirementsTxt = 0x4034;
        internal const int OpenInteractiveForEnvironment = 0x4031;
        internal const int ViewAllEnvironments = 0x400B;

        internal const int AddSearchPathCommandId = 0x4002;
        internal const int AddSearchPathZipCommandId = 0x4003;
        internal const int AddPythonPathToSearchPathCommandId = 0x4030;

        // Context menu IDs
        internal const int EnvironmentsContainerMenuId = 0x2006;
        internal const int EnvironmentMenuId = 0x2007;
        internal const int EnvironmentPackageMenuId = 0x2008;
        internal const int SearchPathContainerMenuId = 0x2009;
        internal const int SearchPathMenuId = 0x200A;

        // Custom (per-project) commands
        internal const int FirstCustomCmdId = 0x4010;
        internal const int LastCustomCmdId = 0x402F;
        internal const int CustomProjectCommandsMenu = 0x2005;

        // Shows up before references
        internal const int InterpretersContainerNodeSortPriority = 200;

        // Appears after references
        internal const int SearchPathContainerNodeSortPriority = 400;

        // Maximal sort priority for Search Path nodes
        internal const int SearchPathNodeMaxSortPriority = 110;


        internal const string InterpreterId = "InterpreterId";
        internal const string InterpreterVersion = "InterpreterVersion";

        internal const string LaunchProvider = "LaunchProvider";

        internal const string PythonExtension = "PythonExtension";

        public const string SearchPathSetting = "SearchPath";
        public const string InterpreterPathSetting = "InterpreterPath";
        public const string InterpreterArgumentsSetting = "InterpreterArguments";
        public const string CommandLineArgumentsSetting = "CommandLineArguments";
        public const string StartupFileSetting = "StartupFile";
        public const string IsWindowsApplicationSetting = "IsWindowsApplication";
        public const string EnvironmentSetting = "Environment";

        /// <summary>
        /// Specifies port to which to open web browser on launch.
        /// </summary>
        public const string WebBrowserPortSetting = "WebBrowserPort";

        /// <summary>
        /// Specifies URL to which to open web browser on launch.
        /// </summary>
        public const string WebBrowserUrlSetting = "WebBrowserUrl";

        /// <summary>
        /// Specifies local address for the web server to listen on.
        /// </summary>
        public const string WebServerHostSetting = "WebServerHost";

        // Mixed-mode debugging project property
        public const string EnableNativeCodeDebugging = "EnableNativeCodeDebugging";

        public const string WorkingDirectorySetting = "WorkingDirectory";
        public const string ProjectHomeSetting = "ProjectHome";

        /// <summary>
        /// The canonical name of the debug launcher for web projects.
        /// </summary>
        public const string WebLauncherName = "Web launcher";

        /// <summary>
        /// The settings collection where "Suppress{dialog}" settings are stored
        /// </summary>
        public const string DontShowUpgradeDialogAgainCollection = "PythonTools\\Dialogs";

        internal const string PythonToolsProcessIdEnvironmentVariable = "_PTVS_PID";
    }
}
