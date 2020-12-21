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

using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    /// <summary>
    /// Stores cached state for creating a MockVs.  This state is initialized once for the process and then
    /// re-used to create new MockVs instances.  We create fresh MockVs instances to avoid having state
    /// lingering between tests.
    /// </summary>
    class CachedVsInfo
    {
        public readonly ComposableCatalog Catalog;
        public readonly List<Type> Packages;
        public Dictionary<string, LanguageServiceInfo> LangServicesByName = new Dictionary<string, LanguageServiceInfo>();
        public Dictionary<Guid, LanguageServiceInfo> LangServicesByGuid = new Dictionary<Guid, LanguageServiceInfo>();
        public Dictionary<string, string> _languageNamesByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CachedVsInfo(ComposableCatalog catalog, List<Type> packages)
        {
            Catalog = catalog;
            Packages = packages;

            foreach (var package in Packages)
            {
                var attrs = package.GetCustomAttributes(typeof(ProvideLanguageServiceAttribute), false);
                foreach (ProvideLanguageServiceAttribute attr in attrs)
                {
                    foreach (var type in package.Assembly.GetTypes())
                    {
                        if (type.GUID == attr.LanguageServiceSid)
                        {
                            var info = new LanguageServiceInfo(attr);
                            LangServicesByGuid[attr.LanguageServiceSid] = info;
                            LangServicesByName[attr.LanguageName] = info;

                            break;
                        }
                    }
                }

                var extensions = package.GetCustomAttributes(typeof(ProvideLanguageExtensionAttribute), false);
                foreach (ProvideLanguageExtensionAttribute attr in extensions)
                {
                    LanguageServiceInfo info;
                    if (LangServicesByGuid.TryGetValue(attr.LanguageService, out info))
                    {
                        _languageNamesByExtension[attr.Extension] = info.Attribute.LanguageName;
                    }
                }
            }
        }
    }
}
