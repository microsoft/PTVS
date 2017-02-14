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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides the command for starting a file or the start item of a project in the REPL window.
    /// </summary>
    internal sealed class SurveyNewsService {
        private readonly PythonToolsService _pyService;
        private string _surveyNewsUrl;
        private object _surveyNewsUrlLock = new object();

        public SurveyNewsService(PythonToolsService pyService) {
            _pyService = pyService;
        }

        private void BrowseSurveyNewsOnIdle(object sender, ComponentManagerEventArgs e) {
            _pyService.OnIdle -= BrowseSurveyNewsOnIdle;

            lock (_surveyNewsUrlLock) {
                if (!string.IsNullOrEmpty(_surveyNewsUrl)) {
                    PythonToolsPackage.OpenVsWebBrowser(_pyService.Site, _surveyNewsUrl);
                    _surveyNewsUrl = null;
                }
            }
        }

        internal void BrowseSurveyNews(string url) {
            lock (_surveyNewsUrlLock) {
                _surveyNewsUrl = url;
            }

            _pyService.OnIdle += BrowseSurveyNewsOnIdle;
        }

        private void CheckSurveyNewsThread(Uri url, bool warnIfNoneAvailable) {
            // We can't use a simple WebRequest, because that doesn't have access
            // to the browser's session cookies.  Cookies are used to remember
            // which survey/news item the user has submitted/accepted.  The server 
            // checks the cookies and returns the survey/news urls that are 
            // currently available (availability is determined via the survey/news 
            // item start and end date).
            var th = new Thread(() => {
                var br = new WebBrowser();
                br.Tag = warnIfNoneAvailable;
                br.DocumentCompleted += (sender, args) => OnSurveyNewsDocumentCompleted(sender, args);
                br.Navigate(url);
                Application.Run();
            });
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        private void OnSurveyNewsDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
            var br = (WebBrowser)sender;
            var warnIfNoneAvailable = (bool)br.Tag;
            if (br.Url == e.Url) {
                List<string> available = null;

                string json = br.DocumentText;
                if (!string.IsNullOrEmpty(json)) {
                    int startIndex = json.IndexOf("<PRE>");
                    if (startIndex > 0) {
                        int endIndex = json.IndexOf("</PRE>", startIndex);
                        if (endIndex > 0) {
                            json = json.Substring(startIndex + 5, endIndex - startIndex - 5);

                            try {
                                // Example JSON data returned by the server:
                                //{
                                // "cannotvoteagain": [], 
                                // "notvoted": [
                                //  "http://ptvs.azurewebsites.net/news/141", 
                                //  "http://ptvs.azurewebsites.net/news/41", 
                                // ], 
                                // "canvoteagain": [
                                //  "http://ptvs.azurewebsites.net/news/51"
                                // ]
                                //}

                                // Description of each list:
                                // voted: cookie found
                                // notvoted: cookie not found
                                // canvoteagain: cookie found, but multiple votes are allowed
                                JavaScriptSerializer serializer = new JavaScriptSerializer();
                                var results = serializer.Deserialize<Dictionary<string, List<string>>>(json);
                                available = results["notvoted"];
                            } catch (ArgumentException) {
                            } catch (InvalidOperationException) {
                            }
                        }
                    }
                }

                if (available != null && available.Count > 0) {
                    BrowseSurveyNews(available[0]);
                } else if (warnIfNoneAvailable) {
                    if (available != null) {
                        BrowseSurveyNews(_pyService.GeneralOptions.SurveyNewsIndexUrl);
                    } else {
                        BrowseSurveyNews(PythonToolsInstallPath.GetFile("NoSurveyNewsFeed.html"));
                    }
                }

                Application.ExitThread();
            }
        }

        internal void CheckSurveyNews(bool forceCheckAndWarnIfNoneAvailable) {
            _pyService.Site.MustBeCalledFromUIThread();

            bool shouldQueryServer = false;
            var options = _pyService.GeneralOptions;

            if (forceCheckAndWarnIfNoneAvailable) {
                shouldQueryServer = true;
            } else {
                // Ensure that we don't prompt the user on their very first project creation.
                // Delay by 3 days by pretending we checked 4 days ago (the default of check
                // once a week ensures we'll check again in 3 days).
                if (options.SurveyNewsLastCheck == DateTime.MinValue) {
                    options.SurveyNewsLastCheck = DateTime.Now - TimeSpan.FromDays(4);
                    options.Save();
                }

                var elapsedTime = DateTime.Now - options.SurveyNewsLastCheck;
                switch (options.SurveyNewsCheck) {
                    case SurveyNewsPolicy.Disabled:
                        break;
                    case SurveyNewsPolicy.CheckOnceDay:
                        shouldQueryServer = elapsedTime.TotalDays >= 1;
                        break;
                    case SurveyNewsPolicy.CheckOnceWeek:
                        shouldQueryServer = elapsedTime.TotalDays >= 7;
                        break;
                    case SurveyNewsPolicy.CheckOnceMonth:
                        shouldQueryServer = elapsedTime.TotalDays >= 30;
                        break;
                    default:
                        Debug.Assert(false, "Unexpected SurveyNewsPolicy: {0}.".FormatInvariant(options.SurveyNewsCheck));
                        break;
                }
            }

            if (shouldQueryServer) {
                options.SurveyNewsLastCheck = DateTime.Now;
                options.Save();
                CheckSurveyNewsThread(new Uri(options.SurveyNewsFeedUrl), forceCheckAndWarnIfNoneAvailable);
            }
        }
    }
}
