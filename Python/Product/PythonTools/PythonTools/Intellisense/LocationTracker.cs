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

using System.Diagnostics;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Translates spans in a versioned response from the out of proc analysis.
    /// 
    /// Responses which need to operator on buffers (for editing, classification, etc...)
    /// will return a version which is what the results are based upon.  A SpanTranslator
    /// will translate from the version which is potentially old to the current
    /// version of the buffer.
    /// </summary>
    internal class LocationTracker {
        private readonly ITextVersion _fromVersion;
        private readonly ITextBuffer _buffer;

        /// <summary>
        /// Creates a new location tracker which can track spans and positions through time.
        /// 
        /// The tracker will translate positions from the specified version to the current
        /// snapshot in VS.  Requests can be made to track either forwards or backwards.
        /// </summary>
        public LocationTracker(ITextVersion lastAnalysisVersion, ITextBuffer buffer, int fromVersion) {
            // We always hold onto the last version that we've successfully analyzed, as that's
            // the last event the out of proc analyzer will send us.  Once we've received
            // that event all future information should come from at least that version.  This
            // prevents us from holding onto every version in the world.
            Debug.Assert(fromVersion >= lastAnalysisVersion.VersionNumber, "fromVersion {0} was less than lastAnalysisVersion {1}".FormatInvariant(fromVersion, lastAnalysisVersion.VersionNumber));

            while (lastAnalysisVersion.Next != null && lastAnalysisVersion.VersionNumber != fromVersion) {
                lastAnalysisVersion = lastAnalysisVersion.Next;
            }

            _fromVersion = lastAnalysisVersion;
            _buffer = buffer;
        }

        public ITextBuffer TextBuffer {
            get {
                return _buffer;
            }
        }

        /// <summary>
        /// Translates the specified span forward in time from the out of proc analysis
        /// to the current snapshot in use in VS.
        /// </summary>
        public SnapshotSpan TranslateForward(Span from) {
            return new SnapshotSpan(
                _buffer.CurrentSnapshot,
                Tracking.TrackSpanForwardInTime(
                    SpanTrackingMode.EdgeInclusive,
                    new Span(from.Start, from.Length),
                    _fromVersion,
                    _buffer.CurrentSnapshot.Version
                )
            );
        }

        /// <summary>
        /// Translates the specified position forward in time from a position in the out of proc
        /// analysis to the current snapshot in use in VS.
        /// </summary>
        public SnapshotPoint TranslateForward(int position) {
            return new SnapshotPoint(
                _buffer.CurrentSnapshot,
                Tracking.TrackPositionForwardInTime(
                    PointTrackingMode.Positive,
                    position,
                    _fromVersion,
                    _buffer.CurrentSnapshot.Version
                )
            );
        }

        /// <summary>
        /// Translates a position back in time from the current position inside of VS to a 
        /// version from the out of proc analysis version.
        /// </summary>
        public int TranslateBack(int position) {
            return Tracking.TrackPositionBackwardInTime(
                PointTrackingMode.Positive,
                position,
                _buffer.CurrentSnapshot.Version,
                _fromVersion
            );
        }
    }

}
