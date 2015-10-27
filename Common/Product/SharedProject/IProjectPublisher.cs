// Visual Studio Shared Project
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

using System;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Implements a publisher which handles publishing the list of files to a destination.
    /// </summary>
    public interface IProjectPublisher {
        /// <summary>
        /// Publishes the files listed in the given project to the provided URI.
        /// 
        /// This function should return when publishing is complete or throw an exception if publishing fails.
        /// </summary>
        /// <param name="project">The project to be published.</param>
        /// <param name="destination">The destination URI for the project.</param>
        void PublishFiles(IPublishProject project, Uri destination);

        /// <summary>
        /// Gets a localized description of the destination type (web site, file share, etc...)
        /// </summary>
        string DestinationDescription {
            get;
        }

        /// <summary>
        /// Gets the schema supported by this publisher - used to select which publisher will
        /// be used based upon the schema of the Uri provided by the user.
        /// </summary>
        string Schema {
            get;
        }
    }
}
