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
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudioTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class ProvideDebugPortSupplierAttribute : RegistrationAttribute {
        private readonly string _id, _name;
        private readonly Type _portSupplier, _portPicker;

        public ProvideDebugPortSupplierAttribute(string name, Type portSupplier, string id, Type portPicker = null) {
            _name = name;
            _portSupplier = portSupplier;
            _id = id;
            _portPicker = portPicker;
        }

        public override void Register(RegistrationContext context) {
            var engineKey = context.CreateKey("AD7Metrics\\PortSupplier\\" + _id);
            engineKey.SetValue("Name", _name);
            engineKey.SetValue("CLSID", _portSupplier.GUID.ToString("B"));
            if (_portPicker != null) {
                engineKey.SetValue("PortPickerCLSID", _portPicker.GUID.ToString("B"));
            }

            var clsidKey = context.CreateKey("CLSID");
            var clsidGuidKey = clsidKey.CreateSubkey(_portSupplier.GUID.ToString("B"));
            clsidGuidKey.SetValue("Assembly", _portSupplier.Assembly.FullName);
            clsidGuidKey.SetValue("Class", _portSupplier.FullName);
            clsidGuidKey.SetValue("InprocServer32", context.InprocServerPath);
            clsidGuidKey.SetValue("CodeBase", Path.Combine(context.ComponentPath, _portSupplier.Module.Name));
            clsidGuidKey.SetValue("ThreadingModel", "Free");
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
