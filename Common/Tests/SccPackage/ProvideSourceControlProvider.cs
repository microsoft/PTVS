// Visual Studio Shared Project
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

namespace Microsoft.TestSccPackage
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ProvideSourceControlProvider : RegistrationAttribute
    {
        private readonly string _name;
        private readonly Guid _sourceControlGuid;
        private readonly Type _providerType, _packageType;

        public ProvideSourceControlProvider(string friendlyName, string sourceControlGuid, Type sccPackage, Type sccProvider)
        {
            _name = friendlyName;
            _providerType = sccProvider;
            _packageType = sccPackage;
            _sourceControlGuid = new Guid(sourceControlGuid);
        }

        public override void Register(RegistrationContext context)
        {
            // http://msdn.microsoft.com/en-us/library/bb165948.aspx
            using (Key sccProviders = context.CreateKey("SourceControlProviders"))
            {
                using (Key sccProviderKey = sccProviders.CreateSubkey(_sourceControlGuid.ToString("B")))
                {
                    sccProviderKey.SetValue("", _name);
                    sccProviderKey.SetValue("Service", _providerType.GUID.ToString("B"));

                    using (Key sccProviderNameKey = sccProviderKey.CreateSubkey("Name"))
                    {
                        sccProviderNameKey.SetValue("", _name);
                        sccProviderNameKey.SetValue("Package", _packageType.GUID.ToString("B"));
                    }
                }
            }/*
            using (Key currentProvider = context.CreateKey("CurrentSourceControlProvider")) {
                currentProvider.SetValue("", _sourceControlGuid.ToString("B"));
            }*/
        }

        public override void Unregister(RegistrationContext context)
        {
        }
    }
}
