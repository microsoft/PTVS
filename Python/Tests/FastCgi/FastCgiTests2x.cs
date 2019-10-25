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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Python;

namespace FastCgiTests {
    [TestClass]
    public class FastCgiTests2x {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestInitialize]
        public void CloseRunningIisExpress() {
            PythonVersion.AssertInstalled();

            IEnumerable<Process> running;
            while ((running = Process.GetProcessesByName("iisexpress")).Any()) {
                foreach (var p in running) {
                    try {
                        p.CloseMainWindow();
                    } catch (Exception ex) {
                        Console.WriteLine("Failed to CloseMainWindow on iisexpress: {0}", ex);
                    }
                }

                Thread.Sleep(1000);

                foreach (var p in running) {
                    if (!p.HasExited) {
                        try {
                            p.Kill();
                        } catch (Exception ex) {
                            Console.WriteLine("Failed to Kill iisexpress: {0}", ex);
                        }
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        [TestCategory("10s")]
        public void DjangoHelloWorld() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                site.StartServer();

                var response = site.Request("");
                Console.WriteLine(response.ContentType);
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Console.WriteLine(content);

                Assert.AreEqual(content, "<html><body>Hello World!</body></html>");
            }
        }
        /*
         * Currently disabled, we need to unify this w/ where web.config lives in Azure first 
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void ConfigVariables() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                File.Copy("TestData\\DjangoTestApp\\web.config", Path.Combine(site.SiteDir, "web.config"));

                site.StartServer();

                CopyDir("TestData", site.SiteDir);

                var response = site.Request("config");
                Console.WriteLine(response.ContentType);
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Console.WriteLine(content);

                Assert.AreEqual(content, "<html><body>Hello There!</body></html>");
            }
        }*/

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void LargeResponse() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                site.StartServer();

                var response = site.Request("large_response");
                Console.WriteLine(response.ContentType);
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();

                string expected = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghjklmnopqrstuvwxyz0123456789";
                Assert.AreEqual(expected.Length * 3000, content.Length, "Got: " + content);
                for (int i = 0; i < content.Length / expected.Length; i++) {
                    for (int j = 0; j < expected.Length; j++) {
                        Assert.AreEqual(
                            expected[j],
                            content[i * expected.Length + j]
                        );
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        [TestCategory("10s")]
        public void DjangoHelloWorldParallel() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                site.StartServer();

                const int threadCnt = 12;
                const int requests = 1000;
                var tasks = new Task[threadCnt];
                int count = 0;

                for (int i = 0; i < tasks.Length; i++) {
                    tasks[i] = Task.Run(() => {
                        for (int j = 0; j < requests; j++) {
                            var response = site.Request("");
                            var stream = response.GetResponseStream();
                            var content = new StreamReader(stream).ReadToEnd();
                            Assert.AreEqual(content, "<html><body>Hello World!</body></html>");
                            Interlocked.Increment(ref count);
                        }
                    });
                }

                for (int i = 0; i < tasks.Length; i++) {
                    tasks[i].Wait();
                }
                Assert.AreEqual(count, threadCnt * requests);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CustomHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.handler")) {
                site.StartServer();

                var response = site.Request("");
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Assert.AreEqual("<html><body>hello world</body></html>", content);

                Assert.AreEqual("42", response.Headers["Custom-Header"]);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void CustomCallableHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.callable_handler()")) {
                site.StartServer();

                var response = site.Request("");
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Assert.AreEqual("<html><body>hello world</body></html>", content);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void ErrorHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.error_handler")) {
                site.StartServer();
                try {
                    var response = site.Request("", printError: false);
                } catch (WebException wex) {
                    var stream = wex.Response.GetResponseStream();
                    var content = new StreamReader(stream).ReadToEnd();

                    Assert.AreEqual(HttpStatusCode.NotFound, ((HttpWebResponse)wex.Response).StatusCode);

                    Assert.AreEqual("<html><body>Sorry folks, we're closed for two weeks to clean and repair America's favorite family fun park</body></html>", content);

                    Assert.AreEqual(WebExceptionStatus.ProtocolError, wex.Status);
                }
            }
        }

        private static string CreateSite() {
            string dirName = TestData.GetTempPath();

            File.Copy(TestData.GetPath("TestData", "applicationhostOriginal.config"),
                Path.Combine(dirName, "applicationHost.config"));

            File.Copy(
                WFastCgiPath,
                Path.Combine(dirName, "wfastcgi.py")
            );

            Directory.CreateDirectory(Path.Combine(dirName, "WebSite"));
            return dirName;
        }

        public static void ConfigureIIS(string appCmd, string appHostConfig, string python, string wfastcgi, Dictionary<string, string> envVars) {
            using (var p = ProcessOutput.RunHiddenAndCapture(
                appCmd, "set", "config", "/section:system.webServer/fastCGI",
                string.Format("/+[fullPath='{0}', arguments='\"{1}\"']", python, wfastcgi),
                "/AppHostConfig:" + appHostConfig
            )) {
                p.Wait();
                DumpOutput(p);
                Assert.AreEqual(0, p.ExitCode);
            }

            using (var p = ProcessOutput.RunHiddenAndCapture(
                appCmd, "set", "config", "/section:system.webServer/handlers",
                string.Format(
                    "/+[name='Python_via_FastCGI',path='*',verb='*',modules='FastCgiModule',scriptProcessor='{0}|\"{1}\"',resourceType='Unspecified']",
                    python, wfastcgi
                ),
                "/AppHostConfig:" + appHostConfig
            )) {
                p.Wait();
                DumpOutput(p);
                Assert.AreEqual(0, p.ExitCode);
            }

            foreach (var keyValue in envVars) {
                using (var p = ProcessOutput.RunHiddenAndCapture(
                    appCmd, "set", "config", "/section:system.webServer/fastCgi",
                    string.Format(
                        "/+[fullPath='{0}', arguments='\"{1}\"'].environmentVariables.[name='{2}',value='{3}']",
                        python, wfastcgi, keyValue.Key, keyValue.Value
                    ),
                    "/commit:apphost",
                    "/AppHostConfig:" + appHostConfig
                )) {
                    p.Wait();
                    DumpOutput(p);
                    Assert.AreEqual(0, p.ExitCode);
                }
            }

            using(var p = ProcessOutput.RunHiddenAndCapture(
                appCmd, "add", "site", "/name:TestSite",
                "/bindings:http://localhost:8181",
                "/physicalPath:" + Path.GetDirectoryName(appHostConfig),
                "/AppHostConfig:" + appHostConfig
            )) {
                p.Wait();
                DumpOutput(p);
                Assert.AreEqual(0, p.ExitCode);
            }
       }

        private static void DumpOutput(ProcessOutput process) {
            Console.WriteLine(process.Arguments);
            foreach (var line in process.StandardOutputLines) {
                Console.WriteLine(line);
            }
            foreach (var line in process.StandardErrorLines) {
                Console.Error.WriteLine(line);
            }
        }

        private void EnsureDjango() {
            EnsureDjango(InterpreterPath);
        }

        private static void EnsureDjango(string python) {
            using (var proc = ProcessOutput.RunHiddenAndCapture(python, "-c", "import django")) {
                proc.Wait();
                if (proc.ExitCode != 0) {
                    DumpOutput(proc);
                    Assert.Inconclusive("Django must be installed into {0} for this test", python);
                }
            }
        }

        private static WebSite ConfigureIISForDjango(string appCmd, string python, string djangoSettings) {
            // TODO If a python interpreter does not have Django installed, check another interpreter
            EnsureDjango(python);

            var site = CreateSite();
            Console.WriteLine("Site: {0}", site);

            ConfigureIIS(
                appCmd,
                Path.Combine(site, "applicationHost.config"),
                python,
                Path.Combine(site, "wfastcgi.py"),
                new Dictionary<string, string>() {
                    { "DJANGO_SETTINGS_MODULE", djangoSettings },
                    { "PYTHONPATH", "" },
                    { "WSGI_HANDLER", "django.core.handlers.wsgi.WSGIHandler()" }
                }
            );

            var module = djangoSettings.Split(new[] { '.' }, 2)[0];
            FileUtils.CopyDirectory(
                TestData.GetPath("TestData", "WFastCgi", module),
                Path.Combine(site, module)
            );


            Console.WriteLine("Site created at {0}", site);
            return new WebSite(site);
        }

        private static WebSite ConfigureIISForCustomHandler(string appCmd, string python, string handler) {
            var site = CreateSite();
            Console.WriteLine("Site: {0}", site);

            ConfigureIIS(
                appCmd,
                Path.Combine(site, "applicationHost.config"),
                python,
                Path.Combine(site, "wfastcgi.py"),
                new Dictionary<string, string>() {
                    { "WSGI_HANDLER", handler },
                    { "WSGI_LOG", Path.Combine(site, "log.txt") }
                }
            );

            var module = handler.Split(new[] { '.' }, 2)[0];
            try {
                File.Copy(
                    TestData.GetPath("TestData", "WFastCGI", module + ".py"),
                    Path.Combine(site, module + ".py"),
                    true
                );
            } catch (IOException ex) {
                Console.WriteLine("Failed to copy {0}.py: {1}", module, ex);
            }


            Console.WriteLine("Site created at {0}", site);
            return new WebSite(site);
        }

        public virtual PythonVersion PythonVersion {
            get {
                return PythonPaths.Python27_x64 ?? PythonPaths.Python27;
            }
        }

        public string InterpreterPath {
            get {
                return PythonVersion.InterpreterPath;
            }
        }

        public virtual string AppCmdPath {
            get {
                return Path.Combine(
                    Path.GetDirectoryName(IisExpressPath),
                    "appcmd.exe"
                );
            }
        }

        class WebSite : IDisposable {
            private readonly string _dir;
            private ProcessOutput _process;

            public WebSite(string dir) {
                _dir = dir;
            }

            public string SiteDir {
                get {
                    return _dir;
                }
            }

            public void StartServer() {
                _process = ProcessOutput.Run(
                    IisExpressPath,
                    new[] { "/config:" + Path.Combine(_dir, "applicationHost.config"), "/systray:false" },
                    null,
                    null,
                    false,
                    new OutputRedirector("IIS")
                );
                Console.WriteLine("Server started: {0}", _process.Arguments);
            }

            public WebResponse Request(string uri, bool printError = true) {
                WebRequest req = WebRequest.Create(
                    "http://localhost:8181/" + uri
                );
                try {
                    return req.GetResponse();
                } catch (WebException ex) {
                    if (printError) {
                        Console.WriteLine(new StreamReader(ex.Response.GetResponseStream()).ReadToEnd());
                    }
                    throw;
                }
            }

            public void StopServer() {
                var p = Interlocked.Exchange(ref _process, null);
                if (p != null) {
                    if (!p.Wait(TimeSpan.FromSeconds(5))) {
                        p.Kill();
                    }
                    p.Dispose();
                }
            }

            #region IDisposable Members

            public void Dispose() {
                StopServer();
            }

            #endregion
        }


        #region Test Cases

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoNewApp() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoApp"),
                new GetAndValidateUrl(GetLocalUrl(), ValidateWelcomeToDjango)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoNewAppUrlRewrite() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoAppUrlRewrite"),
                new GetAndValidateUrl(GetLocalUrl(), ValidateWelcomeToDjango)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestHelloWorld() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "HelloWorld"),
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestHelloWorldCallable() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "HelloWorldCallable"),
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
            );
        }

        /// <summary>
        /// Handler doesn't exist in imported module
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestBadHandler() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("'module' object has no attribute 'does_not_exist'"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER module doesn't exist
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestBadHandler2() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler2"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("\"myapp_does_not_exist.does_not_exist\" could not be imported"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER raises an exceptoin during import
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHandler3() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler3"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("Exception: handler file is raising"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER is just set to modulename
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHandler4() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler4"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("\"myapp\" could not be imported"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER env var isn't set at all
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHandler5() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler5"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("WSGI_HANDLER env var must be set"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER points to object of NoneType
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHandler6() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler6"),
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("\"myapp.app\" could not be imported"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER writes to std err and std out, and raises.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHandler7() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHandler7"),
                new GetAndValidateErrorUrl(GetLocalUrl(),
                    ValidateString("something to std err"),
                    ValidateString("something to std out")
                )
            );
        }

        /// <summary>
        /// Validates environment dict passed to handler
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestEnvironment() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "Environment"),
                new GetAndValidateUrl(
                    GetLocalUrl("/fob/oar/baz?quox=100"),
                    ValidateString("QUERY_STRING: quox=100\nPATH_INFO: /fob/oar/baz\nSCRIPT_NAME: \n")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to .py or .config files are detected
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        [TestCategory("10s")]
        public void TestFileSystemChanges() {
            var location = TestData.GetTempPath();
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\WFastCgi\FileSystemChanges"), location);

            IisExpressTest(
                location,
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile(Path.Combine(location, "myapp.py"), @"def handler(environment, start_response):
    start_response('200', '')
    return [b'goodbye world!']"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                ),
                ReplaceInFile(Path.Combine(location, "web.config"), "myapp.handler", "myapp2.handler"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("myapp2 world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to .py files in a subdirectory are detected
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestFileSystemChangesPackage() {
            var location = TestData.GetTempPath();
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\WFastCgi\FileSystemChangesPackage"), location);
            
            IisExpressTest(
                location,
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile(Path.Combine(location, "myapp", "__init__.py"), @"def handler(environment, start_response):
    start_response('200', '')
    return [b'goodbye world!']"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to a file pattern specified in web.config changes.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestFileSystemChangesCustomRegex() {
            var location = TestData.GetTempPath();
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\WFastCgi\FileSystemChangesCustomRegex"), location);

            IisExpressTest(
                location,
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile(Path.Combine(location, "myapp.data"), "goodbye world!"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi doesn't exit when file system change checks are disabled.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestFileSystemChangesDisabled() {
            var location = TestData.GetTempPath();
            FileUtils.CopyDirectory(TestData.GetPath(@"TestData\WFastCgi\FileSystemChangesDisabled"), location);
            
            IisExpressTest(
                location,
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile(Path.Combine(location, "myapp.py"), @"def handler(environment, start_response):
    start_response('200', '')
    return ['goodbye world!']"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                )
            );
        }

        /// <summary>
        /// Validates that we can setup IIS to serve static files properly
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestStaticFiles() {
            EnsureDjango();
            IisExpressTest(
                CollectStaticFiles(TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleApp")),
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleApp"),
                null,
                new GetAndValidateUrl(
                    GetLocalUrl("/static/fob/helloworld.txt"),
                    ValidateString("hello world from a static text file!")
                )
            );
        }

        /// <summary>
        /// Validates that we can setup IIS to serve static files properly
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestStaticFilesUrlRewrite() {
            EnsureDjango();
            IisExpressTest(
                CollectStaticFiles(TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleAppUrlRewrite")),
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleAppUrlRewrite"),
                null,
                new GetAndValidateUrl(
                    GetLocalUrl("/static/fob/helloworld.txt"),
                    ValidateString("hello world from a static text file!")
                )
            );
        }

        /// <summary>
        /// Validates environment dict passed to handler using URL rewriting
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestEnvironmentUrlRewrite() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "EnvironmentUrlRewrite"),
                new GetAndValidateUrl(
                    GetLocalUrl("/fob/oar/baz?quox=100"),
                    ValidateString("QUERY_STRING: quox=100\nPATH_INFO: /fob/oar/baz\nSCRIPT_NAME: \n")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestStreamingHandler() {
            int partCount = 0;
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "StreamingHandler"),
                new GetAndValidateUrlPieces(
                    GetLocalUrl(),
                    piece => {
                        if (partCount++ == 0) {
                            Assert.AreEqual("Hello world!", piece);

                            var req = WebRequest.Create(GetLocalUrl() + "/fob");
                            var response = req.GetResponse();
                            var reader = new StreamReader(response.GetResponseStream());
                        } else {
                            Assert.AreEqual(partCount, 2);
                            Assert.AreEqual("goodbye world!", piece);
                        }
                    }

                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoQueryString() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleApp"),
                new GetAndValidateUrl(
                    GetLocalUrl("?fob=42&oar=100"),
                    ValidateString("GET: fob=42&oar=100", "GET: oar=100&fob=42")
                )
            );
        }

        /// <summary>
        /// Tests that we can post values to Django and it gets them 
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoPost() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleApp"),
                new PostAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("POST: fob=42&oar=100", "POST: oar=100&fob=42"),
                    new KeyValuePair<string, string>("fob", "42"),
                    new KeyValuePair<string, string>("oar", "100")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoPath() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleApp"),
                new GetAndValidateUrl(
                    GetLocalUrl("/fob/oar/baz"),
                    ValidateString("path: /fob/oar/baz\npath_info: /fob/oar/baz")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoQueryStringUrlRewrite() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleAppUrlRewrite"),
                new GetAndValidateUrl(
                    GetLocalUrl("?fob=42&oar=100"),
                    ValidateString("GET: fob=42&oar=100", "GET: oar=100&fob=42")
                )
            );
        }

        /// <summary>
        /// Tests that we can post values to Django and it gets them when using URL rewriting
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoPostUrlRewrite() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleAppUrlRewrite"),
                new PostAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("POST: fob=42&oar=100", "POST: oar=100&fob=42"),
                    new KeyValuePair<string, string>("fob", "42"),
                    new KeyValuePair<string, string>("oar", "100")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1_FAILING)]
        public void TestDjangoPathUrlRewrite() {
            EnsureDjango();
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "DjangoSimpleAppUrlRewrite"),
                new GetAndValidateUrl(
                    GetLocalUrl("/fob/oar/baz"),
                    ValidateString("path: /fob/oar/baz\npath_info: /fob/oar/baz")
                )
            );
        }

        /// <summary>
        /// Tests expand path
        /// </summary>
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestExpandPathEnvironmentVariables() {
            IisExpressTest(
                null,
                TestData.GetPath("TestData", "WFastCgi", "ExpandPathEnvironmentVariables"),
                new Dictionary<string, string> {
                    { "SITELOCATION", TestData.GetPath("TestData", "WFastCgi", "ExpandPathEnvironmentVariables") },
                    { "OTHERLOCATION", TestData.GetPath("TestData", "WFastCgi", "ExpandPathEnvironmentVariablesOtherDir") }
                },
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHeaders1() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHeaders"),
                new GetAndValidateErrorUrl(
                    GetLocalUrl("/test_1"),
                    ValidateString("500 Error")
                )
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHeaders2() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHeaders"),
                new GetAndValidateErrorUrl(
                    GetLocalUrl("/test_2"),
                    ValidateString("Exception")
                )
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHeaders3() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHeaders"),
                new GetAndValidateErrorUrl(
                    GetLocalUrl("/test_3"),
                    ValidateString("start_response has already been called")
                )
            );
        }
        
        [TestMethod, Priority(UnitTestPriority.P1)]
        public void TestBadHeaders4() {
            IisExpressTest(
                TestData.GetPath("TestData", "WFastCgi", "BadHeaders"),
                new GetAndValidateErrorUrl(
                    GetLocalUrl("/test_4"),
                    ValidateString("start_response has not yet been called")
                )
            );
        }

        #endregion

        #region Test Case Validators/Actions

        abstract class Validator {
            public abstract void Validate();

            public static implicit operator Action(Validator self) {
                return self.Validate;
            }
        }

        /// <summary>
        /// Requests the specified URL, the web page request should succeed, and
        /// then the contents of the web page are validated with the provided delegate.
        /// </summary>
        class GetAndValidateUrl : Validator {
            private readonly string Url;
            private readonly Action<string> Validation;

            public GetAndValidateUrl(string url, Action<string> validation) {
                Url = url;
                Validation = validation;
            }

            public override void Validate() {
                Console.WriteLine("Requesting Url: {0}", Url);
                var req = WebRequest.Create(Url);
                try {
                    var response = req.GetResponse();
                    var reader = new StreamReader(response.GetResponseStream());
                    var result = reader.ReadToEnd();

                    Console.WriteLine("Validating Url");
                    Validation(result);
                } catch (WebException wex) {
                    var reader = new StreamReader(wex.Response.GetResponseStream());
                    Assert.Fail("Failed to get response: {0}", reader.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Requests the specified URL, the web page request should succeed, and
        /// then the contents of the web page are validated with the provided delegate.
        /// </summary>
        class PostAndValidateUrl : Validator {
            private readonly string Url;
            private readonly Action<string> Validation;
            private readonly KeyValuePair<string, string>[] PostValues;

            public PostAndValidateUrl(string url, Action<string> validation, params KeyValuePair<string, string>[] postValues) {
                Url = url;
                Validation = validation;
                PostValues = postValues;
            }

            public override void Validate() {
                Console.WriteLine("Requesting Url: {0}", Url);
                var req = WebRequest.Create(Url);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                StringBuilder data = new StringBuilder();
                foreach (var keyValue in PostValues) {
                    if (data.Length > 0) {
                        data.Append('&');
                    }
                    data.Append(HttpUtility.UrlEncode(keyValue.Key));
                    data.Append("=");
                    data.Append(HttpUtility.UrlEncode(keyValue.Value));
                }
                var bytes = Encoding.UTF8.GetBytes(data.ToString());

                req.ContentLength = bytes.Length;

                var stream = req.GetRequestStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Close();

                try {
                    var response = req.GetResponse();
                    var reader = new StreamReader(response.GetResponseStream());
                    var result = reader.ReadToEnd();

                    Console.WriteLine("Validating Url");
                    Validation(result);
                } catch (WebException wex) {
                    var reader = new StreamReader(wex.Response.GetResponseStream());
                    Assert.Fail("Failed to get response: {0}", reader.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Requests the specified URL, the web page request should succeed, and
        /// then the contents of the web page are validated with the provided delegate.
        /// </summary>
        class GetAndValidateUrlPieces : Validator {
            private readonly string Url;
            private readonly Action<string> Validation;

            public GetAndValidateUrlPieces(string url, Action<string> validation) {
                Url = url;
                Validation = validation;
            }

            public override void Validate() {
                Console.WriteLine("Requesting Url: {0}", Url);
                var req = WebRequest.Create(Url);
                var response = req.GetResponse();
                var reader = new StreamReader(response.GetResponseStream());
                Console.WriteLine("Validating");
                string line;
                while ((line = reader.ReadLine()) != null) {
                    Console.WriteLine("Validating piece: {0}", line);
                    Validation(line);
                }
            }
        }

        /// <summary>
        /// Requests the specified URL, the web page request should succeed, and
        /// then the contents of the web page are validated with the provided delegate.
        /// </summary>
        class GetAndValidateErrorUrl : Validator {
            private readonly string Url;
            private readonly Action<string>[] Validators;

            public GetAndValidateErrorUrl(string url, params Action<string>[] validatiors) {
                Url = url;
                Validators = validatiors;
            }

            public override void Validate() {
                Console.WriteLine("Requesting URL: {0}", Url);
                var req = WebRequest.Create(Url);
                try {
                    var response = req.GetResponse();
                    Assert.Fail("Got successful response: " + new StreamReader(response.GetResponseStream()).ReadToEnd());
                } catch (WebException we) {
                    var reader = new StreamReader(we.Response.GetResponseStream());
                    var result = reader.ReadToEnd();

                    Console.WriteLine("Received: {0}", result);

                    foreach (var validator in Validators) {
                        validator(result);
                    }
                }

            }

            public static implicit operator Action(GetAndValidateErrorUrl self) {
                return self.Validate;
            }
        }

        private Action CollectStaticFiles(string location) {
            location = TestData.GetPath(location);
            return () => {
                using (var p = ProcessOutput.Run(
                    InterpreterPath,
                    new[] { Path.Combine(location, "manage.py"), "collectstatic", "--noinput" },
                    location,
                    null,
                    false,
                    new OutputRedirector("manage.py")
                )) {
                    p.Wait();
                    Assert.AreEqual(0, p.ExitCode);
                }
            };
        }

        private static Action ReplaceInFile(string filename, string oldText, string newText) {
            return () => {
                Console.WriteLine("Replacing text in {0}", filename);
                File.WriteAllText(filename, File.ReadAllText(filename).Replace(oldText, newText));
                System.Threading.Thread.Sleep(3000);
            };
        }

        private static Action PatchFile(string filename, string newText) {
            return () => {
                System.Threading.Thread.Sleep(1000);
                Console.WriteLine("Patching file {0}", filename);
                File.WriteAllText(filename, newText);
                System.Threading.Thread.Sleep(4000);
            };
        }

        private static string MakeAssertMessage(string[] expected, string actual) {
            var combined = string.Join(
                Environment.NewLine,
                expected.Select(s => string.Format(" * \"{0}\"", s))
            );

            return string.Format(
                "Expected any of:{0}{1}{0}Actual:{0}{2}",
                Environment.NewLine,
                combined,
                actual
            );
        }

        private static Action<string> ValidateString(params string[] text) {
            return (received) => {
                Assert.IsTrue(text.Any(t => received.Contains(t)), MakeAssertMessage(text, received));
            };
        }

        private static void ValidateWelcomeToDjango(string text) {
            Assert.IsTrue(text.IndexOf("Congratulations on your first Django-powered page.") != -1, "It worked page failed to be returned");
        }

        private static void ValidateHelloWorld(string text) {
            Assert.IsTrue(text.IndexOf("hello world!") != -1, "hello world page not returned");
        }

        #endregion

        #region Test Case Infrastructure

        private void IisExpressTest(string location, params Action[] actions) {
            IisExpressTest(null, location, null, actions);
        }

        private void IisExpressTest(
            Action initialization,
            string sourceLocation,
            Dictionary<string, string> environment,
            params Action[] actions
        ) {
            Console.WriteLine("Current Directory: {0}", Environment.CurrentDirectory);
            Console.WriteLine("WFastCgiPath: {0}", WFastCgiPath);
            if (!Path.IsPathRooted(sourceLocation)) {
                sourceLocation = TestData.GetPath(sourceLocation);
            }
            Console.WriteLine("Test source: {0}", sourceLocation);
            var location = TestData.GetTempPath();
            FileUtils.CopyDirectory(sourceLocation, location);

            var appConfig = Path.Combine(TestData.GetTempPath(), "applicationHost.config");
            var baseConfig = File.ReadAllText(TestData.GetPath("TestData", "WFastCgi", "applicationHost.config"));
            baseConfig = baseConfig
                .Replace("[PYTHONPATH]", InterpreterPath)
                .Replace("[WFASTCGIPATH]", WFastCgiPath)
                .Replace("[SITE_LOCATION]", location);

            File.WriteAllText(appConfig, baseConfig);

            var webConfigLocSource = Path.Combine(location, "web.config.source");
            if (!File.Exists(webConfigLocSource)) {
                webConfigLocSource = Path.Combine(location, "web.config");
            }
            var webConfigLoc = Path.Combine(location, "web.config");
            var webConfigContents = File.ReadAllText(webConfigLocSource);
            File.WriteAllText(
                webConfigLoc,
                webConfigContents.Replace("[PYTHONPATH]", InterpreterPath)
                                .Replace("[WFASTCGIPATH]", WFastCgiPath)
                                .Replace("[SITEPATH]", Path.GetFullPath(location))
            );

            var env = environment != null ? new Dictionary<string, string>(environment) : new Dictionary<string, string>();
            env["WSGI_LOG"] = Path.Combine(location, "log.txt");

            if (initialization != null) {
                Console.WriteLine("Initializing");
                initialization();
            }

            using (var p = ProcessOutput.Run(
                IisExpressPath,
                new[] { "/config:" + appConfig, "/site:WebSite1", "/systray:false", "/trace:info" },
                null,
                env,
                false,
                new OutputRedirector("IIS")
            )) {
                Console.WriteLine("Starting IIS Express: {0}", p.Arguments);

                try {
                    foreach (var action in actions) {
                        action();
                    }
                } finally {
                    p.Kill();
                }
            }
        }

        private class OutputRedirector : Redirector {
            private readonly string _format;

            public OutputRedirector(string category) {
                if (string.IsNullOrEmpty(category)) {
                    _format = "{0}";
                } else {
                    _format = category + ": {0}";
                }
            }
            public override void WriteLine(string line) {
                Console.WriteLine(_format, line ?? "(null)");
            }

            public override void WriteErrorLine(string line) {
                Console.Error.WriteLine(_format, line ?? "(null)");
            }
        }

        private static string IisExpressPath {
            get {
                var iisExpressPath = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\IISExpress\\10.0", "InstallPath", null) as string ??
                    Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\IISExpress\\8.0", "InstallPath", null) as string;
                if (iisExpressPath == null) {
                    Assert.Inconclusive("Can't find IIS Express");
                    return null;
                }
                return Path.Combine(iisExpressPath, "iisexpress.exe");
            }
        }

        private static string GetLocalUrl(string path = null) {
            string res = "http://localhost:8080";
            if (!String.IsNullOrWhiteSpace(path)) {
                return res + path;
            }
            return res;
        }

        private static string WFastCgiPath {
            get {
                var wfastcgiPath = Path.Combine(Path.GetDirectoryName(typeof(FastCgiTests2x).Assembly.Location), "wfastcgi.py");
                if (File.Exists(wfastcgiPath)) {
                    return wfastcgiPath;
                }

                Assert.Inconclusive("Failed to find wfastcgi.py");
                return null;
            }
        }

        #endregion
    }

}
