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

// PkgCmdID.cs
// MUST match PkgCmdID.h

namespace Microsoft.PythonTools
{
    static class PkgCmdIDList
    {
        public const int EnvWindowToolbar = 0x200c;

        public const uint cmdidExecuteFileInRepl = 0x102;
        public const uint cmdidSendToRepl = 0x103;
        public const uint cmdidFillParagraph = 0x105;
        public const uint cmdidDiagnostics = 0x106;
        public const uint cmdidRemoveImports = 0x107;
        public const uint cmdidRemoveImportsCurrentScope = 0x108;
        public const uint cmdidRefactorRenameIntegratedShell = 0x109;
        public const uint cmdidExtractMethodIntegratedShell = 0x10a;
        public const uint cmdidImportWizard = 0x10d;
        public const uint cmdidImportCoverage = 0x10f;

        public const uint cmdidReplWindow = 0x201;
        public const uint cmdidNewInteractiveWindow = 0x202;
        public const uint cmdidDebugReplWindow = 0x210;
        public const uint cmdidInterpreterList = 0x220;

        public const uint cmdidShowCppView = 0x400C;
        public const uint cmdidShowPythonView = 0x400D;
        public const uint cmdidShowNativePythonFrames = 0x400E;
        public const uint cmdidUsePythonStepping = 0x400F;

        public const uint cmdidAddEnvironment = 0x4006;
        public const uint cmdidAddVirtualEnv = 0x4007;
        public const uint cmdidAddExistingEnv = 0x4008;
        public const uint cmdidActivateEnvironment = 0x4009;
        public const uint cmdidInstallPythonPackage = 0x400A;
        public const uint cmdidViewAllEnvironments = 0x400B;

        public const uint cmdidInstallRequirementsTxt = 0x4033;
        public const uint cmdidGenerateRequirementsTxt = 0x4034;

        public const uint cmdidOpenInteractiveScopeInEditor = 0x4035;

        public const uint cmdidAddCondaEnv = 0x4037;

        public const uint cmdidWebPythonAtMicrosoft = 0x4040;
        public const uint cmdidWebPTVSSupport = 0x4041;
        public const uint cmdidWebDGProducts = 0x4042;

        public const uint comboIdReplScopes = 0x5000;
        public const uint comboIdReplScopesGetList = 0x5001;
        public const uint comboIdReplEvaluators = 0x5002;
        public const uint comboIdReplEvaluatorsGetList = 0x5003;
        public const uint comboIdCurrentEnvironment = 0x5004;
        public const uint comboIdCurrentEnvironmentList = 0x5005;
    };
}