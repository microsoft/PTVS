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
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreters;
using Microsoft.VisualStudioTools;
using AutomationBrowsableAttribute = Microsoft.VisualStudioTools.Project.AutomationBrowsableAttribute;
using HierarchyNode = Microsoft.VisualStudioTools.Project.HierarchyNode;
using NodeProperties = Microsoft.VisualStudioTools.Project.NodeProperties;

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

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName {
            get {
                return Path.GetFileName(CommonUtils.TrimEndSeparator(this.HierarchyNode.Url));
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        [AutomationBrowsable(true)]
        public string FullPath {
            get {
                return this.HierarchyNode.Url;
            }
        }

#if DEBUG
        [SRCategory(SR.Misc)]
        [LocDisplayName(SR.EnvironmentIdDisplayName)]
        [SRDescription(SR.EnvironmentIdDescription)]
        [AutomationBrowsable(true)]
        public string Id {
            get {
                var fact = Factory;
                return fact != null ? fact.Id.ToString("B") : "";
            }
        }
#endif

        [SRCategory(SR.Misc)]
        [LocDisplayName(SR.EnvironmentVersionDisplayName)]
        [SRDescription(SR.EnvironmentVersionDescription)]
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
        [LocDisplayName(SR.BaseInterpreterDisplayName)]
        [SRDescription(SR.BaseInterpreterDescription)]
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
