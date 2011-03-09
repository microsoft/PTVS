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
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.TestSccPackage {
    public class TestSccProvider : IVsSccProvider, IVsSccManager2, IVsQueryEditQuerySave2 {
        private static readonly List<string> _failures = new List<string>();
        private static readonly Dictionary<IVsSccProject2, ProjectInfo> _loadedProjects = new Dictionary<IVsSccProject2, ProjectInfo>();
        private static TestSccProvider _provider;

        public TestSccProvider() {
            _provider = this;
        }

        #region IVsSccManager2 Members

        public int GetSccGlyph(int cFiles, string[] rgpszFullPaths, VsStateIcon[] rgsiGlyphs, uint[] rgdwSccStatus) {
            for (int i = 0; i < cFiles; i++) {
                bool found = false;
                foreach (var proj in _loadedProjects.Values) {
                    FileInfo fi;
                    if (proj.Files.TryGetValue(rgpszFullPaths[i], out fi)) {
                        rgsiGlyphs[i] = fi.StateIcon;
                        found = true;
                    }
                }
                if (!found) {
                    rgsiGlyphs[i] = VsStateIcon.STATEICON_CHECKEDOUT;
                }
            }
            return VSConstants.S_OK;
        }

        public int GetSccGlyphFromStatus(uint dwSccStatus, VsStateIcon[] psiGlyph) {
            return VSConstants.E_FAIL;
        }

        public int RegisterSccProject(IVsSccProject2 pscp2Project, string pszSccProjectName, string pszSccAuxPath, string pszSccLocalPath, string pszProvider) {
            if (ExpectedProjectName != null) {
                AreEqual(ExpectedProjectName, pszSccProjectName);
            }
            if (ExpectedAuxPath != null) {
                AreEqual(ExpectedAuxPath, pszSccAuxPath);
            }
            if (ExpectedLocalPath != null) {
                AreEqual(ExpectedLocalPath, pszSccLocalPath);
            }
            if (ExpectedProvider != null) {
                AreEqual(ExpectedProvider, pszProvider);
            }

            var project = _loadedProjects[pscp2Project] = new ProjectInfo(pscp2Project, pszSccProjectName);
            
            CALPOLESTR[] str = new CALPOLESTR[1];
            CADWORD[] cadword = new CADWORD[1];
            // try it once w/ the correct item ID
            var projectId = GetItemId((IVsHierarchy)pscp2Project);
            if (ErrorHandler.Failed(pscp2Project.GetSccFiles(projectId, str, cadword))) {
                Fail("Failed to get SccFiles");
            }
            string[] files = UnpackCALPOLESTR(str[0]);
            foreach (var file in files) {
                uint pItemId;
                if (ErrorHandler.Succeeded(((IVsHierarchy)pscp2Project).ParseCanonicalName(file, out pItemId))) {
                    project.Files.Add(file, new FileInfo(project, file, pItemId));
                }
            }

            // try it once w/ VSITEMID_ROOT, make sure we get the same count back...
            if (ErrorHandler.Failed(pscp2Project.GetSccFiles(VSConstants.VSITEMID_ROOT, str, cadword))) {
                Fail("Failed to get SccFiles");
            }
            AreEqual(UnpackCALPOLESTR(str[0]).Length, files.Length);


            // couple extra test cases to make sure we handle weird inputs...
            pscp2Project.GetSccFiles(projectId, null, null);
            pscp2Project.GetSccFiles(projectId, new CALPOLESTR[0], new CADWORD[0]);

            AreEqual(VSConstants.E_INVALIDARG, pscp2Project.GetSccFiles(VSConstants.VSITEMID_SELECTION, null, null));
            AreEqual(VSConstants.E_INVALIDARG, pscp2Project.GetSccFiles(0xffffffff, null, null));

            pscp2Project.GetSccSpecialFiles(projectId, "", str, cadword);
            pscp2Project.GetSccSpecialFiles(projectId, "", null, null);

            AreEqual(VSConstants.E_INVALIDARG, pscp2Project.GetSccSpecialFiles(VSConstants.VSITEMID_SELECTION, "", null, null));
            AreEqual(VSConstants.E_INVALIDARG, pscp2Project.GetSccSpecialFiles(0xffffffff, "", null, null));

            return VSConstants.S_OK;
        }

        public static string[] UnpackCALPOLESTR(CALPOLESTR strings) {
            if (strings.pElems != IntPtr.Zero) {
                string[] res = new string[strings.cElems];
                int size = IntPtr.Size;
                for (int i = 0; i < res.Length; i++) {
                    IntPtr strAddr = Marshal.ReadIntPtr(new IntPtr(strings.pElems.ToInt64() + size * i));
                    res[i] = Marshal.PtrToStringUni(strAddr);
                    Marshal.FreeCoTaskMem(strAddr);
                }
                Marshal.FreeCoTaskMem(strings.pElems);

                return res;
            }
            return new string[0];
        }

        public int UnregisterSccProject(IVsSccProject2 pscp2Project) {
            _loadedProjects.Remove(pscp2Project);
            return VSConstants.S_OK;
        }

        #region Obsolete Members

        public int BrowseForProject(out string pbstrDirectory, out int pfOK) {
            // obsolete
            pfOK = 0;
            pbstrDirectory = null;
            return VSConstants.E_NOTIMPL;
        }

        public int IsInstalled(out int pbInstalled) {
            // should always return true
            pbInstalled = 1;
            return VSConstants.S_OK;
        }

        public int CancelAfterBrowseForProject() {
            // obsolete
            return VSConstants.S_OK;
        }
        #endregion


        #endregion

        #region IVsQueryEditQuerySave2 Members

        public int BeginQuerySaveBatch() {
            return VSConstants.S_OK;
        }

        public int DeclareReloadableFile(string pszMkDocument, uint rgf, VSQEQS_FILE_ATTRIBUTE_DATA[] pFileInfo) {
            return VSConstants.S_OK;
        }

        public int DeclareUnreloadableFile(string pszMkDocument, uint rgf, VSQEQS_FILE_ATTRIBUTE_DATA[] pFileInfo) {
            return VSConstants.S_OK;
        }

        public int EndQuerySaveBatch() {           
            return VSConstants.S_OK;
        }

        public int IsReloadable(string pszMkDocument, out int pbResult) {
            pbResult = 1;
            return VSConstants.S_OK;
        }

        public int OnAfterSaveUnreloadableFile(string pszMkDocument, uint rgf, VSQEQS_FILE_ATTRIBUTE_DATA[] pFileInfo) {
            return VSConstants.S_OK;
        }

        public int QueryEditFiles(uint rgfQueryEdit, int cFiles, string[] rgpszMkDocuments, uint[] rgrgf, VSQEQS_FILE_ATTRIBUTE_DATA[] rgFileInfo, out uint pfEditVerdict, out uint prgfMoreInfo) {
            prgfMoreInfo = 0;
            pfEditVerdict = 0;
            return VSConstants.S_OK;
        }

        public int QuerySaveFile(string pszMkDocument, uint rgf, VSQEQS_FILE_ATTRIBUTE_DATA[] pFileInfo, out uint pdwQSResult) {
            
            if (Path.GetFileNameWithoutExtension(pszMkDocument) == "FailSave") {
                pdwQSResult = (uint)tagVSQuerySaveResult.QSR_NoSave_Cancel;
            } else {
                pdwQSResult = (uint)tagVSQuerySaveResult.QSR_SaveOK;
            }
            return VSConstants.S_OK;
        }

        public int QuerySaveFiles(uint rgfQuerySave, int cFiles, string[] rgpszMkDocuments, uint[] rgrgf, VSQEQS_FILE_ATTRIBUTE_DATA[] rgFileInfo, out uint pdwQSResult) {
            pdwQSResult = 0;
            return VSConstants.S_OK;
        }

        #endregion

        #region Test API

        internal void AreEqual(object x, object y) {
            if (x == null) {
                if (y != null) {
                    Fail("{0} != {1}", x, y);
                }
            } else if (!x.Equals(y)) {
                Fail("{0} != {1}", x, y);
            }
        }

        internal static void Fail(string msg, params object[] args) {
            _failures.Add(String.Format(msg, args));
        }

        public static List<string> Failures {
            get {
                return _failures;
            }
        }
        public static string ExpectedProjectName { get; set; }
        public static string ExpectedAuxPath { get; set; }
        public static string ExpectedLocalPath { get; set; }
        public static string ExpectedProvider { get; set; }

        public static ICollection<ProjectInfo> LoadedProjects {
            get {
                return _loadedProjects.Values;
            }
        }

        #endregion

        #region IVsSccProvider

        public int AnyItemsUnderSourceControl(out int pfResult) {
            pfResult = 0;
            return VSConstants.S_OK;
        }

        public int SetActive() {
            return VSConstants.S_OK;
        }

        public int SetInactive() {
            return VSConstants.S_OK;
        }

        #endregion

        private uint GetItemId(IVsHierarchy hierarchy) {
            object extObject;
            uint itemId = 0;
            IVsHierarchy tempHierarchy;

            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_BrowseObject, out extObject);

            IVsBrowseObject browseObject = extObject as IVsBrowseObject;

            if (browseObject != null) {
                browseObject.GetProjectItem(out tempHierarchy, out itemId);
            }

            return itemId;
        }

        public static TestSccProvider Provider {
            get {
                return _provider;
            }
        }
    }

    public class ProjectInfo {
        private readonly IVsSccProject2 _project;
        private readonly string _projectName;
        private readonly Dictionary<string, FileInfo> _files = new Dictionary<string, FileInfo>();

        public ProjectInfo(IVsSccProject2 project, string projectName) {
            _project = project;
            _projectName = projectName;
        }

        public Dictionary<string, FileInfo> Files {
            get {
                return _files;
            }
        }

        public IVsSccProject2 SccProject {
            get {
                return _project;
            }
        }

        public void AllGlyphsChanged() {
            int hr;
            if (ErrorHandler.Failed(hr =  _project.SccGlyphChanged(0, null, null, null))) {
                TestSccProvider.Fail("Bad HR from SccGlyphChanged (AllGlyphsChanged): {0}", hr);
            }
        }
    }


    public class FileInfo {
        private readonly ProjectInfo _project;
        private readonly string _name;
        private readonly uint _itemid;
        private VsStateIcon _icon;

        public FileInfo(ProjectInfo project, string name, uint itemid) {
            _name = name;
            _itemid = itemid;
            _icon = VsStateIcon.STATEICON_CHECKEDOUT;
            _project = project;
        }

        public void GlyphChanged(VsStateIcon icon) {
            int hr;
            _icon = icon;
            if (ErrorHandler.Failed(hr = _project.SccProject.SccGlyphChanged(1, new[] { ItemId }, new[] { icon }, new uint[0]))) {
                TestSccProvider.Fail("Bad HR from SccGlyphChanged: {0}", hr);
            }
        }

        public string Name {
            get {
                return _name;
            }
        }

        public uint ItemId {
            get {
                return _itemid;
            }
        }

        public VsStateIcon StateIcon {
            get {
                return _icon;
            }
            set {
                _icon = value;
            }
        }
    }


}
