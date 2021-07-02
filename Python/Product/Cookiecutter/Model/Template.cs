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

namespace Microsoft.CookiecutterTools.Model
{
    class Template
    {
        public Template()
        {
            Name = string.Empty;
            RemoteUrl = string.Empty;
            LocalFolderPath = string.Empty;
            Description = string.Empty;
            AvatarUrl = string.Empty;
            OwnerUrl = string.Empty;
        }

        public string Name { get; set; }
        public string RemoteUrl { get; set; }
        public string LocalFolderPath { get; set; }
        public string Description { get; set; }
        public string AvatarUrl { get; set; }
        public string OwnerUrl { get; set; }
        public DateTime? ClonedLastUpdate { get; set; }
        public DateTime? RemoteLastUpdate { get; set; }
        public bool? UpdateAvailable
        {
            get
            {
                if (RemoteLastUpdate.HasValue && ClonedLastUpdate.HasValue)
                {
                    var span = RemoteLastUpdate - ClonedLastUpdate;
                    return span.Value.TotalMinutes > 2;
                }

                return null;
            }
        }

        public Template Clone()
        {
            return (Template)MemberwiseClone();
        }
    }
}
