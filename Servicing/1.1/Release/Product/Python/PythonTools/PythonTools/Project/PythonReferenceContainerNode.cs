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
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    class PythonReferenceContainerNode : CommonReferenceContainerNode {
        public PythonReferenceContainerNode(PythonProjectNode root)
            : base(root) {
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(ProjectElement element) {
            return new PythonProjectReferenceNode(this.ProjectMgr, element);
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            return new PythonProjectReferenceNode(this.ProjectMgr, selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
        }

        protected override AssemblyReferenceNode CreateAssemblyReferenceNode(ProjectElement element) {
            AssemblyReferenceNode node = null;
            try {
                node = new PythonAssemblyReferenceNode((PythonProjectNode)this.ProjectMgr, element);
            } catch (ArgumentNullException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (FileNotFoundException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (BadImageFormatException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (FileLoadException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (System.Security.SecurityException e) {
                Trace.WriteLine("Exception : " + e.Message);
            }

            return node;
        }

        protected override AssemblyReferenceNode CreateAssemblyReferenceNode(string fileName) {
            AssemblyReferenceNode node = null;
            try {
                node = new PythonAssemblyReferenceNode((PythonProjectNode)this.ProjectMgr, fileName);
            } catch (ArgumentNullException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (FileNotFoundException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (BadImageFormatException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (FileLoadException e) {
                Trace.WriteLine("Exception : " + e.Message);
            } catch (System.Security.SecurityException e) {
                Trace.WriteLine("Exception : " + e.Message);
            }

            return node;
        }

        protected override ReferenceNode CreateReferenceNode(string referenceType, ProjectElement element) {
            if (referenceType == ProjectFileConstants.Reference) {
                string pyExtension = element.GetMetadata(PythonConstants.PythonExtension);
                if (!String.IsNullOrWhiteSpace(pyExtension)) {
                    return new PythonExtensionReferenceNode((PythonProjectNode)ProjectMgr, element, pyExtension);
                }
            }

            return base.CreateReferenceNode(referenceType, element);
        }
    }
}
