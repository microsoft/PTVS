// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    using System;
    using System.Data.Services.Client;
    using System.Globalization;
    using System.Management.Automation;
    using System.Xml.Linq;

    public static class CmdletExtensions
    {
        public static string ResolvePath(this PSCmdlet cmdlet, string path)
        {
            var result = cmdlet.SessionState.Path.GetResolvedPSPathFromPSPath(path);
            string fullPath = string.Empty;

            if (result != null && result.Count > 0)
            {
                fullPath = result[0].Path;
            }

            return fullPath;
        }

        public static void WriteVerbose(this PSCmdlet cmdlet, string format, params object[] args)
        {
            var text = string.Format(CultureInfo.InvariantCulture, format, args);
            cmdlet.WriteVerbose(text);
        }

        public static Exception ProcessExceptionDetails(this PSCmdlet cmdlet, Exception exception)
        {
            if ((exception is DataServiceQueryException) && (exception.InnerException != null))
            {
                var dscException = FindDataServiceClientException(exception.InnerException);

                if (dscException == null)
                {
                    return new InnerDataServiceException(exception.InnerException.Message);
                }
                else
                {
                    var message = dscException.Message;
                    try
                    {
                        XNamespace ns = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
                        XDocument doc = XDocument.Parse(message);
                        return new InnerDataServiceException(doc.Root.Element(ns + "message").Value);
                    }
                    catch
                    {
                        return new InnerDataServiceException(message);
                    }
                }
            }

            return exception;
        }

        private static Exception FindDataServiceClientException(Exception ex)
        {
            if (ex is DataServiceClientException)
            {
                return ex;
            }
            else if (ex.InnerException != null)
            {
                return FindDataServiceClientException(ex.InnerException);
            }

            return null;
        }
    }
}