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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonReferenceContainerNode : CommonReferenceContainerNode {
        public PythonReferenceContainerNode(PythonProjectNode root)
            : base(root) {
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(ProjectElement element) {
            return new PythonProjectReferenceNode(ProjectMgr, element);
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            return new PythonProjectReferenceNode(ProjectMgr, selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
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
            } else if (referenceType == ProjectFileConstants.WebPiReference) {
                return new WebPiReferenceNode(
                    ProjectMgr,
                    element,
                    element.GetMetadata("Feed"),
                    element.GetMetadata("ProductId"),
                    element.GetMetadata("FriendlyName")
                );
            }

            return base.CreateReferenceNode(referenceType, element);
        }

        protected override ReferenceNode CreateReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            ReferenceNode node = null;
            switch (selectorData.type) {
                case VSCOMPONENTTYPE.VSCOMPONENTTYPE_Custom:
                    if (selectorData.lCustom == 0) {
                        node = new WebPiReferenceNode(
                            (PythonProjectNode)ProjectMgr,
                            selectorData.bstrFile,
                            selectorData.bstrTitle,
                            selectorData.bstrProjRef
                        );

                    }
                    break;
                default:
                    node = base.CreateReferenceNode(selectorData);
                    break;
            }

            return node;
        }
    }
}
