// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    ///<summary>
    /// This attribute associates a file extension to a given editor factory.  
    /// The editor factory may be specified as either a GUID or a type and 
    /// is placed on a package.
    ///</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    internal sealed class ProvideNewFileTemplatesAttribute : RegistrationAttribute {
        private readonly string _projectGuid, _packageGuid;
        private readonly string _languageNameId, _folderName;
        private readonly int _sortPriority;

        public ProvideNewFileTemplatesAttribute(string projectGuid, string packageGuid, string languageNameId, string folderName, int sortPriority = 0x32) {
            _projectGuid = projectGuid;
            _packageGuid = packageGuid;
            _languageNameId = languageNameId;
            _folderName = folderName;
            _sortPriority = sortPriority;
        }

        ///<summary>
        /// Called to register this attribute with the given context.  The context
        /// contains the location where the registration inforomation should be placed.
        /// it also contains such as the type being registered, and path information.
        ///
        /// This method is called both for registration and unregistration.  The difference is
        /// that unregistering just uses a hive that reverses the changes applied to it.
        ///</summary>
        public override void Register(RegistrationContext context) {
            Build().Register(context);
        }

        public override void Unregister(RegistrationContext context) {
            Build().Unregister(context);
        }

        private RegistrationAttributeBuilder Build() {
            var builder = new RegistrationAttributeBuilder();
            string templatesKey = 
                    string.Format(CultureInfo.InvariantCulture, "Projects\\{{{0}}}\\AddItemTemplates\\TemplateDirs\\{{{1}}}\\/1",
                    _projectGuid, _packageGuid);

            builder.Key(templatesKey)
                    .PackageGuidValue("Package")
                    .StringValue(string.Empty, _languageNameId)
                    .StringValue("TemplatesDir", @"$PackageFolder$\" + _folderName)
                    .IntValue("SortPriority", _sortPriority);
            return builder;
        }

    }
}
