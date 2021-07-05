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

namespace Microsoft.PythonTools.Environments {
    sealed class AddEnvironmentTemplateSelector : DataTemplateSelector {
        public DataTemplate AddCondaEnvironment { get; set; }

        public DataTemplate AddExistingEnvironment { get; set; }

        public DataTemplate AddVirtualEnvironment { get; set; }

        public DataTemplate AddInstalledEnvironment { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            if (item is AddCondaEnvironmentView) {
                return AddCondaEnvironment;
            } else if (item is AddExistingEnvironmentView) {
                return AddExistingEnvironment;
            } else if (item is AddVirtualEnvironmentView) {
                return AddVirtualEnvironment;
            } else if (item is AddInstalledEnvironmentView) {
                return AddInstalledEnvironment;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
