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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Microsoft.PythonTools.Django;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using TestUtilities;
using TestUtilities.Python;

namespace FastCgiTest {
    [TestClass]
    public class FastCgiTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void DjangoNewApp() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoApplication.settings")) {
                site.StartServer();

                CopyDir("TestData", site.SiteDir);

                var response = site.Request("");
                Console.WriteLine(response.ContentType);
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Console.WriteLine(content);

                Assert.IsTrue(content.IndexOf("Welcome to Django") != -1);
            }
        }

        [TestMethod, Priority(0)]
        public void DjangoHelloWorld() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                site.StartServer();

                CopyDir("TestData", site.SiteDir);
                
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
        [TestMethod, Priority(0)]
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

        [TestMethod, Priority(0)]
        public void LargeResponse() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                File.Copy("TestData\\DjangoTestApp\\web.config", Path.Combine(site.SiteDir, "web.config"));

                site.StartServer();

                CopyDir("TestData", site.SiteDir);

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


        [TestMethod, Priority(0)]
        public void DjangoHelloWorldParallel() {
            using (var site = ConfigureIISForDjango(AppCmdPath, InterpreterPath, "DjangoTestApp.settings")) {
                site.StartServer();

                CopyDir("TestData", site.SiteDir);

                const int threadCnt = 12;
                const int requests = 1000;
                Thread[] threads = new Thread[threadCnt];
                int count = 0;

                for (int i = 0; i < threads.Length; i++) {
                    threads[i] = new Thread(() => {
                        for (int j = 0; j < requests; j++) {
                            var response = site.Request("");
                            var stream = response.GetResponseStream();
                            var content = new StreamReader(stream).ReadToEnd();
                            Assert.AreEqual(content, "<html><body>Hello World!</body></html>");
                            Interlocked.Increment(ref count);
                        }
                    });
                    threads[i].Start();
                }

                for (int i = 0; i < threads.Length; i++) {
                    threads[i].Join();
                }
                Assert.AreEqual(count, threadCnt * requests);
            }
        }

        [TestMethod, Priority(0)]
        public void CustomHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.handler")) {
                CopyDir("TestData", site.SiteDir);

                site.StartServer();

                var response = site.Request("");
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Assert.AreEqual(content, "<html><body>hello world</body></html>");

                Assert.AreEqual("42", response.Headers["Custom-Header"]);
            }
        }

        [TestMethod, Priority(0)]
        public void CustomCallableHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.callable_handler()")) {
                CopyDir("TestData", site.SiteDir);

                site.StartServer();

                var response = site.Request("");
                var stream = response.GetResponseStream();
                var content = new StreamReader(stream).ReadToEnd();
                Assert.AreEqual(content, "<html><body>hello world</body></html>");
            }
        }

        [TestMethod, Priority(0)]
        public void ErrorHandler() {
            using (var site = ConfigureIISForCustomHandler(AppCmdPath, InterpreterPath, "custom_handler.error_handler")) {
                CopyDir("TestData", site.SiteDir);

                site.StartServer();
                try {
                    var response = site.Request("");
                } catch (WebException wex) {
                    var stream = wex.Response.GetResponseStream();
                    var content = new StreamReader(stream).ReadToEnd();

                    Assert.AreEqual(((HttpWebResponse)wex.Response).StatusCode, HttpStatusCode.NotFound);

                    Assert.AreEqual(content, "<html><body>Sorry folks, we're closed for two weeks to clean and repair America's favorite family fun park</body></html>");

                    Assert.AreEqual(wex.Status, WebExceptionStatus.ProtocolError);
                }
            }
        }

        public static string CreateSite() {
            string dirName = GetTestDirectory();

            File.Copy("TestData\\applicationhostOriginal.config",
                Path.Combine(dirName, "applicationHost.config"));

            File.Copy(
                WFastCgiPath,
                Path.Combine(dirName, "wfastcgi.py")
            );

            Directory.CreateDirectory(Path.Combine(dirName, "WebSite"));
            return dirName;
        }

        private static string GetTestDirectory() {
            string dirName;
            while (true) {
                dirName = Path.Combine(
                    Environment.CurrentDirectory,
                    Path.GetFileName(Path.GetRandomFileName())
                );
                try {
                    Directory.CreateDirectory(dirName);
                    break;
                } catch {
                }
            }
            return dirName;
        }

        public static void ConfigureIIS(string appCmd, string appHostConfig, string python, string wfastcgi, Dictionary<string, string> envVars) {
            var psi = new ProcessStartInfo(
                appCmd,
                String.Format(
                    "set config /section:system.webServer/fastCGI " +
                    "\"/+[fullPath='{0}', arguments='\"\"\"{1}\"\"\"']\" \"/AppHostConfig:{2}\"",
                    python,
                    wfastcgi,
                    appHostConfig
                )
            );
            RunProcess(psi);

            psi = new ProcessStartInfo(
                appCmd,
                String.Format(
                    "set config /section:system.webServer/handlers " +
                    "\"/+[name='Python_via_FastCGI',path='*',verb='*',modules='FastCgiModule',scriptProcessor='{0}|\"\"\"{1}\"\"\"',resourceType='Unspecified']\" " +
                    "\"/AppHostConfig:{2}\"",
                    python,
                    wfastcgi,
                    appHostConfig
                )
            );
            RunProcess(psi);

            foreach (var keyValue in envVars) {
                psi = new ProcessStartInfo(
                    appCmd,
                    String.Format(
                        "set config -section:system.webServer/fastCgi " +
                        "/+\"[fullPath='{0}', arguments='\"\"\"{1}\"\"\"'].environmentVariables.[name='{2}',value='{3}']\" " +
                        "/commit:apphost \"/AppHostConfig:{4}\"",
                        python,
                        wfastcgi,
                        keyValue.Key,
                        keyValue.Value,
                        appHostConfig
                    )
                );
                RunProcess(psi);
            }

            psi = new ProcessStartInfo(
                appCmd,
                String.Format(
                    "add site /name:\"TestSite\" /bindings:http://localhost:8181 \"/physicalPath:{0}\" \"/AppHostConfig:{1}\"",
                    Path.GetDirectoryName(appHostConfig),
                    appHostConfig
                )
            );
            RunProcess(psi);
        }

        private static void RunProcess(ProcessStartInfo psi) {
            var proc = StartProcess(psi);

            proc.WaitForExit();
            if (proc.ExitCode != 0) {
                throw new Exception("Update failed");
            }
        }

        private static Process StartProcess(ProcessStartInfo psi) {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            var proc = Process.Start(psi);
            proc.OutputDataReceived += new DataReceivedEventHandler(proc_OutputDataReceived);
            proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            return proc;
        }

        private static void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            Console.Error.WriteLine(e.Data);
        }

        private static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            Console.WriteLine(e.Data);
        }

        private static WebSite ConfigureIISForDjango(string appCmd, string python, string djangoSettings) {
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
                    { "WSGI_HANDLER", handler }
                }

            );

            Console.WriteLine("Site created at {0}", site);
            return new WebSite(site);
        }

        public virtual string InterpreterPath {
            get {
                return PythonPaths.Python27.Path;
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

        private static void CopyDir(string source, string target) {
            foreach (var dir in Directory.GetDirectories(source)) {
                var targetDir = Path.Combine(target, Path.GetFileName(dir));
                Console.WriteLine("Creating dir: {0}", targetDir);
                Directory.CreateDirectory(targetDir);
                CopyDir(dir, targetDir);
            }

            foreach (var file in Directory.GetFiles(source)) {
                var targetFile = Path.Combine(target, Path.GetFileName(file));
                Console.WriteLine("Deploying: {0} -> {1}", file, targetFile);
                File.Copy(file, targetFile);
            }
        }

        class WebSite : IDisposable {
            private readonly string _dir;
            private Process _process;

            public WebSite(string dir) {
                _dir = dir;
            }

            public string SiteDir {
                get {
                    return _dir;
                }
            }

            public void StartServer() {
                var psi = new ProcessStartInfo(
                    IisExpressPath,
                    String.Format(
                        "/config:\"{0}\" /systray:false",
                        Path.Combine(_dir, "applicationHost.config")
                    )
                );
                _process = StartProcess(psi);
                Console.WriteLine("Server started: {0} {1}", psi.FileName, psi.Arguments);
            }

            public WebResponse Request(string uri) {
                WebRequest req = WebRequest.Create(
                    "http://localhost:8181/" + uri
                );                
                return req.GetResponse();
            }

            public void StopServer() {
                if (_process != null) {
                    _process.Kill();
                    _process = null;
                }
            }

            #region IDisposable Members

            public void Dispose() {
                StopServer();
            }

            #endregion
        }


        #region Test Cases

        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoNewApp() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoApp",
                new GetAndValidateUrl(GetLocalUrl(), ValidateWelcomeToDjango)
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoNewAppUrlRewrite() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoAppUrlRewrite",
                new GetAndValidateUrl(GetLocalUrl(), ValidateWelcomeToDjango)
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestHelloWorld() {
            IisExpressTest(
                "TestData\\WFastCgi\\HelloWorld",
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestHelloWorldCallable() {
            IisExpressTest(
                "TestData\\WFastCgi\\HelloWorldCallable",
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
            );
        }

        /// <summary>
        /// Handler doesn't exist in imported module
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("'module' object has no attribute 'does_not_exist'"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER module doesn't exist
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler2() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler2",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("No module named myapp_does_not_exist"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER raises an exceptoin during import
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler3() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler3",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("Exception: handler file is raising"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER is just set to modulename
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler4() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler4",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("WSGI_HANDLER must be set to module_name.wsgi_handler, got "))
            );
        }

        /// <summary>
        /// WSGI_HANDLER env var isn't set at all
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler5() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler5",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("WSGI_HANDLER env var must be set"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER points to object of NoneType
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler6() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler6",
                new GetAndValidateErrorUrl(GetLocalUrl(), ValidateString("WSGI_HANDLER \"myapp.app\" was set to None"))
            );
        }

        /// <summary>
        /// WSGI_HANDLER writes to std err and std out, and raises.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestBadHandler7() {
            IisExpressTest(
                "TestData\\WFastCgi\\BadHandler7",
                new GetAndValidateErrorUrl(GetLocalUrl(),
                    ValidateString("something to std err"),
                    ValidateString("something to std out")
                )
            );
        }

        /// <summary>
        /// Validates environment dict passed to handler
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestEnvironment() {
            IisExpressTest(
                "TestData\\WFastCgi\\Environment",
                new GetAndValidateUrl(
                    GetLocalUrl("/foo/bar/baz?quox=100"),
                    ValidateString("QUERY_STRING: quox=100\nPATH_INFO: /foo/bar/baz\nSCRIPT_NAME: \n")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to .py or .config files are detected
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestFileSystemChanges() {
            IisExpressTest(
                "TestData\\WFastCgi\\FileSystemChanges",
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile("TestData\\WFastCgi\\FileSystemChanges\\myapp.py", @"def handler(environment, start_response):
    start_response('200', '')
    return ['goodbye world!']"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                ),
                ReplaceInFile("TestData\\WFastCgi\\FileSystemChanges\\web.config", "myapp.handler", "myapp2.handler"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("myapp2 world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to .py files in a subdirectory are detected
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestFileSystemChangesPackage() {
            IisExpressTest(
                "TestData\\WFastCgi\\FileSystemChangesPackage",
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile("TestData\\WFastCgi\\FileSystemChangesPackage\\myapp\\__init__.py", @"def handler(environment, start_response):
    start_response('200', '')
    return ['goodbye world!']"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi exits when changes to a file pattern specified in web.config changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestFileSystemChangesCustomRegex() {
            IisExpressTest(
                "TestData\\WFastCgi\\FileSystemChangesCustomRegex",
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile("TestData\\WFastCgi\\FileSystemChangesCustomRegex\\myapp.txt", "goodbye world!"),
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("goodbye world!")
                )
            );
        }

        /// <summary>
        /// Validates wfastcgi doesn't exit when file system change checks are disabled.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestFileSystemChangesDisabled() {
            IisExpressTest(
                "TestData\\WFastCgi\\FileSystemChangesDisabled",
                new GetAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("hello world!")
                ),
                PatchFile("TestData\\WFastCgi\\FileSystemChangesDisabled\\myapp.py", @"def handler(environment, start_response):
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
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestStaticFiles() {
            IisExpressTest(
                CollectStaticFiles("TestData\\WFastCgi\\DjangoSimpleApp"),
                "TestData\\WFastCgi\\DjangoSimpleApp",
                new GetAndValidateUrl(
                    GetLocalUrl("/static/foo/helloworld.txt"),
                    ValidateString("hello world from a static text file!")
                )
            );
        }

        /// <summary>
        /// Validates that we can setup IIS to serve static files properly
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestStaticFilesUrlRewrite() {
            IisExpressTest(
                CollectStaticFiles("TestData\\WFastCgi\\DjangoSimpleAppUrlRewrite"),
                "TestData\\WFastCgi\\DjangoSimpleAppUrlRewrite",
                new GetAndValidateUrl(
                    GetLocalUrl("/static/foo/helloworld.txt"),
                    ValidateString("hello world from a static text file!")
                )
            );
        }

        /// <summary>
        /// Validates environment dict passed to handler using URL rewriting
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestEnvironmentUrlRewrite() {
            IisExpressTest(
                "TestData\\WFastCgi\\EnvironmentUrlRewrite",
                new GetAndValidateUrl(
                    GetLocalUrl("/foo/bar/baz?quox=100"),
                    ValidateString("QUERY_STRING: quox=100\nPATH_INFO: /foo/bar/baz\nSCRIPT_NAME: \n")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestStreamingHandler() {
            int partCount = 0;
            IisExpressTest(
                "TestData\\WFastCgi\\StreamingHandler",
                new GetAndValidateUrlPieces(
                    GetLocalUrl(),
                    piece => {
                        if (partCount++ == 0) {
                            Assert.AreEqual("Hello world!", piece);

                            var req = WebRequest.Create(GetLocalUrl() + "/foo");
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
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoQueryString() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleApp",
                new GetAndValidateUrl(
                    GetLocalUrl("?foo=42&bar=100"),
                    ValidateString("GET: foo=42&bar=100")
                )
            );
        }

        /// <summary>
        /// Tests that we can post values to Django and it gets them 
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoPost() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleApp",
                new PostAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("POST: foo=42&bar=100"),
                    new KeyValuePair<string, string>("foo", "42"),
                    new KeyValuePair<string, string>("bar", "100")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoPath() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleApp",
                new GetAndValidateUrl(
                    GetLocalUrl("/foo/bar/baz"),
                    ValidateString("path: /foo/bar/baz\npath_info: /foo/bar/baz")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoQueryStringUrlRewrite() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleAppUrlRewrite",
                new GetAndValidateUrl(
                    GetLocalUrl("?foo=42&bar=100"),
                    ValidateString("GET: foo=42&bar=100")
                )
            );
        }

        /// <summary>
        /// Tests that we can post values to Django and it gets them when using URL rewriting
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoPostUrlRewrite() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleAppUrlRewrite",
                new PostAndValidateUrl(
                    GetLocalUrl(),
                    ValidateString("POST: foo=42&bar=100"),
                    new KeyValuePair<string, string>("foo", "42"),
                    new KeyValuePair<string, string>("bar", "100")
                )
            );
        }

        /// <summary>
        /// Tests that we send portions of the response as they are given to us.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestDjangoPathUrlRewrite() {
            IisExpressTest(
                "TestData\\WFastCgi\\DjangoSimpleAppUrlRewrite",
                new GetAndValidateUrl(
                    GetLocalUrl("/foo/bar/baz"),
                    ValidateString("path: /foo/bar/baz\npath_info: /foo/bar/baz")
                )
            );
        }

        /// <summary>
        /// Tests expand path
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        public void TestExpandPathEnvironmentVariables() {
            IisExpressTest(
                psi => {
                    psi.EnvironmentVariables["SITELOCATION"] = TestData.GetPath("TestData\\WFastCgi\\ExpandPathEnvironmentVariables");
                    psi.EnvironmentVariables["OTHERLOCATION"] = TestData.GetPath("TestData\\WFastCgi\\ExpandPathEnvironmentVariablesOtherDir");
                },
                "TestData\\WFastCgi\\ExpandPathEnvironmentVariables",
                new GetAndValidateUrl(GetLocalUrl(), ValidateHelloWorld)
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

        private Action<ProcessStartInfo> CollectStaticFiles(string location) {
            return (startInfo) => {
                var psi = new ProcessStartInfo(
                    PythonPaths.Python27.Path,
                    String.Format("{0} collectstatic --noinput", Path.Combine(location, "manage.py"))
                );
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                var process = Process.Start(psi);
                process.OutputDataReceived += ManagePyOutputDataReceived;
                process.ErrorDataReceived += ManagePyOutputDataReceived;
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
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

        private static Action<string> ValidateString(string text) {
            return (received) => {
                Assert.IsTrue(received.IndexOf(text) != -1, "Didn't get expected string: " + text + "Received:\r\n" + received);
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
            IisExpressTest(null, location, actions);
        }

        private void IisExpressTest(Action<ProcessStartInfo> initialization, string location, params Action[] actions) {
            Console.WriteLine(Environment.CurrentDirectory);
            var appConfig = GenerateApplicationHostConfig(location);
            var webConfigLoc = Path.Combine(location, "web.config");
            var webConfigContents = File.ReadAllText(webConfigLoc);
            File.WriteAllText(
                webConfigLoc,
                webConfigContents.Replace("[PYTHONPATH]", PythonPaths.Python27.Path)
                                .Replace("[WFASTCGIPATH]", WFastCgiPath)
                                .Replace("[SITEPATH]", Path.GetFullPath(location))
            );

            var psi = new ProcessStartInfo(IisExpressPath, String.Format("/config:\"{0}\" /site:WebSite1 /systray:false", appConfig));
            Console.WriteLine("Starting IIS Express: \"{0}\" {1}", psi.FileName, psi.Arguments);
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;

            if (initialization != null) {
                Console.WriteLine("Initializing");
                initialization(psi);
            }

            using (var proc = Process.Start(psi)) {
                proc.OutputDataReceived += IisOutputDataReceived;
                proc.ErrorDataReceived += IisOutputDataReceived;
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();

                try {
                    foreach (var action in actions) {
                        action();
                    }
                } finally {
                    proc.Kill();
                }
            }
        }

        private void ManagePyOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (e.Data != null) {
                Console.WriteLine("manage.py: {0}", e.Data);
            }
        }

        private void IisOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (e.Data != null) {
                Console.WriteLine("IIS: {0}", e.Data);
            }
        }

        private static string IisExpressPath {
            get {
                var iisExpressPath = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\IISExpress\\8.0", "InstallPath", null) as string;
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

        private static string GenerateApplicationHostConfig(string siteLocation) {
            var tempConfig = Path.Combine(GetTestDirectory(), Path.GetRandomFileName());
            StringBuilder baseConfig = new StringBuilder(File.ReadAllText("TestData\\WFastCgi\\applicationHost.config"));
            baseConfig.Replace("[PYTHONPATH]", PythonPaths.Python27.Path)
                      .Replace("[WFASTCGIPATH]", WFastCgiPath)
                      .Replace("[SITE_LOCATION]", Path.GetFullPath(siteLocation));

            File.WriteAllText(tempConfig, baseConfig.ToString());
            return tempConfig;
        }

        private static string WFastCgiPath {
            get {
                string packageLoc = Path.Combine(
                    Path.GetDirectoryName(typeof(DjangoPackage).Assembly.Location),
                    "wfastcgi.py"
                );
                if (File.Exists(packageLoc)) {
                    return packageLoc;
                }

                var wfastcgiPath = Path.Combine(
                    Environment.GetEnvironmentVariable("ProgramFiles"),
                    "MSbuild",
                    "Microsoft",
                    "VisualStudio",
                    "v" + AssemblyVersionInfo.VSVersion,
                    "Python Tools",
                    "wfastcgi.py"
                );

                if (File.Exists(wfastcgiPath)) {
                    return wfastcgiPath;
                }

                wfastcgiPath = Path.Combine(
                    Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                    "MSbuild",
                    "Microsoft",
                    "VisualStudio",
                    "v" + AssemblyVersionInfo.VSVersion,
                    "Python Tools",
                    "wfastcgi.py"
                );

                if (File.Exists(wfastcgiPath)) {
                    return wfastcgiPath;
                }

                throw new InvalidOperationException("Failed to find wfastcgi.py");
            }
        }

        #endregion
    }

}
