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

namespace CookiecutterTests
{
	[TestClass]
	public class FeedTemplateSourceTests
	{
		private static string NonExistentFeedPath => "http://www.microsoft.com/invalidfeed.txt";
		private static string OnlineFeedPath => UrlConstants.DefaultRecommendedFeed;
		private static string LocalFeedPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "feed.txt");

		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestMethod]
		public async Task LoadOnlineFeed()
		{
			ITemplateSource source = new FeedTemplateSource(new Uri(OnlineFeedPath));

			var result = await source.GetTemplatesAsync(null, null, CancellationToken.None);

			Assert.IsNull(result.ContinuationToken);
			Assert.IsTrue(result.Templates.Count > 0);
			Assert.IsFalse(string.IsNullOrEmpty(result.Templates[0].Name));
			Assert.IsFalse(string.IsNullOrEmpty(result.Templates[0].RemoteUrl));
		}

		[TestMethod]
		public async Task LoadLocalFeed()
		{
			ITemplateSource source = new FeedTemplateSource(new Uri(LocalFeedPath));
			Assert.IsTrue(File.Exists(LocalFeedPath));

			var result = await source.GetTemplatesAsync(null, null, CancellationToken.None);

			Assert.IsNull(result.ContinuationToken);
			Assert.AreEqual(6, result.Templates.Count);

			Template[] expected = new Template[] {
				new Template() {
					RemoteUrl = "https://github.com/brettcannon/python-azure-web-app-cookiecutter",
					Name = "brettcannon/python-azure-web-app-cookiecutter",
				},
				new Template() {
					RemoteUrl = "https://github.com/pydanny/cookiecutter-django",
					Name = "pydanny/cookiecutter-django",
				},
				new Template() {
					RemoteUrl = "https://github.com/sloria/cookiecutter-flask",
					Name = "sloria/cookiecutter-flask",
				},
				new Template() {
					RemoteUrl = "https://github.com/pydanny/cookiecutter-djangopackage",
					Name = "pydanny/cookiecutter-djangopackage",
				},
				new Template() {
					RemoteUrl = "https://github.com/marcofucci/cookiecutter-simple-django",
					Name = "marcofucci/cookiecutter-simple-django",
				},
				new Template() {
					RemoteUrl = "https://github.com/agconti/cookiecutter-django-rest",
					Name = "agconti/cookiecutter-django-rest",
				},
			};

			CollectionAssert.AreEqual(expected, result.Templates.ToArray(), new TemplateComparer());
		}

		[TestMethod]
		public async Task LoadNonExistentFeed()
		{
			ITemplateSource source = new FeedTemplateSource(new Uri(NonExistentFeedPath));

			try
			{
				var result = await source.GetTemplatesAsync(null, null, CancellationToken.None);
				Assert.Fail("Expected an TemplateEnumerationException when loading invalid feed.");
			}
			catch (TemplateEnumerationException)
			{
			}
		}

		[TestMethod]
		public async Task SearchFeedSingleTerm()
		{
			ITemplateSource source = new FeedTemplateSource(new Uri(LocalFeedPath));

			var result = await source.GetTemplatesAsync("azure", null, CancellationToken.None);
			Assert.IsNull(result.ContinuationToken);
			Assert.AreEqual(1, result.Templates.Count);
			Assert.AreEqual("brettcannon/python-azure-web-app-cookiecutter", result.Templates[0].Name);
		}

		[TestMethod]
		public async Task SearchFeedMultipleTerms()
		{
			ITemplateSource source = new FeedTemplateSource(new Uri(LocalFeedPath));

			var result = await source.GetTemplatesAsync("flask,azure", null, CancellationToken.None);
			Assert.IsNull(result.ContinuationToken);
			Assert.AreEqual(2, result.Templates.Count);
			Assert.IsNotNull(result.Templates.SingleOrDefault(t => t.Name == "brettcannon/python-azure-web-app-cookiecutter"));
			Assert.IsNotNull(result.Templates.SingleOrDefault(t => t.Name == "sloria/cookiecutter-flask"));
		}

		private class TemplateComparer : IComparer
		{
			public int Compare(object x, object y)
			{
				if (x == y)
				{
					return 0;
				}

				var a = x as Template;
				var b = y as Template;

				if (a == null)
				{
					return -1;
				}

				if (b == null)
				{
					return -1;
				}

				int res;
				res = a.Name.CompareTo(b.Name);
				if (res != 0)
				{
					return res;
				}

				res = a.Description.CompareTo(b.Description);
				if (res != 0)
				{
					return res;
				}

				res = a.RemoteUrl.CompareTo(b.RemoteUrl);
				if (res != 0)
				{
					return res;
				}

				res = a.LocalFolderPath.CompareTo(b.LocalFolderPath);
				if (res != 0)
				{
					return res;
				}

				return 0;
			}
		}
	}
}
