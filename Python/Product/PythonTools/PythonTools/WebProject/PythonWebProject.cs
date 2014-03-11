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
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project.Web {
    [Guid("742BB562-7AEE-4FC7-8CD2-48D66C8CC435")]
    partial class PythonWebProject :
        FlavoredProjectBase,
        IOleCommandTarget,
        IVsProjectFlavorCfgProvider,
        IVsProject,
        IVsFilterAddProjectItemDlg
    {
        private PythonToolsPackage _package;
        internal IVsProject _innerProject;
        internal IVsProject3 _innerProject3;
        private IVsProjectFlavorCfgProvider _innerVsProjectFlavorCfgProvider;
        private static Guid PythonProjectGuid = new Guid(PythonConstants.ProjectFactoryGuid);
        private IOleCommandTarget _menuService;

        public PythonWebProject() {
        }

        internal PythonToolsPackage Package {
            get { return _package; }
            set {
                Debug.Assert(_package == null);
                if (_package != null) {
                    throw new InvalidOperationException("PythonWebProject.Package must only be set once");
                }
                _package = value;
            }
        }

        #region IVsAggregatableProject

        /// <summary>
        /// Do the initialization here (such as loading flavor specific
        /// information from the project)
        /// </summary>
        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel) {
            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);

            var proj = _innerVsHierarchy.GetProject();

            if (proj != null) {
                try {
                    dynamic webAppExtender = proj.get_Extender("WebApplication");
                    if (webAppExtender != null) {
                        webAppExtender.StartWebServerOnDebug = false;
                    }
                } catch (COMException) {
                    // extender doesn't exist...
                }
            }
        }

        #endregion

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidOfficeSharePointCmdSet) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    // Report it as supported so that it's not routed any
                    // further, but disable it and make it invisible.
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_INVISIBLE);
                }
                return VSConstants.S_OK;
            }

            return base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }


        protected override void SetInnerProject(IntPtr innerIUnknown) {
            var inner = Marshal.GetObjectForIUnknown(innerIUnknown);

            // The reason why we keep a reference to those is that doing a QI after being
            // aggregated would do the AddRef on the outer object.
            _innerVsProjectFlavorCfgProvider = inner as IVsProjectFlavorCfgProvider;
            _innerProject = inner as IVsProject;
            _innerProject3 = inner as IVsProject3;
            _innerVsHierarchy = inner as IVsHierarchy;

            // Ensure we have a service provider as this is required for menu items to work
            if (this.serviceProvider == null) {
                this.serviceProvider = (System.IServiceProvider)Package;
            }

            // Now let the base implementation set the inner object
            base.SetInnerProject(innerIUnknown);

            // Get access to the menu service used by FlavoredProjectBase. We
            // need to forward IOleCommandTarget functions to this object, since
            // we override the FlavoredProjectBase implementation with no way to
            // call it directory.
            // (This must run after we called base.SetInnerProject)
            _menuService = (IOleCommandTarget)((System.IServiceProvider)this).GetService(typeof(IMenuCommandService));
            if (_menuService == null) {
                throw new InvalidOperationException("Cannot initialize Web project");
            }
        }

        protected override int GetProperty(uint itemId, int propId, out object property) {
            switch ((__VSHPROPID2)propId) {
                case __VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList: {
                        var res = base.GetProperty(itemId, propId, out property);
                        if (ErrorHandler.Succeeded(res)) {
                            var guids = GetGuidsFromList(property as string);
                            guids.RemoveAll(g => CfgSpecificPropertyPagesToRemove.Contains(g));
                            guids.AddRange(CfgSpecificPropertyPagesToAdd);
                            property = MakeListFromGuids(guids);
                        }
                        return res;
                    }
                case __VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList: {
                        var res = base.GetProperty(itemId, propId, out property);
                        if (ErrorHandler.Succeeded(res)) {
                            var guids = GetGuidsFromList(property as string);
                            guids.RemoveAll(g => PropertyPagesToRemove.Contains(g));
                            guids.AddRange(PropertyPagesToAdd);
                            property = MakeListFromGuids(guids);
                        }
                        return res;
                    }
            }

            return base.GetProperty(itemId, propId, out property);
        }

        private static Guid[] PropertyPagesToAdd = new[] {
            new Guid(PythonConstants.WebPropertyPageGuid)
        };

        private static Guid[] CfgSpecificPropertyPagesToAdd = new Guid[0];

        private static HashSet<Guid> PropertyPagesToRemove = new HashSet<Guid> { 
            new Guid("{8C0201FE-8ECA-403C-92A3-1BC55F031979}"),   // typeof(DeployPropertyPageComClass)
            new Guid("{ED3B544C-26D8-4348-877B-A1F7BD505ED9}"),   // typeof(DatabaseDeployPropertyPageComClass)
            new Guid("{909D16B3-C8E8-43D1-A2B8-26EA0D4B6B57}"),   // Microsoft.VisualStudio.Web.Application.WebPropertyPage
            new Guid("{379354F2-BBB3-4BA9-AA71-FBE7B0E5EA94}"),   // Microsoft.VisualStudio.Web.Application.SilverlightLinksPage
        };

        internal static HashSet<Guid> CfgSpecificPropertyPagesToRemove = new HashSet<Guid> { 
            new Guid("{A553AD0B-2F9E-4BCE-95B3-9A1F7074BC27}"),   // Package/Publish Web 
            new Guid("{9AB2347D-948D-4CD2-8DBE-F15F0EF78ED3}"),   // Package/Publish SQL 
        };

        private static List<Guid> GetGuidsFromList(string guidList) {
            if (string.IsNullOrEmpty(guidList)) {
                return new List<Guid>();
            }

            Guid value;
            return guidList.Split(';')
                .Select(str => Guid.TryParse(str, out value) ? (Guid?)value : null)
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();
        }

        private static string MakeListFromGuids(IEnumerable<Guid> guidList) {
            return string.Join(";", guidList.Select(g => g.ToString("B")));
        }

        internal string RemovePropertyPagesFromList(string propertyPagesList, string[] pagesToRemove) {
            if (pagesToRemove == null || !pagesToRemove.Any()) {
                return propertyPagesList;
            }

            var guidsToRemove = new HashSet<Guid>(
                pagesToRemove.Select(str => { Guid guid; return Guid.TryParse(str, out guid) ? guid : Guid.Empty; })
            );
            guidsToRemove.Add(Guid.Empty);

            return string.Join(
                ";",
                propertyPagesList.Split(';')
                    .Where(str => !string.IsNullOrEmpty(str))
                    .Select(str => { Guid guid; return Guid.TryParse(str, out guid) ? guid : Guid.Empty; })
                    .Except(guidsToRemove)
                    .Select(guid => guid.ToString("B"))
            );
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == GuidList.guidWebPackgeCmdId) {
                if (nCmdID == 0x101 /*  EnablePublishToWindowsAzureMenuItem*/) {

                    // We need to forward the command to the web publish package and let it handle it, while
                    // we listen for the project which is going to get added.  After the command succeds
                    // we can then go and update the newly added project so that it is setup appropriately for
                    // Python...
                    using (var listener = new AzureSolutionListener(this)) {
                        listener.Init();

                        var shell = (IVsShell)((System.IServiceProvider)this).GetService(typeof(SVsShell));
                        var webPublishPackageGuid = GuidList.guidWebPackageGuid;
                        IVsPackage package;

                        int res = shell.LoadPackage(ref webPublishPackageGuid, out package);
                        if (!ErrorHandler.Succeeded(res)) {
                            return res;
                        }

                        var cmdTarget = package as IOleCommandTarget;
                        if (cmdTarget != null) {
                            res = cmdTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                            if (ErrorHandler.Succeeded(res)) {
                                // update the users service definition file to include import...
                                foreach (var project in listener.OpenedHierarchies) {
                                    UpdateAzureDeploymentProject(project);
                                }
                            }
                            return res;
                        }
                    }
                }
            }

            return _menuService.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private void UpdateAzureDeploymentProject(IVsHierarchy project) {
            object projKind;
            if (!ErrorHandler.Succeeded(project.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_TypeName, out projKind)) ||
                !(projKind is string) ||
                (string)projKind != "CloudComputingProjectType") {
                return;
            }

            // first try and update the file through the RDT.  If it's open we want to make sure
            // that VS is aware of the change.
            // https://nodejstools.codeplex.com/workitem/480
            IVsRunningDocumentTable rdt = PythonToolsPackage.GetGlobalService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            IEnumRunningDocuments enumDocs;
            ErrorHandler.ThrowOnFailure(rdt.GetRunningDocumentsEnum(out enumDocs));
            uint[] doc = new uint[1];
            uint fetched;
            while (ErrorHandler.Succeeded(enumDocs.Next(1, doc, out fetched)) && fetched == 1) {
                uint flags;
                uint readLocks, editLocks, itemid;
                string filename;
                IVsHierarchy hierarchy;
                IntPtr docData;

                ErrorHandler.ThrowOnFailure(
                    rdt.GetDocumentInfo(
                        doc[0],
                        out flags,
                        out readLocks,
                        out editLocks,
                        out filename,
                        out hierarchy,
                        out itemid,
                        out docData
                    )
                );
                try {
                    if (hierarchy == project && docData != IntPtr.Zero) {
                        if (String.Equals(Path.GetFileName(filename), "ServiceDefinition.csdef", StringComparison.OrdinalIgnoreCase)) {
                            var adapterFactory = PythonToolsPackage.ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
                            var obj = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData);
                            var vsTextBuffer = obj as IVsTextBuffer;
                            if (vsTextBuffer != null) {
                                var textBuffer = adapterFactory.GetDocumentBuffer(vsTextBuffer);
                                using (var edit = textBuffer.CreateEdit()) {
                                    if (textBuffer != null) {
                                        edit.Replace(
                                            new Span(0, textBuffer.CurrentSnapshot.Length),
                                            UpdateServiceDefinition(textBuffer.CurrentSnapshot.GetText())
                                        );
                                        edit.Apply();
                                    }

                                    string newDoc;
                                    int fCancelled;
                                    if (ErrorHandler.Succeeded(
                                        ((IVsPersistDocData)vsTextBuffer).SaveDocData(
                                            VSSAVEFLAGS.VSSAVE_SilentSave,
                                            out newDoc,
                                            out fCancelled
                                            )
                                    )) {
                                        // we've successfully updated the file via the RDT
                                        return;
                                    }
                                }
                            }
                        }
                    }
                } finally {
                    if (docData != IntPtr.Zero) {
                        Marshal.Release(docData);
                    }
                }
            }

            // didn't find the file in the RDT, update it on disk the old fashioned way
            var dteProject = project.GetProject();
            var serviceDef = dteProject.ProjectItems.Item("ServiceDefinition.csdef");
            if (serviceDef != null && serviceDef.FileCount == 1) {
                string filename = serviceDef.FileNames[0];
                string tmpFile = filename + ".tmp";
                File.WriteAllText(tmpFile, UpdateServiceDefinition(File.ReadAllText(filename)));
                File.Delete(filename);
                File.Move(tmpFile, filename);
            }
        }

        private static string UpdateServiceDefinition(string input) {
            List<string> elements = new List<string>();
            XmlWriterSettings settings = new XmlWriterSettings() { Indent = true, IndentChars = " ", NewLineHandling = NewLineHandling.Entitize };
            var strWriter = new StringWriter();
            using (var reader = XmlReader.Create(new StringReader(input))) {
                using (var writer = XmlWriter.Create(strWriter, settings)) {
                    while (reader.Read()) {
                        switch (reader.NodeType) {
                            case XmlNodeType.Element:
                                // TODO: Switch to the code below when we can successfully install our module...
                                if (reader.Name == "Imports" &&
                                        elements.Count == 2 &&
                                        elements[0] == "ServiceDefinition" &&
                                        elements[1] == "WebRole") {
                                    // insert our Imports node
                                    writer.WriteStartElement("Startup");
                                    writer.WriteStartElement("Task");
                                    writer.WriteAttributeString("commandLine", "Microsoft.PythonTools.AzureSetup.exe");
                                    writer.WriteAttributeString("executionContext", "elevated");
                                    writer.WriteAttributeString("taskType", "simple");

                                    writer.WriteStartElement("Environment");
                                    writer.WriteStartElement("Variable");
                                    writer.WriteAttributeString("name", "EMULATED");
                                    writer.WriteStartElement("RoleInstanceValue");
                                    writer.WriteAttributeString("xpath", "/RoleEnvironment/Deployment/@emulated");

                                    writer.WriteEndElement(); // RoleInstanceValue
                                    writer.WriteEndElement(); // Variable
                                    writer.WriteEndElement(); // Environment
                                    writer.WriteEndElement(); // Task
                                    writer.WriteEndElement(); // Startup
                                }
                                writer.WriteStartElement(reader.Prefix, reader.Name, reader.NamespaceURI);
                                writer.WriteAttributes(reader, true);

                                if (!reader.IsEmptyElement) {
                                    /*
                                    if (reader.Name == "Imports" &&
                                        elements.Count == 2 &&
                                        elements[0] == "ServiceDefinition" &&
                                        elements[1] == "WebRole") {

                                        writer.WriteStartElement("Import");
                                        writer.WriteAttributeString("moduleName", "PythonTools");
                                        writer.WriteEndElement();
                                    }*/

                                    elements.Add(reader.Name);
                                } else {
                                    writer.WriteEndElement();
                                }
                                break;
                            case XmlNodeType.Text:
                                writer.WriteString(reader.Value);
                                break;
                            case XmlNodeType.EndElement:
                                writer.WriteFullEndElement();
                                elements.RemoveAt(elements.Count - 1);
                                break;
                            case XmlNodeType.XmlDeclaration:
                            case XmlNodeType.ProcessingInstruction:
                                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                                break;
                            case XmlNodeType.SignificantWhitespace:
                                writer.WriteWhitespace(reader.Value);
                                break;
                            case XmlNodeType.Attribute:
                                writer.WriteAttributes(reader, true);
                                break;
                            case XmlNodeType.CDATA:
                                writer.WriteCData(reader.Value);
                                break;
                            case XmlNodeType.Comment:
                                writer.WriteComment(reader.Value);
                                break;
                        }
                    }
                }
            }

            return strWriter.ToString();
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidEureka) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x102: // View in Web Page Inspector from Eureka web tools
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidVenusCmdId) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x034: /* add app assembly folder */
                        case 0x035: /* add app code folder */
                        case 0x036: /* add global resources */
                        case 0x037: /* add local resources */
                        case 0x038: /* add web refs folder */
                        case 0x039: /* add data folder */
                        case 0x040: /* add browser folders */
                        case 0x041: /* theme */
                        case 0x054: /* package settings */
                        case 0x055: /* context package settings */

                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidWebAppCmdId) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x06A: /* check accessibility */
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == VSConstants.VSStd2K) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd2KCmdID.SETASSTARTPAGE:
                        case VSConstants.VSStd2KCmdID.CHECK_ACCESSIBILITY:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.PreviewInBrowser:
                        case VSConstants.VSStd97CmdID.BrowseWith:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            }

            return _menuService.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #region IVsProjectFlavorCfgProvider Members

        public int CreateProjectFlavorCfg(IVsCfg pBaseProjectCfg, out IVsProjectFlavorCfg ppFlavorCfg) {
            // We're flavored with a Web Application project and our normal
            // project...  But we don't want the web application project to
            // influence our config as that alters our debug launch story.  We
            // control that w/ the web project which is actually just letting
            // the base Python project handle it. So we keep the base Python
            // project config here.
            IVsProjectFlavorCfg webCfg;
            ErrorHandler.ThrowOnFailure(
                _innerVsProjectFlavorCfgProvider.CreateProjectFlavorCfg(
                    pBaseProjectCfg,
                    out webCfg
                )
            );
            ppFlavorCfg = new PythonWebProjectConfig(pBaseProjectCfg, webCfg);
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsProject Members

        int IVsProject.AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult) {
            return _innerProject.AddItem(itemidLoc, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, pResult);
        }

        int IVsProject.GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot, out string pbstrItemName) {
            return _innerProject.GenerateUniqueItemName(itemidLoc, pszExt, pszSuggestedRoot, out pbstrItemName);
        }

        int IVsProject.GetItemContext(uint itemid, out VisualStudio.OLE.Interop.IServiceProvider ppSP) {
            return _innerProject.GetItemContext(itemid, out ppSP);
        }

        int IVsProject.GetMkDocument(uint itemid, out string pbstrMkDocument) {
            return _innerProject.GetMkDocument(itemid, out pbstrMkDocument);
        }

        int IVsProject.IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid) {
            return _innerProject.IsDocumentInProject(pszMkDocument, out pfFound, pdwPriority, out pitemid);
        }

        int IVsProject.OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
            return _innerProject.OpenItem(itemid, rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }

        #endregion

        #region IVsFilterAddProjectItemDlg Members

        int IVsFilterAddProjectItemDlg.FilterListItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterListItemByTemplateFile(ref Guid rguidProjectItemTemplates, string pszTemplateFile, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterTreeItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterTreeItemByTemplateDir(ref Guid rguidProjectItemTemplates, string pszTemplateDir, out int pfFilter) {
            // https://pytools.codeplex.com/workitem/1313
            // ASP.NET will filter some things out, including .css files, which we don't want it to do.
            // So we shut that down by not forwarding this to any inner projects, which is fine, because
            // Python projects don't implement this interface either.
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        #endregion
    }
}
