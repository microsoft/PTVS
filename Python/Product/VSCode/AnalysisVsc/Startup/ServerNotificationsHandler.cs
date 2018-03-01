// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.DsTools.Core.Disposables;
using Microsoft.DsTools.Core.Services;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.VsCode.Core.Shell;

namespace Microsoft.PythonTools.VsCode.Startup {
    public sealed class ServerNotificationsHandler: IDisposable {
        private readonly DisposableBag _bag = new DisposableBag(nameof(ServerNotificationsHandler));
        private readonly Server _server;
        private readonly IUIService _ui;
        private readonly ITelemetryService _telemetry;

        public ServerNotificationsHandler(Server server, IServiceContainer services) {
            _ui = services.GetService<IUIService>();
            _telemetry = services.GetService<ITelemetryService>();

            _server = server;

            _server.OnLogMessage += OnLogMessage;
            _server.OnShowMessage += OnShowMessage;
            _server.OnTelemetry += OnTelemetry;

            _bag
                .Add(() => _server.OnLogMessage -= OnLogMessage)
                .Add(() => _server.OnShowMessage -= OnShowMessage)
                .Add(() => _server.OnTelemetry -= OnTelemetry);
        }

        public void Dispose() => _bag.TryDispose();

        private void OnTelemetry(object sender, TelemetryEventArgs e) 
            => _telemetry.SendTelemetry(e.value);
        private void OnShowMessage(object sender, ShowMessageEventArgs e) 
            => _ui.ShowMessage(e.message, e.type);
        private void OnLogMessage(object sender, LogMessageEventArgs e) 
            => _ui.LogMessage(e.message, e.type);
    }
}
