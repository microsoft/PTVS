// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Concrete;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    [DataContract(Name = "Error", Namespace = Constants.ServiceManagementNS)]
    public class ServiceManagementError : IExtensibleDataObject
    {
        [DataMember(Order = 1)]
        public string Code { get; set; }

        [DataMember(Order = 2)]
        public string Message { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public ConfigurationWarningsList ConfigurationWarnings { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }

    public static class ErrorCode
    {
        public const string MissingOrIncorrectVersionHeader = "MissingOrIncorrectVersionHeader";
        public const string InvalidRequest = "InvalidRequest";
        public const string InvalidXmlRequest = "InvalidXmlRequest";
        public const string InvalidContentType = "InvalidContentType";
        public const string MissingOrInvalidRequiredQueryParameter = "MissingOrInvalidRequiredQueryParameter";
        public const string InvalidHttpVerb = "InvalidHttpVerb";
        public const string InternalError = "InternalError";
        public const string BadRequest = "BadRequest";
        public const string AuthenticationFailed = "AuthenticationFailed";
        public const string ResourceNotFound = "ResourceNotFound";
        public const string SubscriptionDisabled = "SubscriptionDisabled";
        public const string ServerBusy = "ServerBusy";
        public const string TooManyRequests = "TooManyRequests";
        public const string ConflictError = "ConflictError";
        public const string ConfiguraitonError = "ConfigurationError";

    }
}