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

namespace Microsoft.VisualStudio.Shell {
    /// <summary>
    /// This attribute adds the property page registration for Component picker 
    /// For Example:
    /// [HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0Exp\ComponentPickerPages\My Component Page]
    /// @="#13925"
    ///     "Package"="{B0002DC2-56EE-4931-93F7-70D6E9863940}"
    ///     "Page"="{0A9F3920-3881-4f50-8986-9EDEC7B33566}"
    ///     "Sort"=dword:00000014
    ///     "AddToMru"=dword:00000000
    ///     "ComponentType"=".Net Assembly"
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class ProvideComponentPickerPropertyPageAttribute : RegistrationAttribute {
        private string _packageGuid;
        private string _pageGuid;
        private string _pageRegKeyName;

        private string _componentType = null;
        private int _sortOrder = -1;
        private bool _addToMRU = false;
        private string _defaultPageNameValue = "";

        /// <summary>
        /// Creates a new ComponentPicker page registration attribute to register a custom
        /// component picker property page. 
        /// </summary>
        /// <param name="packageType">The type of pacakge that provides the page</param>
        /// <param name="pageType">The page type that needs to be registered</param>
        /// <param name="pageRegKeyName">Registry key name for the page.</param>
        public ProvideComponentPickerPropertyPageAttribute(Type packageType, Type pageType, string pageRegKeyName) {
            if (packageType == null)
                throw new ArgumentNullException("packageType");
            if (pageType == null)
                throw new ArgumentNullException("pageType");
            if (pageRegKeyName == null)
                throw new ArgumentNullException("pageName");

            _packageGuid = packageType.GUID.ToString("B");
            _pageGuid = pageType.GUID.ToString("B");
            _pageRegKeyName = pageRegKeyName;
        }

        /// <summary>
        /// Get the pacakge Guid
        /// </summary>
        public string PacakgeGuid {
            get { return _packageGuid; }
        }

        /// <summary>
        /// Get the Guid representing the property page
        /// </summary>
        public string PageGuid {
            get { return _pageGuid; }
        }

        /// <summary>
        /// Get the property page reg key name.
        /// </summary>
        public string PageRegKeyName {
            get { return _pageRegKeyName; }
        }

        /// <summary>
        /// Get or Set the AddToMru value
        /// </summary>
        public bool AddToMru {
            get { return _addToMRU; }
            set { _addToMRU = value; }
        }

        /// <summary>
        /// Get or set the Component Type value.
        /// </summary>
        public string ComponentType {
            get { return _componentType; }
            set { _componentType = value; }
        }

        /// <summary>
        /// Get or Set the Sort reg value
        /// </summary>
        public int SortOrder {
            get { return _sortOrder; }
            set { _sortOrder = value; }
        }

        /// <summary>
        /// get / sets default page name value 
        /// </summary>
        public string DefaultPageNameValue {
            get { return _defaultPageNameValue; }
            set { _defaultPageNameValue = value; }
        }

        /// <summary>
        /// Property that gets the page reg key name
        /// </summary>
        private string PageRegKey {
            get { return string.Format(CultureInfo.InvariantCulture, @"ComponentPickerPages\{0}", PageRegKeyName); }
        }
        /// <summary>
        ///     Called to register this attribute with the given context.  The context
        ///     contains the location where the registration inforomation should be placed.
        ///     It also contains other information such as the type being registered and path information.
        /// </summary>
        public override void Register(RegistrationContext context) {
            using (Key childKey = context.CreateKey(PageRegKey)) {
                childKey.SetValue(string.Empty, DefaultPageNameValue);
                childKey.SetValue("Package", PacakgeGuid);
                childKey.SetValue("Page", PageGuid);

                if (SortOrder != -1)
                    childKey.SetValue("Sort", SortOrder);
                if (AddToMru)
                    childKey.SetValue("AddToMru", Convert.ToInt32(AddToMru));
                if (ComponentType != null)
                    childKey.SetValue("ComponentType", ComponentType);

            }

        }

        /// <summary>
        /// Unregister property page
        /// </summary>
        /// <param name="context"></param>
        public override void Unregister(RegistrationContext context) {
            context.RemoveKey(PageRegKey);
        }
    }
}