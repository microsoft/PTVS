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
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools {
    public enum TestFrameworkType {
        None = 0,
        Pytest = 1,
        UnitTest = 2
    }

    static class PythonConstants {
        //Language name
        public const string LanguageName = "Python";
        internal const string TextEditorSettingsRegistryKey = LanguageName;
        internal const string ProjectFileFilter = "Python Project File (*.pyproj)\n*.pyproj\nAll Files (*.*)\n*.*\n";
        /// <summary>
        /// The extension for Python files which represent Windows applications.
        /// </summary>
        internal const string ProjectImageList = "Microsoft.PythonImageList.png";

        internal const string FileExtension = ".py";
        internal const string WindowsFileExtension = ".pyw";
        internal const string StubFileExtension = ".pyi";
        internal const string SourceFileExtensions = ".py;.pyw;.pyi";
        internal static readonly string[] SourceFileExtensionsArray = SourceFileExtensions.Split(';');

        internal const string LibraryManagerGuid = "888888e5-b976-4366-9e98-e7bc01f1842c";
        internal const string LibraryManagerServiceGuid = "88888859-2f95-416e-9e2b-cac4678e5af7";
        public const string ProjectFactoryGuid = "888888a0-9f3d-457c-b088-3a5042f75d52";
        internal const string WebProjectFactoryGuid = "1b580a1a-fdb3-4b32-83e1-6407eb2722e6";
        internal const string EditorFactoryGuid = "888888c4-36f9-4453-90aa-29fa4d2e5706";
        internal const string ProjectNodeGuid = "8888881a-afb8-42b1-8398-e60d69ee864d";
        public const string GeneralPropertyPageGuid = "888888fd-3c4a-40da-aefb-5ac10f5e8b30";
        public const string DebugPropertyPageGuid = "9A46BC86-34CB-4597-83E5-498E3BDBA20A";
        public const string PublishPropertyPageGuid = "63DF0877-CF53-4975-B200-2B11D669AB00";
        public const string TestPropertyPageGuid = "D3B8505A-A2A7-49ED-B2C1-400136801EC6";
        internal const string WebPropertyPageGuid = "76EED3B5-14B1-413B-937A-F6F79AC1F8C8";
        internal const string TextMateEditorGuid = "3B902123-F8A7-4915-9F01-361F908088D0";
        //internal const string EditorFactoryPromptForEncodingGuid = "CA887E0B-55C6-4AE9-B5CF-A2EEFBA90A3E";

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

        // Command IDs
        internal const int AddEnvironment = 0x4006;
        internal const int AddVirtualEnv = 0x4007;
        internal const int AddExistingEnv = 0x4008;
        internal const int ActivateEnvironment = 0x4009;
        internal const int InstallPythonPackage = 0x400A;
        internal const int InstallRequirementsTxt = 0x4033;
        internal const int GenerateRequirementsTxt = 0x4034;
        internal const int ProcessRequirementsTxt = 0x4036; // deprecated
        internal const int AddCondaEnv = 0x4037;
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
        internal const int ReplWindowToolbar = 0x200B;
        internal const int EnvironmentStatusBarMenu = 0x200D;

        // Custom (per-project) commands
        internal const int FirstCustomCmdId = 0x4010;
        internal const int LastCustomCmdId = 0x402F;
        internal const int CustomProjectCommandsMenu = 0x2005;

        // Environments status bar switcher commands
        internal const int FirstEnvironmentCmdId = 0x4050;
        internal const int LastEnvironmentCmdId = 0x4090;

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
        public const string FormatterSetting = "Formatter";
        public const string ExtraPathsSetting = "ExtraPaths";
        public const string StubPathSetting = "StubPath";
        public const string TypeCheckingModeSetting = "TypeCheckingMode";
        public const string EnvironmentSetting = "Environment";
        public const string TestFrameworkSetting = "TestFramework";
        public const string UnitTestRootDirectorySetting = "UnitTestRootDirectory";
        public const string DefaultUnitTestRootDirectory = ".";
        public const string UnitTestPatternSetting = "UnitTestPattern";
        public const string DefaultUnitTestPattern = "test*.py";

        public static readonly Regex DefaultTestFileNameRegex = 
            new Regex(@"((^test.*)|(^.*_test))\.(py|txt)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex TestFileExtensionRegex = 
            new Regex(@".*\.(py|txt)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly HashSet<string> PyTestFrameworkConfigFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"pytest.ini", "setup.cfg", "tox.ini"};

        /// <summary>
        /// Specifies port to which to open web browser on launch.
        /// </summary>
        public const string WebBrowserPortSetting = "WebBrowserPort";

        /// <summary>
        /// Specifies URL to which to open web browser on launch.
        /// </summary>
        public const string WebBrowserUrlSetting = "WebBrowserUrl";

        /// <summary>
        /// When True, prevents web projects from copying Cloud Service files
        /// into their bin directory.
        /// </summary>
        public const string SuppressCollectPythonCloudServiceFiles = "SuppressCollectPythonCloudServiceFiles";

        /// <summary>
        /// Specifies local address for the web server to listen on.
        /// </summary>
        public const string WebServerHostSetting = "WebServerHost";

        // Mixed-mode debugging project property
        public const string EnableNativeCodeDebugging = "EnableNativeCodeDebugging";

        // Suppress the prompt for environment creation project property
        public const string SuppressEnvironmentCreationPrompt = "SuppressEnvironmentCreationPrompt";

        // Suppress the prompt for package installation project property
        public const string SuppressPackageInstallationPrompt = "SuppressPackageInstallationPrompt";

        // Suppress the prompt for pytest configuration project property
        public const string SuppressConfigureTestFrameworkPrompt = "SuppressConfigureTestFrameworkPrompt";

        // Suppress the prompt for python version not supported property
        public const string SuppressPythonVersionNotSupportedPrompt = "SuppressPythonVersionNotSupportedPrompt";

        // Launch option to ignore pause on exist settings
        internal const string NeverPauseOnExit = "NeverPauseOnExit";

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

        internal const string UnitTestExecutorUriString = "executor://PythonUnitTestExecutor/v1";
        internal const string PytestExecutorUriString = "executor://PythonPyTestExecutor/v1";
        internal const string PythonProjectContainerDiscovererUriString = "executor://PythonProjectDiscoverer/v1";
        internal const string PythonWorkspaceContainerDiscovererUriString = "executor://PythonWorkspaceDiscoverer/v1";
        internal const string PythonCodeCoverageUriString = "datacollector://Microsoft/PythonCodeCoverage/1.0";

        public static readonly Uri UnitTestExecutorUri = new Uri(UnitTestExecutorUriString);
        public static readonly Uri PytestExecutorUri = new Uri(PytestExecutorUriString);

        public static readonly Uri PythonProjectContainerDiscovererUri = new Uri(PythonProjectContainerDiscovererUriString);
        public static readonly Uri PythonWorkspaceContainerDiscovererUri = new Uri(PythonWorkspaceContainerDiscovererUriString);

        public static readonly Uri PythonCodeCoverageUri = new Uri(PythonCodeCoverageUriString);

        //Discovery
        internal const string PytestText = "pytest";
        internal const string UnitTestText = "unittest";
        internal const int DiscoveryTimeoutInSeconds = 60;
    }
}
