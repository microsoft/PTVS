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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudioTools {
    class DeveloperActivityAttribute : RegistrationAttribute {
        private readonly Type _projectType;
        private readonly int _templateSet;
        private readonly string _developerActivity;

        public DeveloperActivityAttribute(string developerActivity, Type projectPackageType) {
            _developerActivity = developerActivity;
            _projectType = projectPackageType;
            _templateSet = 1;
        }

        public DeveloperActivityAttribute(string developerActivity, Type projectPackageType, int templateSet) {
            _developerActivity = developerActivity;
            _projectType = projectPackageType;
            _templateSet = templateSet;
        }

        public override void Register(RegistrationAttribute.RegistrationContext context) {
            var key = context.CreateKey("NewProjectTemplates\\TemplateDirs\\" + _projectType.GUID.ToString("B") + "\\/" + _templateSet);
            key.SetValue("DeveloperActivity", _developerActivity);
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
        }
    }
}
