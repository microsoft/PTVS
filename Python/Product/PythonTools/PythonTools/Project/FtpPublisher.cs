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
using System.ComponentModel.Composition;
using System.IO;
using System.Net;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Publishes files to a ftp server
    /// </summary>
    [Export(typeof(IProjectPublisher))]
    class FtpPublisher : IProjectPublisher {
        #region IProjectPublisher Members

        public void PublishFiles(IPublishProject project, Uri destination) {
            var files = project.Files;

            for (int i = 0; i < files.Count; i++) {
                var item = files[i];

                // try copying without impersonating first...
                CopyOneFile(destination, item);

                project.Progress = (int)(((double)i / (double)files.Count) * 100);
            }
        }

        private static void CopyOneFile(Uri destination, IPublishFile item) {
            var destFile = item.DestinationFile;

            // get the destination file URI, the root path, and the destination directory
            string newLoc = "ftp://";
            if (!String.IsNullOrEmpty(destination.UserInfo)) {
                newLoc += destination.UserInfo + "@";
            }

            newLoc += destination.Host;
            string rootPath = newLoc + "/";
            newLoc += destination.AbsolutePath + "/" + destFile.Replace('\\', '/') + destination.Query;

            string destinationDir = Path.GetDirectoryName(Path.Combine(destination.AbsolutePath.Replace('/', '\\'), destFile));

            EnsureDirectoryExists(rootPath, destinationDir);

            // upload the file
            ServicePointManager.CheckCertificateRevocationList = true;
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(newLoc));
            request.Method = WebRequestMethods.Ftp.UploadFile;
            byte[] buffer = new byte[1024];
            FileStream stream = new FileStream(item.SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            int bytesRead;
            request.ContentLength = stream.Length;
            var reqStream = request.GetRequestStream();
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                reqStream.Write(buffer, 0, bytesRead);
            }

            reqStream.Close();

            var response = (FtpWebResponse)request.GetResponse();
            if (response.StatusCode != FtpStatusCode.ClosingData) {
                throw new IOException(Strings.FtpPublisherUploadFileException.FormatUI(response.StatusDescription));
            }
        }

        private static void EnsureDirectoryExists(string rootPath, string destinationDir) {
            List<string> dirs = new List<string>();
            for (string dirName = destinationDir; !String.IsNullOrEmpty(Path.GetFileName(dirName)); dirName = Path.GetDirectoryName(dirName)) {
                dirs.Add(Path.GetFileName(dirName));
            }
            string curDir = "";
            for (int i = dirs.Count - 1; i >= 0; i--) {
                curDir = Path.Combine(curDir, dirs[i]);

                string ftpDir = curDir.Replace('\\', '/');
                string dirLoc = rootPath + ftpDir.Replace('\\', '/');
                if (!FtpDirExists(dirLoc)) {
                    FtpCreateDir(dirLoc);
                }
            }
        }

        private static void FtpCreateDir(string dirLoc) {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(dirLoc));
            request.Method = WebRequestMethods.Ftp.MakeDirectory;

            using (var response = (FtpWebResponse)request.GetResponse()) {
                if (response.StatusCode != FtpStatusCode.PathnameCreated) {
                    throw new IOException(Strings.FtpPublisherDirCreateException.FormatUI(response.StatusDescription));
                }
            }
        }

        private static bool FtpDirExists(string dirLoc) {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(dirLoc + "/"));
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            try {
                using (var response = (FtpWebResponse)request.GetResponse()) {
                    if (response.StatusCode != FtpStatusCode.DataAlreadyOpen) {
                        throw new IOException(Strings.FtpPublisherDirExistsCheckException.FormatUI(response.StatusDescription));
                    }
                    return true;
                }
            } catch (WebException) {
                return false;
            }

        }

        public string DestinationDescription => Strings.FtpPublisherDestinationDescription;

        public string Schema => "ftp";

        #endregion
    }
}
