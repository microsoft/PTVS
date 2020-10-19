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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.Project.Automation {
    /// <summary>
    /// Contains all of the properties of a given object that are contained in a generic collection of properties.
    /// </summary>
    [ComVisible(true)]
    public class OAProperties : EnvDTE.Properties {
        private readonly NodeProperties _target;
        private readonly Dictionary<string, EnvDTE.Property> _properties = new Dictionary<string, EnvDTE.Property>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public OAProperties(NodeProperties target) {
            Utilities.ArgumentNotNull("target", target);

            _target = target;
            AddPropertiesFromType(target.GetType());
        }

        /// <summary>
        /// Defines the NodeProperties object that contains the defines the properties.
        /// </summary>
        public NodeProperties Target => _target;

        #region EnvDTE.Properties

        /// <summary>
        /// Microsoft Internal Use Only.
        /// </summary>
        public virtual object Application => null;

        /// <summary>
        /// Gets a value indicating the number of objects in the collection.
        /// </summary>
        public int Count => _properties.Count;

        /// <summary>
        /// Gets the top-level extensibility object.
        /// </summary>
        public virtual EnvDTE.DTE DTE {
            get {
                if (_target.HierarchyNode == null || _target.HierarchyNode.ProjectMgr == null || _target.HierarchyNode.ProjectMgr.IsClosed ||
                    _target.HierarchyNode.ProjectMgr.Site == null) {
                    throw new InvalidOperationException();
                }
                return _target.HierarchyNode.ProjectMgr.Site.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            }
        }

        /// <summary>
        /// Gets an enumeration for items in a collection. 
        /// </summary>
        /// <returns>An enumerator. </returns>
        public IEnumerator GetEnumerator() {
            if (_properties.Count == 0) {
                yield return new OANullProperty(this);
            }

            IEnumerator enumerator = _properties.Values.GetEnumerator();

            while (enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }

        /// <summary>
        /// Returns an indexed member of a Properties collection. 
        /// </summary>
        /// <param name="index">The index at which to return a member.</param>
        /// <returns>A Property object.</returns>
        public virtual EnvDTE.Property Item(object index) {
            if (index is string indexAsString) {
                if (_properties.ContainsKey(indexAsString)) {
                    return _properties[indexAsString];
                }
                return null;
            } else if (index is int @int) {
                int realIndex = @int - 1;
                if (realIndex >= 0 && realIndex < _properties.Count) {
                    IEnumerator enumerator = _properties.Values.GetEnumerator();

                    int i = 0;
                    while (enumerator.MoveNext()) {
                        if (i++ == realIndex) {
                            return (EnvDTE.Property)enumerator.Current;
                        }
                    }
                }
            }

            throw new ArgumentException(SR.GetString(SR.InvalidParameter), "index");
        }
        /// <summary>
        /// Gets the immediate parent object of a Properties collection.
        /// </summary>
        public virtual object Parent => null;
        #endregion

        #region methods
        /// <summary>
        /// Add properties to the collection of properties filtering only those properties which are com-visible and AutomationBrowsable
        /// </summary>
        /// <param name="targetType">The type of NodeProperties the we should filter on</param>
        private void AddPropertiesFromType(Type targetType) {
            Utilities.ArgumentNotNull("targetType", targetType);

            // If the type is not COM visible, we do not expose any of the properties
            if (!IsComVisible(targetType)) {
                return;
            }

            // Add all properties being ComVisible and AutomationVisible 
            PropertyInfo[] propertyInfos = targetType.GetProperties();
            foreach (PropertyInfo propertyInfo in propertyInfos) {
                if (!IsInMap(propertyInfo) && IsComVisible(propertyInfo) && IsAutomationVisible(propertyInfo)) {
                    AddProperty(propertyInfo);
                }
            }
        }
        #endregion

        #region virtual methods
        /// <summary>
        /// Creates a new OAProperty object and adds it to the current list of properties
        /// </summary>
        /// <param name="propertyInfo">The property to be associated with an OAProperty object</param>
        private void AddProperty(PropertyInfo propertyInfo) {
            var attrs = propertyInfo.GetCustomAttributes(typeof(PropertyNameAttribute), false);
            string name = propertyInfo.Name;
            if (attrs.Length > 0) {
                name = ((PropertyNameAttribute)attrs[0]).Name;
            }
            _properties.Add(name, new OAProperty(this, propertyInfo));
        }
        #endregion

        #region helper methods

        private bool IsInMap(PropertyInfo propertyInfo) => _properties.ContainsKey(propertyInfo.Name);

        private static bool IsAutomationVisible(PropertyInfo propertyInfo) {
            object[] customAttributesOnProperty = propertyInfo.GetCustomAttributes(typeof(AutomationBrowsableAttribute), true);

            foreach (AutomationBrowsableAttribute attr in customAttributesOnProperty) {
                if (!attr.Browsable) {
                    return false;
                }
            }
            return true;
        }

        private static bool IsComVisible(Type targetType) {
            object[] customAttributesOnProperty = targetType.GetCustomAttributes(typeof(ComVisibleAttribute), true);

            foreach (ComVisibleAttribute attr in customAttributesOnProperty) {
                if (!attr.Value) {
                    return false;
                }
            }
            return true;
        }

        private static bool IsComVisible(PropertyInfo propertyInfo) {
            object[] customAttributesOnProperty = propertyInfo.GetCustomAttributes(typeof(ComVisibleAttribute), true);

            foreach (ComVisibleAttribute attr in customAttributesOnProperty) {
                if (!attr.Value) {
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
