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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	public sealed class MockVsSolution : IVsSolution, IVsRegisterProjectTypes, IVsCreateAggregateProject, IVsHierarchy
	{
		private string _solutionFile;
		private readonly Dictionary<Guid, ProjectInfo> _projects = new Dictionary<Guid, ProjectInfo>();
		private readonly Dictionary<string, ProjectInfo> _projectByName = new Dictionary<string, ProjectInfo>();
		private readonly Dictionary<Guid, IVsProjectFactory> _projectFactories = new Dictionary<Guid, IVsProjectFactory>();
		private static Regex _projectRegex = new Regex(@"Project\(""({[0-9A-Fa-f\-]+})""\)\s*=\s*""([\w\d\s]+)"",\s*""([\.\\\w\d\s]+)"",\s*""({[0-9A-Fa-f\-]+})""");

		private class ProjectInfo
		{
			public readonly Guid ProjectGuid;
			public readonly Guid ProjectType;
			public readonly IVsHierarchy Hierarchy;
			public readonly string Name, Filename;

			public ProjectInfo(Guid projectGuid, Guid typeGuid, IVsHierarchy hierarchy, string name, string filename)
			{
				ProjectGuid = projectGuid;
				ProjectType = typeGuid;
				Hierarchy = hierarchy;
				Name = name;
				Filename = filename;
			}
		}

		public string SolutionFile => _solutionFile;

		public int AddVirtualProject(IVsHierarchy pHierarchy, uint grfAddVPFlags)
		{
			throw new NotImplementedException();
		}

		public int AddVirtualProjectEx(IVsHierarchy pHierarchy, uint grfAddVPFlags, ref Guid rguidProjectID)
		{
			throw new NotImplementedException();
		}

		public int AdviseSolutionEvents(IVsSolutionEvents pSink, out uint pdwCookie)
		{
			throw new NotImplementedException();
		}

		public int CanCreateNewProjectAtLocation(int fCreateNewSolution, string pszFullProjectFilePath, out int pfCanCreate)
		{
			throw new NotImplementedException();
		}

		public int CloseSolutionElement(uint grfCloseOpts, IVsHierarchy pHier, uint docCookie)
		{
			throw new NotImplementedException();
		}

		public int CreateNewProjectViaDlg(string pszExpand, string pszSelect, uint dwReserved)
		{
			throw new NotImplementedException();
		}

		public int CreateProject(ref Guid rguidProjectType, string lpszMoniker, string lpszLocation, string lpszName, uint grfCreateFlags, ref Guid iidProject, out IntPtr ppProject)
		{
			throw new NotImplementedException();
		}

		public int CreateSolution(string lpszLocation, string lpszName, uint grfCreateFlags)
		{
			throw new NotImplementedException();
		}

		public int GenerateNextDefaultProjectName(string pszBaseName, string pszLocation, out string pbstrProjectName)
		{
			throw new NotImplementedException();
		}

		public int GenerateUniqueProjectName(string lpszRoot, out string pbstrProjectName)
		{
			throw new NotImplementedException();
		}

		public int GetGuidOfProject(IVsHierarchy pHierarchy, out Guid pguidProjectID)
		{
			throw new NotImplementedException();
		}

		public int GetItemInfoOfProjref(string pszProjref, int propid, out object pvar)
		{
			throw new NotImplementedException();
		}

		public int GetItemOfProjref(string pszProjref, out IVsHierarchy ppHierarchy, out uint pitemid, out string pbstrUpdatedProjref, VSUPDATEPROJREFREASON[] puprUpdateReason)
		{
			var comps = pszProjref.Split('|');
			if (comps.Length == 3)
			{
				foreach (var project in _projects.Values)
				{
					if (project.Filename == comps[1])
					{
						ppHierarchy = project.Hierarchy;
						if (ErrorHandler.Succeeded(project.Hierarchy.ParseCanonicalName(comps[2], out pitemid)))
						{
							pbstrUpdatedProjref = pszProjref;
							puprUpdateReason[0] = VSUPDATEPROJREFREASON.UPR_NoUpdate;
							return VSConstants.S_OK;
						}
					}
				}
			}
			ppHierarchy = null;
			pitemid = 0;
			pbstrUpdatedProjref = null;
			return VSConstants.E_FAIL;
		}

		public int GetProjectEnum(uint grfEnumFlags, ref Guid rguidEnumOnlyThisType, out IEnumHierarchies ppenum)
		{
			__VSENUMPROJFLAGS flags = (__VSENUMPROJFLAGS)grfEnumFlags;

			ProjectInfo[] projects;
			if (flags.HasFlag(__VSENUMPROJFLAGS.EPF_MATCHTYPE))
			{
				var guid = rguidEnumOnlyThisType;
				projects = _projects.Values.Where(x => x.ProjectGuid == guid).ToArray();
			}
			else if (flags.HasFlag(__VSENUMPROJFLAGS.EPF_ALLPROJECTS))
			{
				projects = _projects.Values.ToArray();
			}
			else
			{
				throw new NotImplementedException();
			}

			ppenum = new ProjectEnum(projects);
			return VSConstants.S_OK;
		}

		private class ProjectEnum : IEnumHierarchies
		{
			private readonly ProjectInfo[] _projects;
			private int _index;

			public ProjectEnum(ProjectInfo[] projects)
			{
				_projects = projects;
			}

			public int Clone(out IEnumHierarchies ppenum)
			{
				ppenum = new ProjectEnum(_projects);
				return VSConstants.S_OK;
			}

			public int Next(uint celt, IVsHierarchy[] rgelt, out uint pceltFetched)
			{
				pceltFetched = 0;
				for (int i = 0; i < celt && _index < _projects.Length; i++)
				{
					rgelt[i] = _projects[_index++].Hierarchy;
					pceltFetched++;
				}
				return VSConstants.S_OK;
			}

			public int Reset()
			{
				_index = 0;
				return VSConstants.S_OK;
			}

			public int Skip(uint celt)
			{
				_index += (int)celt;
				return VSConstants.S_OK;
			}
		}

		public int GetProjectFactory(uint dwReserved, Guid[] pguidProjectType, string pszMkProject, out IVsProjectFactory ppProjectFactory)
		{
			throw new NotImplementedException();
		}

		public int GetProjectFilesInSolution(uint grfGetOpts, uint cProjects, string[] rgbstrProjectNames, out uint pcProjectsFetched)
		{
			throw new NotImplementedException();
		}

		public int GetProjectInfoOfProjref(string pszProjref, int propid, out object pvar)
		{
			throw new NotImplementedException();
		}

		public int GetProjectOfGuid(ref Guid rguidProjectID, out IVsHierarchy ppHierarchy)
		{
			if (_projects.TryGetValue(rguidProjectID, out ProjectInfo proj))
			{
				ppHierarchy = proj.Hierarchy;
				return VSConstants.S_OK;
			}
			ppHierarchy = null;
			return VSConstants.E_FAIL;
		}

		public int GetProjectOfProjref(string pszProjref, out IVsHierarchy ppHierarchy, out string pbstrUpdatedProjref, VSUPDATEPROJREFREASON[] puprUpdateReason)
		{
			throw new NotImplementedException();
		}

		public int GetProjectOfUniqueName(string pszUniqueName, out IVsHierarchy ppHierarchy)
		{
			if (_projectByName.TryGetValue(pszUniqueName, out ProjectInfo proj))
			{
				ppHierarchy = proj.Hierarchy;
				return VSConstants.S_OK;
			}
			ppHierarchy = null;
			return VSConstants.E_FAIL;
		}

		public int GetProjectTypeGuid(uint dwReserved, string pszMkProject, out Guid pguidProjectType)
		{
			throw new NotImplementedException();
		}

		public int GetProjrefOfItem(IVsHierarchy pHierarchy, uint itemid, out string pbstrProjref)
		{
			foreach (var project in _projects)
			{
				if (ComUtilities.IsSameComObject(pHierarchy, project.Value.Hierarchy))
				{
					ErrorHandler.ThrowOnFailure(project.Value.Hierarchy.GetCanonicalName(itemid, out global::System.String canonicalName));
					pbstrProjref = project.Value.ProjectGuid.ToString("B") + "|" + project.Value.Filename + "|" + canonicalName;
					return VSConstants.S_OK;
				}
			}
			pbstrProjref = null;
			return VSConstants.E_FAIL;
		}

		public int GetProjrefOfProject(IVsHierarchy pHierarchy, out string pbstrProjref)
		{
			throw new NotImplementedException();
		}

		public int GetProperty(int propid, out object pvar)
		{
			throw new NotImplementedException();
		}

		public int GetSolutionInfo(out string pbstrSolutionDirectory, out string pbstrSolutionFile, out string pbstrUserOptsFile)
		{
			pbstrSolutionDirectory = Path.GetDirectoryName(_solutionFile);
			pbstrSolutionFile = _solutionFile;
			pbstrUserOptsFile = "";
			return VSConstants.S_OK;
		}

		public int GetUniqueNameOfProject(IVsHierarchy pHierarchy, out string pbstrUniqueName)
		{
			throw new NotImplementedException();
		}

		public int GetVirtualProjectFlags(IVsHierarchy pHierarchy, out uint pgrfAddVPFlags)
		{
			throw new NotImplementedException();
		}

		public int OnAfterRenameProject(IVsProject pProject, string pszMkOldName, string pszMkNewName, uint dwReserved)
		{
			throw new NotImplementedException();
		}

		public int OpenSolutionFile(uint grfOpenOpts, string pszFilename)
		{
			_solutionFile = pszFilename;
			_projects.Clear();
			_projectByName.Clear();

			var text = File.ReadAllText(pszFilename);
			foreach (Match match in _projectRegex.Matches(text))
			{
				var typeGuid = Guid.Parse(match.Groups[1].Value);
				var projectName = match.Groups[2].Value;
				var filename = match.Groups[3].Value;
				var projectGuid = Guid.Parse(match.Groups[4].Value);

				if (!_projectFactories.TryGetValue(typeGuid, out IVsProjectFactory factory))
				{
					Console.WriteLine("Unregistered project type: {0} for project {1}", typeGuid, filename);
					return VSConstants.E_FAIL;
				}

				IntPtr project = IntPtr.Zero;
				try
				{
					int hr = factory.CreateProject(
						Path.Combine(Path.GetDirectoryName(pszFilename), filename),
						"",
						"",
						(uint)__VSCREATEPROJFLAGS.CPF_OPENFILE,
						ref projectGuid,
						out project,
						out global::System.Int32 cancelled
					);

					if (ErrorHandler.Failed(hr))
					{
						return hr;
					}

					var vsProj = (IVsProject)Marshal.GetObjectForIUnknown(project);

					ProjectInfo projectInfo = new ProjectInfo(
						projectGuid,
						typeGuid,
						(IVsHierarchy)vsProj,
						projectName,
						filename
					);
					ErrorHandler.ThrowOnFailure(projectInfo.Hierarchy.SetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ParentHierarchy, this));
					_projects[projectGuid] = projectInfo;
					_projectByName[projectName] = projectInfo;
				}
				finally
				{
					if (project != IntPtr.Zero)
					{
						Marshal.Release(project);
					}
				}

			}
			return VSConstants.S_OK;
		}

		public int OpenSolutionViaDlg(string pszStartDirectory, int fDefaultToAllProjectsFilter)
		{
			throw new NotImplementedException();
		}

		public int QueryEditSolutionFile(out uint pdwEditResult)
		{
			throw new NotImplementedException();
		}

		public int QueryRenameProject(IVsProject pProject, string pszMkOldName, string pszMkNewName, uint dwReserved, out int pfRenameCanContinue)
		{
			throw new NotImplementedException();
		}

		public int RemoveVirtualProject(IVsHierarchy pHierarchy, uint grfRemoveVPFlags)
		{
			throw new NotImplementedException();
		}

		public int SaveSolutionElement(uint grfSaveOpts, IVsHierarchy pHier, uint docCookie)
		{
			throw new NotImplementedException();
		}

		public int SetProperty(int propid, object var)
		{
			throw new NotImplementedException();
		}

		public int UnadviseSolutionEvents(uint dwCookie)
		{
			throw new NotImplementedException();
		}

		#region IVsRegisterProjectTypes

		public int RegisterProjectType(ref Guid rguidProjType, IVsProjectFactory pVsPF, out uint pdwCookie)
		{
			_projectFactories[rguidProjType] = pVsPF;
			pdwCookie = 1;
			return VSConstants.S_OK;
		}

		public int UnregisterProjectType(uint dwCookie)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IVsCreateAggregateProject

		public int CreateAggregateProject(string pszProjectTypeGuids, string pszFilename, string pszLocation, string pszName, uint grfCreateFlags, ref Guid iidProject, out IntPtr ppvProject)
		{
			ppvProject = IntPtr.Zero;

			Guid[] guids = pszProjectTypeGuids.Split(';').Select(x => Guid.Parse(x)).ToArray();
			IntPtr outer = IntPtr.Zero;

			for (int i = 0; i < guids.Length; i++)
			{
				if (!_projectFactories.TryGetValue(guids[0], out IVsProjectFactory factory))
				{
					Console.WriteLine("Unknown project factory during aggregate creation: {0}", guids[0]);
					return VSConstants.E_FAIL;
				}

				var agg = (IVsAggregatableProjectFactoryCorrected)factory;
				IntPtr punk = IntPtr.Zero;
				try
				{
					int hr = agg.PreCreateForOuter(outer, out punk);
					if (ErrorHandler.Failed(hr))
					{
						return hr;
					}

					Debug.Assert(guids.Length == 1, "We don't initialize for outer in the right order...");
					outer = punk;
					var guid = typeof(IVsAggregatableProject).GUID;
					hr = Marshal.QueryInterface(punk, ref guid, out IntPtr obj);
					var aggProj = (IVsAggregatableProject)Marshal.GetObjectForIUnknown(obj);
					var iunknown = VSConstants.IID_IUnknown;
					hr = aggProj.InitializeForOuter(pszFilename, pszLocation, pszName, grfCreateFlags, ref iunknown, out IntPtr project, out global::System.Int32 cancelled);
					if (ErrorHandler.Failed(hr))
					{
						return hr;
					}
					ppvProject = project;
				}
				finally
				{
					if (punk != IntPtr.Zero)
					{
						Marshal.Release(punk);
					}
				}
			}
			return VSConstants.S_OK;
		}

		#endregion

		public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie)
		{
			throw new NotImplementedException();
		}

		public int Close()
		{
			throw new NotImplementedException();
		}

		public int GetCanonicalName(uint itemid, out string pbstrName)
		{
			throw new NotImplementedException();
		}

		public int GetGuidProperty(uint itemid, int propid, out Guid pguid)
		{
			throw new NotImplementedException();
		}

		public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested)
		{
			throw new NotImplementedException();
		}

		public int GetProperty(uint itemid, int propid, out object pvar)
		{
			throw new NotImplementedException();
		}

		public int GetSite(out VisualStudio.OLE.Interop.IServiceProvider ppSP)
		{
			throw new NotImplementedException();
		}

		public int ParseCanonicalName(string pszName, out uint pitemid)
		{
			throw new NotImplementedException();
		}

		public int QueryClose(out int pfCanClose)
		{
			throw new NotImplementedException();
		}

		public int SetGuidProperty(uint itemid, int propid, ref Guid rguid)
		{
			throw new NotImplementedException();
		}

		public int SetProperty(uint itemid, int propid, object var)
		{
			throw new NotImplementedException();
		}

		public int SetSite(VisualStudio.OLE.Interop.IServiceProvider psp)
		{
			throw new NotImplementedException();
		}

		public int UnadviseHierarchyEvents(uint dwCookie)
		{
			throw new NotImplementedException();
		}

		public int Unused0()
		{
			throw new NotImplementedException();
		}

		public int Unused1()
		{
			throw new NotImplementedException();
		}

		public int Unused2()
		{
			throw new NotImplementedException();
		}

		public int Unused3()
		{
			throw new NotImplementedException();
		}

		public int Unused4()
		{
			throw new NotImplementedException();
		}
	}
}
