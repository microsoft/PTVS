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
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Editor {
    /// <summary>
    /// Interface for classes that need to receive events from text buffers.
    /// </summary>
    /// <remarks>
    /// This is an interface rather than an abstract class because there are
    /// IntelliSense features that require using another base class.
    /// Implement no-op handlers by returning <see cref="Task.CompletedTask"/>.
    /// </remarks>
    internal interface IPythonTextBufferInfoEventSink {
        Task PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e);
    }

    internal enum PythonTextBufferInfoEvents {
        None = 0,
        NewAnalysisEntry,
        NewParseTree,
        NewAnalysis,
        TextContentChanged,
        TextContentChangedLowPriority,
        ContentTypeChanged,
        DocumentEncodingChanged,
        NewTextBufferInfo,
        TextContentChangedOnBackgroundThread,
        AnalyzerExpired,
    }

    internal class PythonTextBufferInfoEventArgs : EventArgs {
        public PythonTextBufferInfoEventArgs(PythonTextBufferInfoEvents eventType) {
            Event = eventType;
        }

        public PythonTextBufferInfoEvents Event { get; }
    }

    internal class PythonTextBufferInfoNestedEventArgs : PythonTextBufferInfoEventArgs {
        public PythonTextBufferInfoNestedEventArgs(PythonTextBufferInfoEvents eventType, EventArgs e)
            : base(eventType) {
            NestedEventArgs = e;
        }

        public EventArgs NestedEventArgs { get; }
    }

    internal class PythonNewTextBufferInfoEventArgs : PythonTextBufferInfoEventArgs {
        public PythonNewTextBufferInfoEventArgs(PythonTextBufferInfoEvents eventType, PythonTextBufferInfo newInfo)
            : base(eventType) {
            NewTextBufferInfo = newInfo;
        }

        public PythonTextBufferInfo NewTextBufferInfo { get; }
    }
}
