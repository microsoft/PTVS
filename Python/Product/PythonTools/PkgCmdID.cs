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

// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace Microsoft.PythonTools {
    static class PkgCmdIDList {
        public const uint cmdidExecuteFileInRepl = 0x102;
        public const uint cmdidSendToRepl = 0x103;
        public const uint cmdidSendToDefiningModule = 0x104;
        public const uint cmdidFillParagraph = 0x105;
        public const uint cmdidDiagnostics = 0x106;
        public const uint cmdidRemoveImports = 0x107;
        public const uint cmdidRemoveImportsCurrentScope = 0x108;
        public const uint cmdidRefactorRenameIntegratedShell = 0x109;
        public const uint cmdidExtractMethodIntegratedShell = 0x10a;
        public const uint cmdidImportWizard = 0x10d;
        public const uint cmdidSurveyNews = 0x10e;

        public const uint cmdidReplWindow = 0x201;
        public const uint cmdidReplWindow2 = 0x202;
        public const uint cmdidReplWindow3 = 0x203;
        public const uint cmdidReplWindow4 = 0x204;
        public const uint cmdidReplWindow5 = 0x205;
        public const uint cmdidReplWindow6 = 0x206;
        public const uint cmdidReplWindow7 = 0x207;
        public const uint cmdidReplWindow8 = 0x208;
        public const uint cmdidReplWindow9 = 0x209;
        public const uint cmdidReplWindowA = 0x20A;
        public const uint cmdidReplWindowB = 0x20B;
        public const uint cmdidReplWindowC = 0x20C;
        public const uint cmdidReplWindowD = 0x20D;
        public const uint cmdidReplWindowE = 0x20E;
        public const uint cmdidReplWindowF = 0x20F;
        public const uint cmdidDebugReplWindow = 0x210;
        public const uint cmdidInterpreterList = 0x220;

        public const uint cmdidShowCppView = 0x400C;
        public const uint cmdidShowPythonView = 0x400D;
        public const uint cmdidShowNativePythonFrames = 0x400E;
        public const uint cmdidUsePythonStepping = 0x400F;

        public const uint cmdidAzureExplorerAttachPythonDebugger = 0x4032;

        public const uint cmdidAddEnvironment = 0x4006;
        public const uint cmdidAddVirtualEnv = 0x4007;
        public const uint cmdidAddExistingVirtualEnv = 0x4008;
        public const uint cmdidActivateEnvironment = 0x4009;
        public const uint cmdidInstallPythonPackage = 0x400A;
        public const uint cmdidViewAllEnvironments = 0x400B;

        public const uint cmdidInstallRequirementsTxt = 0x4033;
        public const uint cmdidGenerateRequirementsTxt = 0x4034;

        public const uint comboIdReplScopes = 0x5000;
        public const uint comboIdReplScopesGetList = 0x5001;
    };
}