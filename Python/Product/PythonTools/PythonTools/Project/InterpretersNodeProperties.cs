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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    [Guid(PythonConstants.InterpretersPropertiesGuid)]
    public class InterpretersNodeProperties : NodeProperties {
        #region properties

        [Browsable(false)]
        [AutomationBrowsable(false)]
        protected IPythonInterpreterFactory Factory {
            get {
                var node = HierarchyNode as InterpretersNode;
                if (node != null) {
                    return node._factory;
                }
                return null;
            }
        }

        // TODO: Expose interpreter configuration through properties

        [SRCategory(SR.Misc)]
        [SRDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName {
            get {
                return PathUtils.GetFileOrDirectoryName(this.HierarchyNode.Url);
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        [AutomationBrowsable(true)]
        public string FullPath {
            get {
                return this.HierarchyNode.Url;
            }
        }

#if DEBUG
        [SRCategory(SR.Misc)]
        [SRDisplayName("EnvironmentIdDisplayName")]
        [SRDescription("EnvironmentIdDescription")]
        [AutomationBrowsable(true)]
        public string Id {
            get {
                var fact = Factory;
                return fact != null ? fact.Id.ToString("B") : "";
            }
        }
#endif

        [SRCategory(SR.Misc)]
        [SRDisplayName("EnvironmentVersionDisplayName")]
        [SRDescription("EnvironmentVersionDescription")]
        [AutomationBrowsable(true)]
        public string Version {
            get {
                var fact = Factory;
                return fact != null ? fact.Configuration.Version.ToString() : "";
            }
        }

        #region properties - used for automation only
        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName {
            get {
                return this.HierarchyNode.Url;
            }
        }

        #endregion

        #endregion

        #region ctors
        internal InterpretersNodeProperties(HierarchyNode node)
            : base(node) { }
        #endregion

        public override string GetClassName() {
            return "Environment Properties";
        }
    }

    [ComVisible(true)]
    [Guid(PythonConstants.InterpretersWithBaseInterpreterPropertiesGuid)]
    public class InterpretersNodeWithBaseInterpreterProperties : InterpretersNodeProperties {
        [SRCategory(SR.Misc)]
        [SRDisplayName("BaseInterpreterDisplayName")]
        [SRDescription("BaseInterpreterDescription")]
        [AutomationBrowsable(true)]
        public string BaseInterpreter {
            get {
                var fact = Factory as DerivedInterpreterFactory;
                return fact != null ? 
                    fact.BaseInterpreter.Description :
                    SR.GetString(SR.UnknownInParentheses);
            }
        }

        internal InterpretersNodeWithBaseInterpreterProperties(HierarchyNode node)
            : base(node) { }
    }
}
