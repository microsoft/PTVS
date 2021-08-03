using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LogHub;
using Microsoft.VisualStudio.RpcContracts.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace Microsoft.PythonTools.Logging {
    [Export(typeof(IPythonToolsLogger))]
    class LogHubLogger : IPythonToolsLogger {
        
        private TraceSource _traceSource;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

        /// <summary>
        /// See directions here on how to log to the log hub: https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/23437/Using-LogHub
        /// </summary>
        /// <param name="asyncServiceProvider"></param>
        [ImportingConstructor]
        public LogHubLogger([Import(typeof(SAsyncServiceProvider))] IAsyncServiceProvider2 asyncServiceProvider) {
            asyncServiceProvider.GetServiceAsync<SVsBrokeredServiceContainer, IBrokeredServiceContainer>(throwOnFailure: true).ContinueWith(async (t) => {
                var serviceBroker = t.Result.GetFullAccessServiceBroker();
                Assumes.NotNull(serviceBroker);

                // Setup logging using the log hub
                using TraceConfiguration config = await TraceConfiguration.CreateTraceConfigurationInstanceAsync(serviceBroker, false);
                SourceLevels sourceLevels = SourceLevels.Information | SourceLevels.ActivityTracing;
                LoggerOptions logOptions = new(
                    requestedLoggingLevel: new LoggingLevelSettings(sourceLevels),
                    privacySetting: PrivacyFlags.MayContainPersonallyIdentifibleInformation | PrivacyFlags.MayContainPrivateInformation);

                this._traceSource = await config.RegisterLogSourceAsync(new LogId("Microsoft.PythonTools", serviceId: null), logOptions, traceSource: null, isBootstrappedService: true, default);
            });
        }

        public void LogEvent(PythonLogEvent logEvent, object argument) {
            if (_traceSource != null) {
                _traceSource.TraceEvent(TraceEventType.Information, (int)TraceEventType.Information, $"{Enum.GetName(typeof(PythonLogEvent), logEvent)}:{argument?.ToString()}");
            }
        }
        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> properties, IReadOnlyDictionary<string, double> measurements) {
        }
        public void LogFault(Exception ex, string description, bool dumpProcess) {
            if (_traceSource != null) {
                _traceSource.TraceEvent(TraceEventType.Error, (int)TraceEventType.Error, ex.Message);
            }
        }
    }
}
