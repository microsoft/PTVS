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

using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.ServiceModel;
using System.Xml.Linq;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    /// <summary>
    /// Exception used to wrap Azure Service Management errors before
    /// displaying them to users in PowerShell and use their text as the
    /// primary error message.
    /// </summary>
    [Serializable]
    public class AzureServiceManagementException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the AzureServiceManagementException.
        /// </summary>
        /// <param name="message">Azure error message.</param>
        /// <param name="innerException">The original exception.</param>
        private AzureServiceManagementException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Wrap an existing error record in one that will display the Azure
        /// service management error message.
        /// </summary>
        /// <param name="errorRecord">The existing error.</param>
        /// <returns>
        /// Either a new ErrorRecord that displays the Azure service management
        /// error message, or the existing error record if there was no Azure
        /// error to wrap.
        /// </returns>
        public static ErrorRecord WrapExistingError(ErrorRecord errorRecord)
        {
            Debug.Assert(errorRecord != null, "errorRecord cannot be null.");
            Debug.Assert(errorRecord.Exception != null, "errorRecord.Exception cannot be null.");

            // Try to pull the actual error message out of the error's
            // exception to give the users more information about what went
            // wrong
            CommunicationException exception = errorRecord.Exception as CommunicationException;
            if (exception != null)
            {
                WebException innerException = exception.InnerException as WebException;
                if (innerException != null)
                {
                    HttpWebResponse response = innerException.Response as HttpWebResponse;
                    if (response != null && (int)response.StatusCode >= 400)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            // Format the response XML with proper indentation
                            string responseText = reader.ReadToEnd();
                            string formattedXml = XDocument.Parse(responseText).ToString();

                            // Create a new error record that wraps the old
                            // error record
                            string exceptionMessage = string.Format(
                                Properties.Resources.AzureServiceManagementException_WrapExistingError_DetailedErrorFormat,
                                formattedXml);
                            errorRecord = new ErrorRecord(
                                new AzureServiceManagementException(exceptionMessage, exception),
                                errorRecord.FullyQualifiedErrorId,
                                errorRecord.CategoryInfo.Category,
                                errorRecord.TargetObject);
                        }
                    }
                }

            }

            return errorRecord;
        }

        // I'm leaving this code in because there was enough debate about how
        // to display the error message that we're going to revisit for the
        // next release.
        //
        /////// <summary>
        /////// Try to parse the document containing the Azure service management
        /////// error.
        /////// </summary>
        /////// <param name="doc">The document.</param>
        /////// <param name="code">The error code.</param>
        /////// <param name="message">The error message.</param>
        /////// <returns>
        /////// A value indicating if the document was successfully parsed.
        /////// </returns>
        ////private static bool TryParseErrorDocument(XDocument doc, out string code, out string message)
        ////{
        ////    Debug.Assert(doc != null, "doc cannot be null."
        ////    code = null;
        ////    message = null;
        ////
        ////    try
        ////    {
        ////        Func<string, string> getValue =
        ////            (string name) =>
        ////            {
        ////                XName elementName = XName.Get(name, WAPPSCmdlet.Constants.ServiceManagementNS);
        ////                XElement element = doc.Root.Element(elementName);
        ////                return element != null ?
        ////                    element.Value :
        ////                    null;
        ////            };
        ////        code = getValue("Code");
        ////        message = getValue("Message");
        ////    }
        ////    catch
        ////    {
        ////        // Ignore any parsing errors caused by response
        ////        // format changes so we'll just show the
        ////        // original error.
        ////        Debug.Fail("Azure Service Management error format has changed.");
        ////        return false;
        ////    }
        ////
        ////    return true;
        ////}
    }
}
