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
using System.Globalization;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {

    /// <include file='doc\ProvideEditorExtensionAttribute.uex' path='docs/doc[@for="ProvideEditorExtensionAttribute"]' />
    /// <devdoc>
    ///     This attribute associates a file extension to a given editor factory.  
    ///     The editor factory may be specified as either a GUID or a type and 
    ///     is placed on a package.
    ///     
    /// This differs from the normal one in that more than one extension can be supplied and
    /// a linked editor GUID can be supplied.
    /// </devdoc>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    internal sealed class ProvideLanguageTemplatesAttribute : RegistrationAttribute {
        private readonly string _projectFactory, _languageName, _package, _languageGuid, _description, _templateGroup,
            _codeFileExtension, _templateFolder, _webProjectGuid;
        
        public ProvideLanguageTemplatesAttribute(string projectFactory, string languageName, string package, 
            string templateGroup, string description, string languageProjectGuid, string codeFileExtension, string templateFolder, string webProjectGuid) {
            _projectFactory = projectFactory;
            _languageName = languageName;
            _package = package;
            _description = description;
            _languageGuid = languageProjectGuid;
            _templateGroup = templateGroup;            
            _codeFileExtension = codeFileExtension;
            _templateFolder = templateGroup;
            _webProjectGuid = webProjectGuid;
        }


        /// <include file='doc\ProvideEditorExtensionAttribute.uex' path='docs/doc[@for="Register"]' />
        /// <devdoc>
        ///     Called to register this attribute with the given context.  The context
        ///     contains the location where the registration inforomation should be placed.
        ///     it also contains such as the type being registered, and path information.
        ///
        ///     This method is called both for registration and unregistration.  The difference is
        ///     that unregistering just uses a hive that reverses the changes applied to it.
        /// </devdoc>
        public override void Register(RegistrationContext context) {
            string langTemplates = string.Format(CultureInfo.InvariantCulture, "Projects\\{0}\\LanguageTemplates", _projectFactory);

            using (Key projectKey = context.CreateKey(langTemplates)) {
                projectKey.SetValue(_languageGuid, _webProjectGuid);
            }

            var newProject = string.Format(CultureInfo.InvariantCulture, "Projects\\{0}", _webProjectGuid);
            using (Key projectKey = context.CreateKey(newProject)) {
                projectKey.SetValue(null, _description);
                projectKey.SetValue(_package, _languageGuid);
                projectKey.SetValue("Language(VsTemplate)", _languageName);
                //projectKey.SetValue("Package", _package);
                projectKey.SetValue("ShowOnlySpecifiedTemplates(VsTemplate)", 0);
                projectKey.SetValue("TemplateGroupIDs(VsTemplate)", _templateGroup);

                using (Key propKey = projectKey.CreateSubkey("WebApplicationProperties")) {
                    propKey.SetValue("CodeFileExtension", _codeFileExtension);
                    propKey.SetValue("TemplateFolder", _templateFolder);
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}