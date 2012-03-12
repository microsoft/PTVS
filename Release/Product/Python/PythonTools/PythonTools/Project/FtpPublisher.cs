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
using System.ComponentModel.Composition;
using System.IO;
using System.Net;

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
                throw new IOException(String.Format("Failed to upload file: {0}", response.StatusDescription));
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

            var response = (FtpWebResponse)request.GetResponse();
            if (response.StatusCode != FtpStatusCode.PathnameCreated) {
                throw new IOException(String.Format("Failed to create directory: {0}", response.StatusDescription));
            }
        }

        private static bool FtpDirExists(string dirLoc) {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(dirLoc));
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            try {
                var response = (FtpWebResponse)request.GetResponse();
                if (response.StatusCode != FtpStatusCode.DataAlreadyOpen) {
                    throw new IOException(String.Format("Failed to check if directory exists: {0}", response.StatusDescription));
                }
                return true;
            } catch (WebException) {
                return false;
            }

        }

        public string DestinationDescription {
            get { return "ftp server"; }
        }

        public string Schema {
            get { return "ftp"; }
        }

        #endregion
    }
}
