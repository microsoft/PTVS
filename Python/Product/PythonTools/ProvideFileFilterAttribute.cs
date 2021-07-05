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

namespace Microsoft.PythonTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideFileFilterAttribute : RegistrationAttribute {
        private readonly string _id, _name, _filter;
        private readonly int _sortPriority;

        public ProvideFileFilterAttribute(string projectGuid, string name, string filter, int sortPriority) {
            _name = name;
            _id = Guid.Parse(projectGuid).ToString("B");
            _filter = filter;
            _sortPriority = sortPriority;
        }

        public override void Register(RegistrationContext context) {
            using (var engineKey = context.CreateKey("Projects\\" + _id + "\\Filters\\" + _name)) {
                engineKey.SetValue("", _filter);
                engineKey.SetValue("SortPriority", _sortPriority);
                engineKey.SetValue("CommonOpenFilesFilter", 1);
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
