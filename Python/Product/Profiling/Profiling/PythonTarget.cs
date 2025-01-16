namespace Microsoft.PythonTools.Profiling {
    using System;
    using System.Threading.Tasks;
    using Microsoft.DiagnosticsHub.Targets;

    class PythonTarget : ITarget, IDisposable
    {

        private bool _isDisposed = false;
        public TargetProperties TargetProperties => throw new NotImplementedException();

        public SupportedTargetInterfaces SupportedInterfaces => throw new NotImplementedException();

        public event EventHandler<TargetExitedEventArgs> TargetExited;
        public event EventHandler TargetPropertiesChanged;

        public PythonTarget() { }

        public void Dispose()
        {
            if (this._isDisposed)
            {
                return;
            }
            this._isDisposed = true;
            TargetPropertiesChanged?.Invoke(this, EventArgs.Empty);
        }
        public string GetName()
        {
            return "placeholder python project";
        }

        public bool IsLaunched() => throw new NotImplementedException();
        public Task LaunchAsync(TargetLaunchArgs targetLaunchArgs)
        {
            return Task.CompletedTask;
        }
        public Task OnCollectionStartedAsync(CollectionStartedArgs collectionStartedArgs)
        {
            return Task.CompletedTask;
        }
        public Task<bool> SetupAsync(TargetSetupArgs targetSetupArgs)
        {
            return Task.FromResult<bool>(true);
        }
        public Task StopAsync(TargetStopArgs targetStopArgs)
        {
            TargetExited?.Invoke(this, new TargetExitedEventArgs(this, null));
            return Task.CompletedTask;
        }
    }
}
