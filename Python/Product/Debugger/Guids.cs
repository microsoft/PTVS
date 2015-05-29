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

namespace Microsoft.PythonTools.DkmDebugger {
    public static class Guids {
        public const string RemoteComponentId = "BCFD7074-A4D3-42A9-B1B6-C975304C882A";
        public static readonly Guid RemoteComponentGuid = new Guid(RemoteComponentId);

        public const string LocalComponentId = "E42AC982-8F0B-45DE-8F22-EC045687F2EC";
        public static readonly Guid LocalComponentGuid = new Guid(LocalComponentId);

        public const string LocalStackWalkingComponentId = "538659BC-990B-4F53-B3C8-834D54397E15";
        public static readonly Guid LocalStackWalkingComponentGuid = new Guid(LocalStackWalkingComponentId);

        public static readonly Guid MicrosoftVendorGuid = new Guid("994B45C4-E6E9-11D2-903F-00C04FA302A1");
        public static readonly Guid PythonRuntimeTypeGuid = new Guid("0B253BA3-E62E-4428-A583-36E33EA26E54");
        public static readonly Guid PythonSymbolProviderGuid = new Guid("4C802B60-6E39-4CE0-8FE8-F77F83458399");
        public static readonly Guid PythonLanguageGuid = new Guid("DA3C7D59-F9E4-4697-BEE7-3A0703AF6BFF");
        public static readonly Guid UnknownPythonModuleGuid = new Guid("42A6F911-9997-4504-861C-91015BDCE588");
        public static readonly Guid CppLanguageGuid = new Guid("3A12D0B7-C26C-11D0-B442-00A0244A1DD2");

        public const string PythonExceptionCategoryId = "EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9";
        public static readonly Guid PythonExceptionCategoryGuid = new Guid(PythonExceptionCategoryId);

        public const string PythonNativeVisualizerId = "C85DBEDF-48BA-4BC8-ADC7-B3A7B70D692A";
        public static readonly Guid PythonNativeVisualizerGuid = new Guid(PythonNativeVisualizerId);

        public const string PythonStepTargetSourceId = "5653D51F-7824-41A0-9CE5-96D2E4AFC18B";
        public static readonly Guid PythonStepTargetSourceGuid = new Guid(PythonStepTargetSourceId);

        public const string PythonTraceManagerSourceId = "5B0A4B66-C7A5-4D51-9581-9C89AF483691";
        public static readonly Guid PythonTraceManagerSourceGuid = new Guid(PythonTraceManagerSourceId);

        public const string CustomDebuggerEventHandlerId = "996D22BD-D117-4611-88F2-2832CB7D9517";
        public static readonly Guid CustomDebuggerEventHandlerGuid = new Guid(CustomDebuggerEventHandlerId);

        public const string ProgramProviderCLSID = "FA452F5D-539E-4B55-BCC6-5DE7E342BC44";
        public const string DebugEngineCLSID = "0DA53AFE-069E-47A3-AE34-32610A8253A3";
        public const string RemoteDebugPortSupplierCLSID = "B8CBA3DE-4A20-4DD7-8709-EC66A6A256D3";
    };
}
