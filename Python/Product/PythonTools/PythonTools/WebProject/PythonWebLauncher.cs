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
using System.Net;
using System.Threading.Tasks;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project.Web {
    /// <summary>
    /// Web launcher.  This wraps the default launcher and provides it with a
    /// different IPythonProject which launches manage.py with the appropriate
    /// options.  Upon a successful launch we will then automatically load the
    /// appropriate page into the users web browser.
    /// </summary>
    class PythonWebLauncher : IProjectLauncher {
        public const string RunWebServerCommand = "PythonRunWebServerCommand";
        public const string DebugWebServerCommand = "PythonDebugWebServerCommand";

        public const string RunWebServerTargetProperty = "PythonRunWebServerCommand";
        public const string RunWebServerTargetTypeProperty = "PythonRunWebServerCommandType";
        public const string RunWebServerArgumentsProperty = "PythonRunWebServerCommandArguments";
        public const string RunWebServerEnvironmentProperty = "PythonRunWebServerCommandEnvironment";

        public const string DebugWebServerTargetProperty = "PythonDebugWebServerCommand";
        public const string DebugWebServerTargetTypeProperty = "PythonDebugWebServerCommandType";
        public const string DebugWebServerArgumentsProperty = "PythonDebugWebServerCommandArguments";
        public const string DebugWebServerEnvironmentProperty = "PythonDebugWebServerCommandEnvironment";

        private readonly IServiceProvider _serviceProvider;
        private readonly PythonToolsService _pyService;
        private readonly LaunchConfiguration _runConfig, _debugConfig, _defaultConfig;

        public PythonWebLauncher(
            IServiceProvider serviceProvider,
            LaunchConfiguration runConfig,
            LaunchConfiguration debugConfig,
            LaunchConfiguration defaultConfig
        ) {
            _serviceProvider = serviceProvider;
            _pyService = _serviceProvider.GetPythonToolsService();
            _runConfig = runConfig;
            _debugConfig = debugConfig;
            _defaultConfig = defaultConfig;
        }

        #region IPythonLauncher Members

        private static bool IsDebugging(IServiceProvider provider, IVsDebugger debugger) {
            return provider.GetUIThread().Invoke(() => {
                var mode = new[] { DBGMODE.DBGMODE_Design };
                return ErrorHandler.Succeeded(debugger.GetMode(mode)) && mode[0] != DBGMODE.DBGMODE_Design;
            });
        }

        public int LaunchProject(bool debug) {
            var config = debug ? _debugConfig : _runConfig;

            DebugLaunchHelper.RequireStartupFile(config);

            Uri url;
            int port;
            GetFullUrl(_serviceProvider, config, out url, out port);

            var env = new Dictionary<string, string> { { "SERVER_PORT", port.ToString() } };
            if (url != null) {
                env["SERVER_HOST"] = url.Host;
            }

            config.Environment = PathUtils.MergeEnvironments(env, config.Environment);

            try {
                _serviceProvider.GetPythonToolsService().Logger?.LogEvent(Logging.PythonLogEvent.Launch, new Logging.LaunchInfo {
                    IsDebug = debug,
                    IsWeb = true,
                    Version = config.Interpreter?.Version.ToString() ?? ""
                });
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            if (debug) {
                if (url != null) {
                    config.LaunchOptions[PythonConstants.WebBrowserUrlSetting] = url.AbsoluteUri;
                }
                using (var dsi = DebugLaunchHelper.CreateDebugTargetInfo(_serviceProvider, config)) {
                    dsi.Launch();
                }
            } else {
                var psi = DebugLaunchHelper.CreateProcessStartInfo(_serviceProvider, config);

                var process = Process.Start(psi);
                if (url != null && process != null) {
                    StartBrowser(url.AbsoluteUri, () => process.HasExited)
                        .ContinueWith(t => { process.Close(); })
                        .HandleAllExceptions(_serviceProvider, GetType())
                        .DoNotWait();
                }
            }

            return VSConstants.S_OK;
        }

        public int LaunchFile(string file, bool debug) {
            return new DefaultPythonLauncher(_serviceProvider, _defaultConfig).LaunchFile(file, debug);
        }


        private Task StartBrowser(string url, Func<bool> shortCircuitPredicate) {
            Uri uri;
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri)) {
                var tcs = new TaskCompletionSource<object>();

                OnPortOpenedHandler.CreateHandler(
                    uri.Port,
                    shortCircuitPredicate: shortCircuitPredicate,
                    action: () => {
                        try {
                            var web = _serviceProvider.GetService(typeof(SVsWebBrowsingService)) as IVsWebBrowsingService;
                            if (web == null) {
                                CommonPackage.OpenWebBrowser(_serviceProvider, url);
                                return;
                            }

                            ErrorHandler.ThrowOnFailure(
                                web.CreateExternalWebBrowser(
                                    (uint)__VSCREATEWEBBROWSER.VSCWB_ForceNew,
                                    VSPREVIEWRESOLUTION.PR_Default,
                                    url
                                )
                            );
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                            tcs.SetException(ex);
                        } finally {
                            tcs.TrySetResult(null);
                        }
                    }
                );

                return tcs.Task;
            }

            return Task.FromResult<object>(null);
        }


        #endregion

        internal static void GetFullUrl(IServiceProvider provider, LaunchConfiguration config, out Uri uri, out int port) {
            int p;
            if (int.TryParse(config.GetLaunchOption(PythonConstants.WebBrowserPortSetting) ?? "", out p)) {
                port = p;
            } else {
                SocketUtils.GetRandomPortListener(IPAddress.Loopback, out port).Stop();
                p = -1;
            }

            var host = config.GetLaunchOption(PythonConstants.WebBrowserUrlSetting);
            if (string.IsNullOrEmpty(host)) {
                uri = null;
                return;
            }

            try {
                UriBuilder builder;
                if (Uri.TryCreate(host, UriKind.Absolute, out uri)) {
                    builder = new UriBuilder(uri);
                } else {
                    builder = new UriBuilder();
                    builder.Scheme = Uri.UriSchemeHttp;
                    builder.Host = "localhost";
                    builder.Path = host;
                }

                if (p >= 0) {
                    builder.Port = p;
                } else if (builder.Port < 0 || (builder.Uri.IsDefaultPort && !host.Contains(":{0}".FormatInvariant(builder.Port)))) {
                    SocketUtils.GetRandomPortListener(IPAddress.Loopback, out port).Stop();
                    builder.Port = port;
                }

                uri = builder.Uri;
                port = uri.Port;
            } catch (UriFormatException) {
                if (provider == null) {
                    throw;
                }
                var output = OutputWindowRedirector.GetGeneral(provider);
                output.WriteErrorLine(Strings.ErrorInvalidLaunchUrl.FormatUI(host));
                output.ShowAndActivate();
                uri = null;
            }
        }
    }
}
