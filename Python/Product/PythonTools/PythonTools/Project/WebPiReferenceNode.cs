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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using VSLangProj;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]
    internal class WebPiReferenceNode : ReferenceNode {
        private readonly string _feed;              // The name of the assembly this refernce represents
        private readonly string _productId, _friendlyName;
        private Automation.OAWebPiReference _automationObject;
        private bool _isDisposed;

        internal WebPiReferenceNode(ProjectNode root, string filename, string productId, string friendlyName)
            : this(root, null, filename, productId, friendlyName) {
        }

        internal WebPiReferenceNode(ProjectNode root, ProjectElement element, string filename, string productId, string friendlyName)
            : base(root, element) {
            Utilities.ArgumentNotNullOrEmpty("filename", filename);
            _feed = filename;
            _productId = productId;
            _friendlyName = friendlyName;
        }

        public override string Url {
            get {
                return _feed + "?" + _productId;
            }
        }

        public override string Caption {
            get {
                return _friendlyName;
            }
        }

        internal override object Object {
            get {
                if (null == _automationObject) {
                    _automationObject = new Automation.OAWebPiReference(this);
                }
                return _automationObject;
            }
        }

#if DEV14_OR_LATER
        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.XWorldFile;
        }
#else
        public override int ImageIndex {
            get {
                return ProjectMgr.GetIconIndex(ProjectNode.ImageName.XWorld);
            }
        }
#endif

        protected override NodeProperties CreatePropertiesObject() {
            return new WebPiReferenceNodeProperties(this);
        }

        #region methods

        public override Guid ItemTypeGuid {
            get {
                return VSConstants.ItemTypeGuid.VirtualFolder_guid;
            }
        }

        /// <summary>
        /// Links a reference node to the project and hierarchy.
        /// </summary>
        protected override void BindReferenceData() {
            Debug.Assert(_feed != null, "The _feed field has not been initialized");

            // If the item has not been set correctly like in case of a new reference added it now.
            // The constructor for the AssemblyReference node will create a default project item. In that case the Item is null.
            // We need to specify here the correct project element. 
            if (ItemNode == null || ItemNode is VirtualProjectElement) {
                ItemNode = new MsBuildProjectElement(
                    ProjectMgr, 
                    _feed + "?" + _productId, 
                    ProjectFileConstants.WebPiReference
                );
            }

            // Set the basic information we know about
            ItemNode.SetMetadata("Feed", _feed);
            ItemNode.SetMetadata("ProductId", _productId);
            ItemNode.SetMetadata("FriendlyName", _friendlyName);
        }

        /// <summary>
        /// Disposes the node
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }

            base.Dispose(disposing);
            _isDisposed = true;
        }

        /// <summary>
        /// Checks if an assembly is already added. The method parses all references and compares the full assemblynames, or the location of the assemblies to decide whether two assemblies are the same.
        /// </summary>
        /// <returns>true if the assembly has already been added.</returns>
        protected override bool IsAlreadyAdded() {
            ReferenceContainerNode referencesFolder = ProjectMgr.GetReferenceContainer() as ReferenceContainerNode;
            Debug.Assert(referencesFolder != null, "Could not find the References node");
            if (referencesFolder == null) {
                // Return true so that our caller does not try and add us.
                return true;
            }

            for (HierarchyNode n = referencesFolder.FirstChild; n != null; n = n.NextSibling) {
                var extensionRefNode = n as WebPiReferenceNode;
                if (null != extensionRefNode) {
                    // We will check if Url of the assemblies is the same.
                    // TODO: Check full assembly name?
                    if (CommonUtils.IsSamePath(extensionRefNode.Url, Url)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if this is node a valid node for painting the default reference icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon() {
            return true;
        }

        /// <summary>
        /// Overridden method. The method updates the build dependency list before removing the node from the hierarchy.
        /// </summary>
        public override void Remove(bool removeFromStorage) {
            if (ProjectMgr == null) {
                return;
            }

            ItemNode.RemoveFromProjectFile();
            base.Remove(removeFromStorage);
        }

        #endregion
    }


    [CLSCompliant(false), ComVisible(true)]
    public class WebPiReferenceNodeProperties : NodeProperties {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.RefName)]
        [SRDescriptionAttribute(SR.RefNameDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public override string Name {
            get {
                return this.HierarchyNode.Caption;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.WebPiFeed)]
        [SRDescriptionAttribute(SR.WebPiFeedDescription)]
        [Browsable(true)]
        public string Feed {
            get {
                return this.GetProperty("Feed", "");
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [SRDisplayName(SR.WebPiProduct)]
        [SRDescriptionAttribute(SR.WebPiProductDescription)]
        [Browsable(true)]
        public virtual string ProductId {
            get {
                return this.GetProperty("ProductID", "");
            }
        }

        [SRCategoryAttribute(SR.Advanced)]
        [SRDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        [TypeConverter(typeof(BuildActionTypeConverter))]
        public prjBuildAction BuildAction {
            get {
                return prjBuildAction.prjBuildActionNone;
            }
        }

        #endregion

        #region ctors
        internal WebPiReferenceNodeProperties(WebPiReferenceNode node)
            : base(node) {
        }
        #endregion

        #region overridden methods
        public override string GetClassName() {
            return SR.GetString(SR.WebPiReferenceProperties);
        }
        #endregion
    }

}
