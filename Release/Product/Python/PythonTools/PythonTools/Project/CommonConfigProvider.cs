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

using Microsoft.VisualStudio;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Enables the Any CPU Platform form name for Dynamic Projects.
    /// Hooks language specific project config.
    /// </summary>
    internal class CommonConfigProvider : ConfigProvider {
        private CommonProjectNode _project;

        public CommonConfigProvider(CommonProjectNode project)
            : base(project) {
            _project = project;
        }

        #region overridden methods

        protected override ProjectConfig CreateProjectConfiguration(string configName) {
            return _project.MakeConfiguration(configName);
        }

        public override int GetPlatformNames(uint celt, string[] names, uint[] actual) {
            if (names != null) {
                names[0] = "Any CPU";
            }

            if (actual != null) {
                actual[0] = 1;
            }

            return VSConstants.S_OK;
        }

        public override int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual) {
            if (names != null) {
                names[0] = "Any CPU";
            }

            if (actual != null) {
                actual[0] = 1;
            }

            return VSConstants.S_OK;
        }
        #endregion
    }
}
