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
	public class TargetDefinition : ProjectContentGenerator
	{
		public readonly string Name;
		public readonly Action<ProjectTargetElement>[] Creators;

		public TargetDefinition(string name, params Action<ProjectTargetElement>[] creators)
		{
			Name = name;
			Creators = creators;
		}

		public string DependsOnTargets { get; set; }

		public override void Generate(ProjectType projectType, MSBuild.Project project)
		{
			var target = project.Xml.AddTarget(Name);
			if (!string.IsNullOrEmpty(DependsOnTargets))
			{
				target.DependsOnTargets = DependsOnTargets;
			}
			foreach (Action<ProjectTargetElement> creator in Creators)
			{
				creator(target);
			}
		}
	}
}
