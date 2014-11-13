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

namespace Microsoft.VisualStudioTools.VSTestHost.Internal {
    sealed class RegisterSupportedTestTypeAttribute : RegistrationAttribute {
        private readonly string _hostAdapterName;
        private readonly string _hostAdapterDisplayName;
        private readonly string _testTypeGuid;
        private readonly string _testTypeName;

        const string SupportedTestTypesKey = @"EnterpriseTools\QualityTools\HostAdapters\{0}\SupportedTestTypes";
        const string SupportedHostAdaptersKey = @"EnterpriseTools\QualityTools\TestTypes\{0}\SupportedHostAdapters";

        public RegisterSupportedTestTypeAttribute(
            string hostAdapterName,
            string hostAdapterDisplayName,
            string testTypeGuid,
            string testTypeName
        ) {
            _hostAdapterName = hostAdapterName;
            _hostAdapterDisplayName = hostAdapterDisplayName;
            _testTypeGuid = new Guid(testTypeGuid).ToString("B");
            _testTypeName = testTypeName;
        }

        public override void Register(RegistrationAttribute.RegistrationContext context) {
            using (var key = context.CreateKey(string.Format(SupportedTestTypesKey, _hostAdapterName))) {
                key.SetValue(_testTypeGuid, _testTypeName);
            }

            using (var key = context.CreateKey(string.Format(SupportedHostAdaptersKey, _testTypeGuid))) {
                key.SetValue(_hostAdapterName, _hostAdapterDisplayName);
            }
        }

        public override void Unregister(RegistrationAttribute.RegistrationContext context) {
            context.RemoveValue(string.Format(SupportedTestTypesKey, _hostAdapterName), _testTypeGuid);
            context.RemoveKeyIfEmpty(string.Format(SupportedTestTypesKey, _hostAdapterName));

            context.RemoveValue(string.Format(SupportedHostAdaptersKey, _testTypeGuid), _hostAdapterName);
            context.RemoveKeyIfEmpty(string.Format(SupportedHostAdaptersKey, _testTypeGuid));
        }
    }
}
