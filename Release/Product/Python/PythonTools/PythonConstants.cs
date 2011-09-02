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

namespace Microsoft.PythonTools {
    internal static class PythonConstants {
        //Language name
        public const string LanguageName = "Python";
        public const string TextEditorSettingsRegistryKey = LanguageName;
        public const string FileExtension = ".py";
        /// <summary>
        /// The extension for Python files which represent Windows applications.
        /// </summary>
        public const string WindowsFileExtension = ".pyw";
        public const string ProjectImageList = "Microsoft.PythonImageList.bmp";
        
        public const string LibraryManagerGuid = "888888e5-b976-4366-9e98-e7bc01f1842c";
        public const string LibraryManagerServiceGuid = "88888859-2f95-416e-9e2b-cac4678e5af7";
        public const string ProjectFactoryGuid = "888888a0-9f3d-457c-b088-3a5042f75d52";
        public const string EditorFactoryGuid = "888888c4-36f9-4453-90aa-29fa4d2e5706";
        public const string ProjectNodeGuid = "8888881a-afb8-42b1-8398-e60d69ee864d";
        public const string GeneralPropertyPageGuid = "888888fd-3c4a-40da-aefb-5ac10f5e8b30";
        public const string DebugPropertyPageGuid = "9A46BC86-34CB-4597-83E5-498E3BDBA20A";
        public const string PublishPropertyPageGuid = "63DF0877-CF53-4975-B200-2B11D669AB00";
        public const string EditorFactoryPromptForEncodingGuid = "CA887E0B-55C6-4AE9-B5CF-A2EEFBA90A3E";

        // Do not change below info without re-requesting PLK:
        public const string ProjectSystemPackageGuid = "15490272-3C6B-4129-8E1D-795C8B6D8E9F"; //matches PLK

        //IDs of the icons for product registration (see Resources.resx)
        public const int IconIfForSplashScreen = 300;
        public const int IconIdForAboutBox = 400;

        
        public const string InterpreterId = "InterpreterId";
        public const string InterpreterVersion = "InterpreterVersion";

        public const string LaunchProvider = "LaunchProvider";

    }
}
