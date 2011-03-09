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

using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    class ProvideDebugLanguageAttribute : RegistrationAttribute {
        private readonly string _guid, _languageName, _engineGuid;

        public ProvideDebugLanguageAttribute(string languageName, string guid, string engineGuid) {
            _languageName = languageName;
            _guid = guid;
            _engineGuid = engineGuid;
        }

        public override void Register(RegistrationContext context) {
            var langSvcKey = context.CreateKey("Langauges\\Language Services\\" + _languageName + "\\Debugger Languages\\" + _guid);
            langSvcKey.SetValue("", _languageName);

            var eeKey = context.CreateKey("AD7Metrics\\ExpressionEvaluator\\" + _guid + "\\{994B45C4-E6E9-11D2-903F-00C04FA302A1}\\Engine");
            eeKey.SetValue("Language", _languageName);
            eeKey.SetValue("Name", _languageName);
            eeKey.SetValue("Engine", _engineGuid);
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
