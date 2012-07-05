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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"Release\Product\Python\Django\wfastcgi.py")]
    [DeploymentItem(@"Release\Product\Python\FastCgiTest\TestData\", "TestData")]
    public class FastCgiTests {
        [TestMethod]
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

        [TestMethod]
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
        [TestMethod]
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

        [TestMethod]
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
                Assert.AreEqual(content.Length, expected.Length * 3000);
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


        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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
            string dirName;
            while (true) {
                dirName = Path.Combine(
                    Path.GetTempPath(),
                    Path.GetRandomFileName()
                );
                try {
                    Directory.CreateDirectory(dirName);
                    break;
                } catch {
                }
            }

            File.Copy("TestData\\applicationhostOriginal.config",
                Path.Combine(dirName, "applicationHost.config"));

            File.Copy(
                "wfastcgi.py",
                Path.Combine(dirName, "wfastcgi.py")
            );

            Directory.CreateDirectory(Path.Combine(dirName, "WebSite"));
            return dirName;
        }

        public static void ConfigureIIS(string appCmd, string appHostConfig, string python, string wfastcgi, Dictionary<string, string> envVars) {
            var psi = new ProcessStartInfo(
                appCmd,
                String.Format(
                    "set config /section:system.webServer/fastCGI " +
                    "\"/+[fullPath='{0}', arguments='{1}']\" \"/AppHostConfig:{2}\"",
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
                    "\"/+[name='Python_via_FastCGI',path='*',verb='*',modules='FastCgiModule',scriptProcessor='{0}|{1}',resourceType='Unspecified']\" " +
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
                        "/+\"[fullPath='{0}', arguments='{1}'].environmentVariables.[name='{2}',value='{3}']\" " +
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
                    { "PYTHONPATH", "" }
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
                return @"C:\Python27\python.exe";
            }
        }

        public virtual string AppCmdPath {
            get {
                return @"C:\Program Files (x86)\IIS Express\appcmd.exe";
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
                    "C:\\Program Files (x86)\\IIS Express\\iisexpress.exe",
                    String.Format(
                        "/config:{0} /systray:false",
                        Path.Combine(_dir, "applicationHost.config")
                    )
                );
                _process = StartProcess(psi);
                Console.WriteLine("Server started");
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
    }

}
