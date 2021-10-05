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

using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities.SharedProject
{
	/// <summary>
	/// Generates a folder and if not excluded adds it to the generated project.
	/// </summary>
	public sealed class FolderItem : ProjectContentGenerator
	{
		public readonly string Name;
		public readonly bool IsExcluded, IsMissing;

		/// <summary>
		/// Creates a new folder with the specified name.  If the folder
		/// is excluded then it will be created on disk but not added to the
		/// project.
		/// </summary>
		public FolderItem(string name, bool isExcluded = false, bool isMissing = false)
		{
			Name = name;
			IsExcluded = isExcluded;
			IsMissing = isMissing;
		}

		public override void Generate(ProjectType projectType, MSBuild.Project project)
		{
			if (!IsMissing)
			{
				Directory.CreateDirectory(Path.Combine(project.DirectoryPath, Name));
			}

			if (!IsExcluded)
			{
				project.AddItem("Folder", Name);
			}
		}
	}
}
