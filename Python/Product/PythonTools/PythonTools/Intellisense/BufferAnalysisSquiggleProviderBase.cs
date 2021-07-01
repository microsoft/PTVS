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

using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Options;

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Common base for squiggles providers that operate on a text buffer
    /// </summary>
    abstract class BufferAnalysisSquiggleProviderBase<T> : IPythonTextBufferInfoEventSink
    {
        // Allows test cases to skip checking user options
        internal static bool _alwaysCreateSquiggle;

        private readonly Func<GeneralOptions, bool> _getSetting;
        private readonly PythonTextBufferInfoEvents[] _triggerEvents;

        protected TaskProvider TaskProvider { get; }
        protected PythonEditorServices Services { get; }
        protected bool Enabled { get; set; }

        public BufferAnalysisSquiggleProviderBase(
            IServiceProvider serviceProvider,
            TaskProvider taskProvider,
            Func<GeneralOptions, bool> getSetting,
            PythonTextBufferInfoEvents[] triggerEvents)
        {

            Services = serviceProvider.GetComponentModel().GetService<PythonEditorServices>();
            TaskProvider = taskProvider ?? throw new ArgumentNullException(nameof(taskProvider));

            _getSetting = getSetting;
            _triggerEvents = triggerEvents;

            var options = Services.Python?.GeneralOptions;
            if (options != null)
            {
                Enabled = _getSetting(options);
                options.Changed += GeneralOptions_Changed;
            }
        }

        private void GeneralOptions_Changed(object sender, EventArgs e)
        {
            if (sender is GeneralOptions options)
            {
                Enabled = _getSetting(options);
            }
        }

        public void AddBuffer(PythonTextBufferInfo buffer)
        {
            buffer.AddSink(typeof(T), this);
            if (buffer.AnalysisEntry?.IsAnalyzed == true)
            {
                OnNewAnalysis(buffer, buffer.AnalysisEntry)
                    .HandleAllExceptions(Services.Site, GetType())
                    .DoNotWait();
            }
        }

        public void RemoveBuffer(PythonTextBufferInfo buffer) => buffer.RemoveSink(typeof(T));

        async Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e)
        {
            if (_triggerEvents.Contains(e.Event))
            {
                await OnNewAnalysis(sender, e.AnalysisEntry);
            }
        }

        protected abstract Task OnNewAnalysis(PythonTextBufferInfo bi, AnalysisEntry entry);
    }
}
