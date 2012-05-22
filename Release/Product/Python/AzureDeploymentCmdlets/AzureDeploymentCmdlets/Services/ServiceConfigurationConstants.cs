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

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    public static class ConfigurationConstants
    {
        public const string ServiceManagementEndpoint = "https://management.core.windows.net";

        public static Binding WebHttpBinding(int maxStringContentLength)
        {
            var binding = new WebHttpBinding(WebHttpSecurityMode.Transport);
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;
            binding.ReaderQuotas.MaxStringContentLength =
                maxStringContentLength > 0 ?
                maxStringContentLength :
                67108864;

            return binding;
        }
    }
}