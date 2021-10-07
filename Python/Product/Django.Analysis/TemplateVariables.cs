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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Django.Analysis
{
	internal class TemplateVariables
	{
		private readonly Dictionary<string, Dictionary<IPythonProjectEntry, ValuesAndVersion>> _values = new Dictionary<string, Dictionary<IPythonProjectEntry, ValuesAndVersion>>();

		public void UpdateVariable(string name, AnalysisUnit unit, IEnumerable<AnalysisValue> values)
		{
			if (!_values.TryGetValue(name, out Dictionary<IPythonProjectEntry, ValuesAndVersion> entryMappedValues))
			{
				_values[name] = entryMappedValues = new Dictionary<IPythonProjectEntry, ValuesAndVersion>();
			}

			foreach (var value in values)
			{
				var module = value.DeclaringModule ?? unit.ProjectEntry;
				if (!entryMappedValues.TryGetValue(module, out ValuesAndVersion valsAndVersion) || valsAndVersion.DeclaringVersion != module.AnalysisVersion)
				{
					entryMappedValues[module] = valsAndVersion = new ValuesAndVersion(module.AnalysisVersion);
				}

				valsAndVersion.Values.Add(value);
			}
		}

		private struct ValuesAndVersion
		{
			public readonly int DeclaringVersion;
			public readonly HashSet<AnalysisValue> Values;

			public ValuesAndVersion(int declaringVersion)
			{
				DeclaringVersion = declaringVersion;
				Values = new HashSet<AnalysisValue>();
			}
		}

		internal Dictionary<string, HashSet<AnalysisValue>> GetAllValues()
		{
			var res = new Dictionary<string, HashSet<AnalysisValue>>();

			foreach (var nameAndValues in _values)
			{
				HashSet<AnalysisValue> curValues = new HashSet<AnalysisValue>();
				res[nameAndValues.Key] = curValues;

				foreach (var projectAndValues in nameAndValues.Value)
				{
					foreach (var analysisValue in projectAndValues.Value.Values)
					{
						if (analysisValue.DeclaringModule == null ||
							analysisValue.DeclaringModule.AnalysisVersion == projectAndValues.Value.DeclaringVersion)
						{
							curValues.Add(analysisValue);
						}
					}
				}
			}
			return res;
		}
	}

}
