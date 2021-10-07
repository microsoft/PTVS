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

using Microsoft.VisualStudioTools;

namespace TestUtilities
{
	public static class TestData
	{
		private static string GetRootDir()
		{
			var dir = CommonUtils.GetParent((typeof(TestData)).Assembly.Location);
			while (!string.IsNullOrEmpty(dir) &&
				Directory.Exists(dir) &&
				!File.Exists(CommonUtils.GetAbsoluteFilePath(dir, "build.root")))
			{
				dir = CommonUtils.GetParent(dir);
			}
			return dir ?? "";
		}

		public static void ProvideContext(TestContext context)
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_TESTDATA_TEMP_PATH")))
			{
				Environment.SetEnvironmentVariable("_TESTDATA_TEMP_PATH", context.DeploymentDirectory);
			}
		}

		/// <summary>
		/// Returns the full path to the test data root.
		/// </summary>
		private static string CalculateTestDataRoot()
		{
			var path = Environment.GetEnvironmentVariable("_TESTDATA_ROOT_PATH");
			if (Directory.Exists(path))
			{
				return path;
			}

			path = GetRootDir();
			if (Directory.Exists(path))
			{
				foreach (global::System.String landmark in new[] {
					"TestData",
					@"Python\Tests\TestData"
				})
				{
					var candidate = CommonUtils.GetAbsoluteDirectoryPath(path, landmark);
					if (Directory.Exists(candidate))
					{
						return CommonUtils.GetParent(candidate);
					}
				}
			}

			throw new InvalidOperationException("Failed to find test data");
		}

		private static readonly Lazy<string> _root = new Lazy<string>(CalculateTestDataRoot);
		public static string Root => _root.Value;

		/// <summary>
		/// Returns the full path to the deployed file.
		/// </summary>
		public static string GetPath(params string[] paths)
		{
			var res = Root;
			foreach (global::System.String p in paths)
			{
				res = CommonUtils.GetAbsoluteFilePath(res, p);
			}
			return res;
		}

		private static string CalculateTempRoot()
		{
			var path = Environment.GetEnvironmentVariable("_TESTDATA_TEMP_PATH");

			if (string.IsNullOrEmpty(path))
			{
				path = Path.GetTempPath();
				var subpath = Path.Combine(path, Path.GetRandomFileName());
				while (Directory.Exists(subpath) || File.Exists(subpath))
				{
					subpath = Path.Combine(path, Path.GetRandomFileName());
				}
				path = subpath;
			}
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
			return path;
		}

		private static readonly Lazy<string> _tempRoot = new Lazy<string>(CalculateTempRoot);

		/// <summary>
		/// Returns the full path to a temporary directory. This is within the
		/// deployment to ensure that test files are easily cleaned up.
		/// </summary>
		/// <param name="subPath">
		/// Name of the subdirectory within the temporary directory. If omitted,
		/// a randomly generated name will be used.
		/// </param>
		public static string GetTempPath(string subPath = null)
		{
			var path = _tempRoot.Value;
			if (string.IsNullOrEmpty(subPath))
			{
				subPath = Path.GetRandomFileName();
				while (Directory.Exists(Path.Combine(path, subPath)))
				{
					subPath = Path.GetRandomFileName();
				}
			}
			path = CommonUtils.GetAbsoluteDirectoryPath(path, subPath);
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
			Console.WriteLine($"Creating temp directory for test at {path}");
			return path;
		}
	}
}

