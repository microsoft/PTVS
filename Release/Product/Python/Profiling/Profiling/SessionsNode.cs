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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Top level node for our UI Hierarchy which includes all of the various performance sessions.
    /// 
    /// We need one top-level node which we'll add the other nodes to.  We treat the other nodes
    /// as nested hierarchies.
    /// </summary>
    class SessionsNode : BaseHierarchyNode, IVsHierarchyDeleteHandler {
        private readonly List<SessionNode> _sessions = new List<SessionNode>();
        private readonly IVsUIHierarchyWindow _window;
        internal int _activeSession = -1;
        internal static ImageList _imageList = InitImageList();
        const string _rootName = "Python Performance Sessions";

        internal SessionsNode(IVsUIHierarchyWindow window) {
            _window = window;
        }

        internal SessionNode AddTarget(ProfilingTarget target, string filename, bool save) {
            Debug.Assert(filename.EndsWith(".pyperf"));

            // ensure a unique name
            string newBaseName = Path.GetFileNameWithoutExtension(filename);
            string tempBaseName = newBaseName;
            int append = 0;
            bool dupFound;
            do {
                dupFound = false;
                for (int i = 0; i < _sessions.Count; i++) {
                    if (Path.GetFileNameWithoutExtension(_sessions[i].Filename) == newBaseName) {
                        dupFound = true;
                    }
                }
                if (dupFound) {
                    append++;
                    newBaseName = tempBaseName + append;
                }
            } while (dupFound);

            string newFilename = newBaseName + ".pyperf";
            // add directory name back if present...
            string dirName = Path.GetDirectoryName(filename);
            if (dirName != "") {
                newFilename = Path.Combine(dirName, newFilename);
            }
            filename = newFilename;

            // save to the unique item if desired (we save whenever we have an active solution as we have a place to put it)...
            if (save) {
                using (var fs = new FileStream(filename, FileMode.Create)) {
                    ProfilingTarget.Serializer.Serialize(fs, target);
                }
            }

            var node = OpenTarget(target, filename);

            if (!save) {
                node.MarkDirty();
                node._neverSaved = true;
            }

            return node;
        }

        internal SessionNode OpenTarget(ProfilingTarget target, string filename) {
            for (int i = 0; i < _sessions.Count; i++) {
                if (_sessions[i].Filename == filename) {
                    throw new InvalidOperationException(String.Format("Performance '{0}' session is already open", filename));
                }
            }

            uint itemid = (uint)_sessions.Count;
            var node = new SessionNode(this, target, filename);
            _sessions.Add(node);

            uint prevSibl;
            if (itemid != 0) {
                prevSibl = itemid - 1;
            } else {
                prevSibl = VSConstants.VSITEMID_NIL;
            }

            OnItemAdded(VSConstants.VSITEMID_ROOT, prevSibl, itemid);

            if (_activeSession == -1) {
                SetActiveSession(node);
            }

            return node;
        }

        internal void SetActiveSession(SessionNode node) {
            int oldItem = _activeSession;
            if (oldItem != -1) {
                _window.ExpandItem(this, (uint)_activeSession, EXPANDFLAGS.EXPF_UnBoldItem);
            }

            _activeSession = _sessions.IndexOf(node);
            Debug.Assert(_activeSession != -1);

            _window.ExpandItem(this, (uint)_activeSession, EXPANDFLAGS.EXPF_BoldItem);
        }

        #region IVsUIHierarchy Members

        public override int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested) {
            if (itemid >= 0 && itemid < _sessions.Count) {
                if (iidHierarchyNested == typeof(IVsHierarchy).GUID || iidHierarchyNested == typeof(IVsUIHierarchy).GUID) {
                    ppHierarchyNested = System.Runtime.InteropServices.Marshal.GetComInterfaceForObject(_sessions[(int)itemid], typeof(IVsUIHierarchy));
                    pitemidNested = VSConstants.VSITEMID_ROOT;
                    return VSConstants.S_OK;
                }
            }

            return base.GetNestedHierarchy(itemid, ref iidHierarchyNested, out ppHierarchyNested, out pitemidNested);
        }

        public override int GetProperty(uint itemid, int propid, out object pvar) {
            // GetProperty is called many many times for this particular property
            pvar = null;
            var prop = (__VSHPROPID)propid;
            switch (prop) {
                case __VSHPROPID.VSHPROPID_CmdUIGuid:
                    pvar = new Guid(GuidList.guidPythonProfilingPkgString);
                    break;

                case __VSHPROPID.VSHPROPID_Parent:
                    if (itemid != VSConstants.VSITEMID_ROOT) {
                        pvar = VSConstants.VSITEMID_ROOT;
                    }
                    break;

                case __VSHPROPID.VSHPROPID_FirstChild:
                    if (itemid == VSConstants.VSITEMID_ROOT && _sessions.Count > 0)
                        pvar = 0;
                    else
                        pvar = VSConstants.VSITEMID_NIL;
                    break;

                case __VSHPROPID.VSHPROPID_NextSibling:
                    if (itemid != VSConstants.VSITEMID_ROOT && itemid < _sessions.Count - 1)
                        pvar = itemid + 1;
                    else
                        pvar = VSConstants.VSITEMID_NIL;
                    break;

                case __VSHPROPID.VSHPROPID_Expandable:
                    pvar = true;
                    break;

                case __VSHPROPID.VSHPROPID_ExpandByDefault:
                    if (itemid == VSConstants.VSITEMID_ROOT) {
                        pvar = true;
                    }
                    break;

                case __VSHPROPID.VSHPROPID_IconImgList:
                case __VSHPROPID.VSHPROPID_OpenFolderIconHandle:
                    pvar = (int)_imageList.Handle;
                    break;

                case __VSHPROPID.VSHPROPID_IconIndex:
                case __VSHPROPID.VSHPROPID_OpenFolderIconIndex:
                    if (itemid == VSConstants.VSITEMID_ROOT) {
                        pvar = 0;
                    } else {
                        pvar = (int)TreeViewIconIndex.PerformanceSession;
                    }
                    break;
                case __VSHPROPID.VSHPROPID_SaveName:
                    if (itemid != VSConstants.VSITEMID_ROOT) {
                        pvar = GetItem(itemid).Filename;
                    }
                    break;
                case __VSHPROPID.VSHPROPID_Caption:
                    if (itemid != VSConstants.VSITEMID_ROOT) {
                        pvar = GetItem(itemid).Caption;
                    }
                    break;

                case __VSHPROPID.VSHPROPID_ParentHierarchy:
                    if (itemid >= 0 && itemid < _sessions.Count)
                        pvar = this as IVsHierarchy;
                    break;
            }

            if (pvar != null)
                return VSConstants.S_OK;

            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        #endregion

        #region IVsHierarchyDeleteHandler Members

        public int DeleteItem(uint dwDelItemOp, uint itemid) {
            var item = GetItem(itemid);
            if (item != null) {
                switch ((__VSDELETEITEMOPERATION)dwDelItemOp) {
                    case __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage:
                        var session = _sessions[(int)itemid];
                        File.Delete(item.Filename);
                        goto case __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
                    case __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject:
                        item.Removed();
                        _sessions.RemoveAt((int)itemid);
                        OnItemDeleted(itemid);
                        // our itemids have all changed, invalidate them
                        OnInvalidateItems(VSConstants.VSITEMID_ROOT);
                        break;
                }

                if (itemid == _activeSession) {
                    if (_sessions.Count > 0) {
                        SetActiveSession(_sessions[0]);
                    } else {
                        _activeSession = -1;
                    }
                }
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int QueryDeleteItem(uint dwDelItemOp, uint itemid, out int pfCanDelete) {
            pfCanDelete = 1;
            return VSConstants.S_OK;
        }

        #endregion

        private static ImageList InitImageList() {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.PythonTools.ProfilingTreeView.bmp");
            return Utilities.GetImageList(
                stream
            );
        }

        private SessionNode GetItem(uint itemid) {
            if (itemid < _sessions.Count) {
                return _sessions[(int)itemid];
            }
            return null;
        }


        internal void StartProfiling() {
            if (_activeSession != -1) {
                _sessions[_activeSession].StartProfiling();
            }
        }

        public List<SessionNode> Sessions {
            get {
                return _sessions;
            }
        }
    }
}
