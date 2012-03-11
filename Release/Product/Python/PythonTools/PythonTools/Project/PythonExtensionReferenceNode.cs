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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    [CLSCompliant(false)]
    [ComVisible(true)]
    public class PythonExtensionReferenceNode : ReferenceNode {
        private readonly string _filename;              // The name of the assembly this refernce represents
        private Automation.OAPythonExtensionReference _automationObject;
        private FileChangeManager _fileChangeListener;  // Defines the listener that would listen on file changes on the nested project node.
        private bool _isDisposed, _failedToAnalyze;

        internal PythonExtensionReferenceNode(PythonProjectNode root, string filename)
            : this(root, null, filename) {
        }

        internal PythonExtensionReferenceNode(PythonProjectNode root, ProjectElement element, string filename)
            : base(root, element) {
            _filename = filename;

            var interp = root.GetInterpreter() as IPythonInterpreter2;
            if (interp != null) {
                AnalyzeReference(interp);
            }
            InitializeFileChangeEvents();
        }

        private void AnalyzeReference(IPythonInterpreter2 interp) {
            _failedToAnalyze = false;
            var task = interp.AddReferenceAsync(new ProjectReference(_filename, ProjectReferenceKind.ExtensionModule));

            // check if we get an exception, and if so mark ourselves as a dangling reference.
            task.ContinueWith(new TaskFailureHandler(TaskScheduler.FromCurrentSynchronizationContext(), this).HandleAddRefFailure);
        }

        class TaskFailureHandler {
            private readonly TaskScheduler _uiScheduler;
            private readonly PythonExtensionReferenceNode _node;
            public TaskFailureHandler(TaskScheduler uiScheduler, PythonExtensionReferenceNode refNode) {
                _uiScheduler = uiScheduler;
                _node = refNode;
            }

            public void HandleAddRefFailure(Task task) {
                if (task.Exception != null) {
                    Task.Factory.StartNew(MarkFailed, default(CancellationToken), TaskCreationOptions.None, _uiScheduler);
                }
            }

            public void MarkFailed() {
                _node._failedToAnalyze = true;
            }
        }

        public override string Url {
            get {
                return _filename;
            }
        }

        public override string Caption {
            get {
                return Path.GetFileName(_filename);
            }
        }

        internal override object Object {
            get {
                if (null == _automationObject) {
                    _automationObject = new Automation.OAPythonExtensionReference(this);
                }
                return _automationObject;
            }
        }

        #region methods

        /// <summary>
        /// Closes the node.
        /// </summary>
        /// <returns></returns>
        public override int Close() {
            try {
                Dispose(true);
            } finally {
                base.Close();
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Links a reference node to the project and hierarchy.
        /// </summary>
        protected override void BindReferenceData() {
            Debug.Assert(_filename != null, "The _filename field has not been initialized");

            // If the item has not been set correctly like in case of a new reference added it now.
            // The constructor for the AssemblyReference node will create a default project item. In that case the Item is null.
            // We need to specify here the correct project element. 
            if (ItemNode == null || ItemNode is VirtualProjectElement) {
                ItemNode = new MsBuildProjectElement(ProjectMgr, _filename, ProjectFileConstants.Reference);
            }

            // Set the basic information we know about
            ItemNode.SetMetadata(ProjectFileConstants.Name, Path.GetFileName(_filename));
            string relativePath = CommonUtils.GetRelativeFilePath(ProjectMgr.ProjectFolder, _filename);
            ItemNode.SetMetadata(PythonConstants.PythonExtension, relativePath);
        }

        /// <summary>
        /// Disposes the node
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }

            try {
                UnregisterFromFileChangeService();
            } finally {
                base.Dispose(disposing);
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Checks if an assembly is already added. The method parses all references and compares the full assemblynames, or the location of the assemblies to decide whether two assemblies are the same.
        /// </summary>
        /// <returns>true if the assembly has already been added.</returns>
        protected override bool IsAlreadyAdded() {
            ReferenceContainerNode referencesFolder = ProjectMgr.FindChild(ReferenceContainerNode.ReferencesNodeVirtualName) as ReferenceContainerNode;
            Debug.Assert(referencesFolder != null, "Could not find the References node");

            for (HierarchyNode n = referencesFolder.FirstChild; n != null; n = n.NextSibling) {
                var extensionRefNode = n as PythonExtensionReferenceNode;
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
            if (String.IsNullOrEmpty(_filename) || !File.Exists(_filename) || _failedToAnalyze) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers with File change events
        /// </summary>
        private void InitializeFileChangeEvents() {
            _fileChangeListener = new FileChangeManager(ProjectMgr.Site);
            _fileChangeListener.FileChangedOnDisk += OnExtensionChangedOnDisk;
        }

        /// <summary>
        /// Unregisters this node from file change notifications.
        /// </summary>
        private void UnregisterFromFileChangeService() {
            _fileChangeListener.FileChangedOnDisk -= OnExtensionChangedOnDisk;
            _fileChangeListener.Dispose();
        }

        /// <summary>
        /// Event callback. Called when one of the extension files are changed.
        /// </summary>
        /// <param name="sender">The FileChangeManager object.</param>
        /// <param name="e">Event args containing the file name that was updated.</param>
        private void OnExtensionChangedOnDisk(object sender, FileChangedOnDiskEventArgs e) {
            Debug.Assert(e != null, "No event args specified for the FileChangedOnDisk event");


            var interp = ((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreter2;
            if (interp != null && CommonUtils.IsSamePath(e.FileName, _filename)) {
                if ((e.FileChangeFlag & (_VSFILECHANGEFLAGS.VSFILECHG_Attr | _VSFILECHANGEFLAGS.VSFILECHG_Size | _VSFILECHANGEFLAGS.VSFILECHG_Time | _VSFILECHANGEFLAGS.VSFILECHG_Add)) != 0) {
                    // file was modified, unload and reload the extension module from our database.
                    interp.RemoveReference(new ProjectReference(_filename, ProjectReferenceKind.ExtensionModule));

                    AnalyzeReference(interp);
                } else if ((e.FileChangeFlag & _VSFILECHANGEFLAGS.VSFILECHG_Del) != 0) {
                    // file was deleted, unload from our extension database
                    interp.RemoveReference(new ProjectReference(_filename, ProjectReferenceKind.ExtensionModule));
                    OnInvalidateItems(Parent);
                }
            }
        }

        /// <summary>
        /// Overridden method. The method updates the build dependency list before removing the node from the hierarchy.
        /// </summary>
        public override void Remove(bool removeFromStorage) {
            if (ProjectMgr == null) {
                return;
            }

            var interp = ((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreter2;
            if (interp != null) {
                interp.RemoveReference(new ProjectReference(_filename, ProjectReferenceKind.ExtensionModule));
            }
            ItemNode.RemoveFromProjectFile();
            base.Remove(removeFromStorage);
        }

        #endregion
    }
}
