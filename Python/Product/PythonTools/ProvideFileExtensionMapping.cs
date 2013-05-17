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
using Microsoft.VisualStudio.Shell;

namespace Microsoft {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    class ProvideFileExtensionMapping : RegistrationAttribute {
        private readonly string _name, _id, _editorGuid, _package;
        private readonly int _sortPriority;

        public ProvideFileExtensionMapping(string id, string name, object editorGuid, string package, int sortPriority) {
            _id = id;
            _name = name;
            if (editorGuid is Type) {
                _editorGuid = ((Type)editorGuid).GUID.ToString("B");
            } else {
                _editorGuid = editorGuid.ToString();
            }
            _package = package;
            _sortPriority = sortPriority;
        }

        public override void Register(RegistrationContext context) {
            using (Key mappingKey = context.CreateKey("FileExtensionMapping\\" + _id)) {
                mappingKey.SetValue("", _name);
                mappingKey.SetValue("DisplayName", _name);
                mappingKey.SetValue("EditorGuid", _editorGuid);
                mappingKey.SetValue("Package", _package);
                mappingKey.SetValue("SortPriority", _sortPriority);
            }
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
        }
    }
}
