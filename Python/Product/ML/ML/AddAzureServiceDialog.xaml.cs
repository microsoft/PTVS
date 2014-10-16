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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Data.Edm;
using Microsoft.Data.Edm.Library;
using LocalizedResources = Microsoft.PythonTools.Resources.Resources;

namespace Microsoft.PythonTools.ML {
    /// <summary>
    /// Interaction logic for AddAzureServiceDialog.xaml
    /// </summary>
    partial class AddAzureServiceDialog : DialogWindowVersioningWorkaround {
        internal ServiceInfoData ServiceInfo;

        public AddAzureServiceDialog() {
            InitializeComponent();
            AddToCombo.SelectedIndex = 0;
            UpdateAddToCombo();
        }

        public AddAzureServiceDialog(string defaultTargetFolder, bool supportsViews)
            : this() {
            if (defaultTargetFolder != null) {
                InputTargetFolder.Text = defaultTargetFolder;
                DashboardTargetFolder.Text = defaultTargetFolder;
            }
            AddDashboardDisplayPanel.Visibility = AddInputFormPanel.Visibility =
                supportsViews ? System.Windows.Visibility.Visible : Visibility.Collapsed;
        }


        /// <summary>
        /// Adds a file to the list of files the user can generate the service code into.
        /// </summary>
        /// <param name="file"></param>
        public void AddTargetFile(string file) {
            AddToCombo.Items.Add(new ComboBoxItem() { Content = file });
        }

        private async void OkButtonClick(object sender, RoutedEventArgs e) {
            try {
                var oauthEndpoint = UrlToODataUrl(AzureURL.Text);

                var req = WebRequest.Create(oauthEndpoint);
                req.ContentType = "application/json";
                var response = await req.GetResponseAsync();
                var responseStream = response.GetResponseStream();
                var reader = new StreamReader(responseStream);
                var content = await reader.ReadToEndAsync();

                var serializer = new JavaScriptSerializer();
                var odataInfo = serializer.Deserialize<Dictionary<string, object>>(content);
                object value;
                if (odataInfo.TryGetValue("odata.metadata", out value) &&
                    (value is string)) {

                    req = WebRequest.Create((string)value);
                    response = await req.GetResponseAsync();
                    responseStream = response.GetResponseStream();

                    var doc = new XPathDocument(responseStream);
                    XmlNamespaceManager mngr = new XmlNamespaceManager(new NameTable(
                        ));
                    mngr.AddNamespace("edmx", "http://schemas.microsoft.com/ado/2009/11/edmx");
                    mngr.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
                    mngr.AddNamespace("edm", "http://schemas.microsoft.com/ado/2009/11/edm");

                    var nav = doc.CreateNavigator();

                    ServiceInfo = SelectNodes(mngr, nav);

                    DialogResult = true;
                    Close();
                }
            } catch (WebException wex) {
                MessageBox.Show(String.Format(LocalizedResources.MetadataDownloadFailed, wex.Message), "Python Azure ML", MessageBoxButton.OK, MessageBoxImage.Error);
            } catch (UriFormatException ufe) {
                MessageBox.Show(String.Format(LocalizedResources.AzureMLServiceInvalid, ufe.Message), "Python Azure ML", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private static ServiceInfoData SelectNodes(XmlNamespaceManager mngr, XPathNavigator nav) {
            var serviceInfo = new ServiceInfoData();

            var submitParams = nav.Select("/edmx:Edmx/edmx:DataServices/edm:Schema/edm:EntityContainer[@Name='Default']/edm:FunctionImport/edm:Parameter", mngr);
            Console.WriteLine(submitParams);
            foreach (XPathNavigator param in submitParams) {
                var name = param.GetAttribute("Name", "");
                var type = param.GetAttribute("Type", "");
                var nullable = param.GetAttribute("Nullable", "");
                serviceInfo.Inputs.Add(new KeyValuePair<string, EdmPrimitiveTypeKind>(name, ParseEdmType(type)));
            }

            var resultScore = nav.Select("/edmx:Edmx/edmx:DataServices/edm:Schema/edm:ComplexType[@Name='ScoreResult']/edm:Property", mngr);
            foreach (XPathNavigator param in resultScore) {
                var name = param.GetAttribute("Name", "");
                var type = param.GetAttribute("Type", "");
                var nullable = param.GetAttribute("Nullable", "");
                serviceInfo.Outputs.Add(new KeyValuePair<string, EdmPrimitiveTypeKind>(name, ParseEdmType(type)));
            }

            return serviceInfo;
        }

        private static EdmPrimitiveTypeKind ParseEdmType(string type) {
            switch (type) {
                case "Edm.String":
                    return EdmPrimitiveTypeKind.String;
                case "Edm.Double":
                    return EdmPrimitiveTypeKind.Double;
                case "Edm.Int32":
                    return EdmPrimitiveTypeKind.Int32;
                case "Edm.Boolean":
                    return EdmPrimitiveTypeKind.Boolean;
                default:
                    return EdmPrimitiveTypeKind.None;
            }
        }
        private static string GetDefaultValue(EdmPrimitiveTypeKind type) {
            switch (type) {
                case EdmPrimitiveTypeKind.String:
                    return "'0'";
                case EdmPrimitiveTypeKind.Double:
                    return "0.0";
                case EdmPrimitiveTypeKind.Int32:
                    return "0";
                case EdmPrimitiveTypeKind.Boolean:
                    return "False";
                default:
                    throw new InvalidOperationException();
            }
        }

        internal class ServiceInfoData {
            public readonly List<KeyValuePair<string, EdmPrimitiveTypeKind>> Inputs = new List<KeyValuePair<string, EdmPrimitiveTypeKind>>();
            public readonly List<KeyValuePair<string, EdmPrimitiveTypeKind>> Outputs = new List<KeyValuePair<string, EdmPrimitiveTypeKind>>();
        }

        // TODO: "Parse(url, out server, out wsid, out sid)" and "MakeODataUrl(server, wsid, sid)" 
        /// <summary>
        /// We accept URLs in one of 3 formats depending on where the user happens to grab them from.
        /// 
        /// https://server/workspaces/wsid/services/sid/score
        /// https://server/odata/workspaces/wsid/services/sid
        /// https://server/workspaces/wsid/services/sid/score/help
        /// 
        /// This function turns the URL into the OData URL.
        /// </summary>
        private static string UrlToODataUrl(string url) {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) {
                throw new UriFormatException(url);
            }
            if (uri.AbsolutePath.StartsWith("/odata")) {
                return url;
            }
            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/score")) {
                // remove score, add odata
                string authority = uri.GetLeftPart(UriPartial.Authority);
                var path = "/odata" + uri.PathAndQuery.Substring(uri.PathAndQuery.LastIndexOf("/score"));
                return authority + path + uri.Fragment;
            }
            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/score/help")) {
                // remove /score/help, add odata
                string authority = uri.GetLeftPart(UriPartial.Authority);
                var path = "/odata" + uri.PathAndQuery.Substring(uri.PathAndQuery.LastIndexOf("/score/help"));
                return authority + path + uri.Fragment;
            }

            return url;
        }

        /// <summary>
        /// We accept URLs in one of 3 formats depending on where the user happens to grab them from.
        /// 
        /// https://server/workspaces/wsid/services/sid/score
        /// https://server/odata/workspaces/wsid/services/sid
        /// https://server/workspaces/wsid/services/sid/score/help
        /// 
        /// This function turns the URL into the /score URL.
        /// </summary>
        private static string UrlToServiceUrl(string url) {
            Uri uri;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) {
                throw new UriFormatException(url);
            }

            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/score")) {
                return url;
            }
            if (uri.AbsolutePath.StartsWith("/odata")) {
                // remove odata, add /score
                string authority = uri.GetLeftPart(UriPartial.Authority);
                var path = uri.PathAndQuery;
                Debug.Assert(path.StartsWith("/odata"));

                url = authority + path.Substring("/odata".Length);
                if (!url.EndsWith("/")) {
                    url += "/";
                }
                url += "score";
                url += uri.Fragment;

                return url;
            }
            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/score/help")) {
                // remove /score/help
                return url.Substring(0, url.LastIndexOf("/score/help"));
            }

            return url;
        }

        public string GenerateServiceCode() {
            List<string> parameters = new List<string>();
            List<string> dictCreation = new List<string>();
            foreach (var inputName in ServiceInfo.Inputs) {
                parameters.Add(
                    String.Format(
                        "{0} = {1}",
                        inputName.Key,
                        GetDefaultValue(inputName.Value)
                    )
                );
                dictCreation.Add(String.Format("                '{0}' : {0}", inputName.Key));
            }

            // TODO: Insert imports at top, code at bottom.
            return String.Format(
                ReadResource("service_code.py"),
                String.Join(", ", parameters),
                String.Join(",\r\n", dictCreation),
                UrlToServiceUrl(AzureURL.Text),
                ApiKey.Text,
                ServiceName.Text,
                String.Join(", ", ServiceInfo.Outputs.Select(x => "'" + x.Key + "'"))
            );
        }

        public string GenerateBottleDashboardRoute(string importName) {
            List<string> parameters = new List<string>();
            foreach (var inputName in ServiceInfo.Inputs) {
                parameters.Add(
                    String.Format(
                        "        {0} = {1}",
                        inputName.Key,
                        GetDefaultValue(inputName.Value)
                    )
                );
            }

            return String.Format(
                ReadResource("bottle_dashboard_route.py"),
                ServiceName.Text,
                importName,
                String.Join(",\r\n", parameters)
            );
        }

        public string GenerateBottleDashboardTemplate() {
            List<string> results = new List<string>();
            foreach (var inputName in ServiceInfo.Outputs) {
                results.Add(
                    String.Format(
                        "        <tr><td>{0}</td><td>{{{{ score.{0} }}}}</td></tr>",
                        inputName.Key
                    )
                );
            }

            return String.Format(
                ReadResource("bottle_dashboard_template.tpl"),
                String.Join("\r\n", results)
            );
        }

        public string GenerateBottleFormRoute(string importName) {
            List<string> parameters = new List<string>();
            foreach (var inputName in ServiceInfo.Inputs) {
                if (inputName.Value == EdmPrimitiveTypeKind.Boolean) {
                    parameters.Add(
                        String.Format(
                            "        {0} = '{0}' in request.forms",
                            inputName.Key
                        )
                    );
                } else {
                    parameters.Add(
                        String.Format(
                            "        {0} = request.forms['{0}']",
                            inputName.Key
                        )
                    );
                }
            }

            return String.Format(
                ReadResource("bottle_form_route.py"),
                ServiceName.Text,
                importName,
                String.Join(",\r\n", parameters),
                ServiceName.Text.Replace('_', ' ')
            );
        }

        private static string ReadResource(string name) {
            return new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.PythonTools.ML." + name)).ReadToEnd();
        }

        public string GenerateBottleFormTemplate() {
            List<string> parameters = new List<string>();
            foreach (var inputName in ServiceInfo.Inputs) {
                parameters.Add(
                    String.Format(
                        "        <tr><td>{0}</td><td><input type=\"{1}\" name=\"{0}\"/></td></tr>",
                        inputName.Key,
                        GetInputHtmlType(inputName.Value)
                    )
                );
            }

            return String.Format(
                ReadResource("bottle_form_template.tpl"),
                String.Join("\r\n", parameters)
            );
        }

        private string GetInputHtmlType(EdmPrimitiveTypeKind kind) {
            switch (kind) {
                case EdmPrimitiveTypeKind.Boolean:
                    return "checkbox";
                case EdmPrimitiveTypeKind.Int32:
                case EdmPrimitiveTypeKind.Double:
                    return "number";
                default:
                    return "text";
            }
        }

        private void ServiceName_TextChanged(object sender, TextChangedEventArgs e) {
            UpdateAddToCombo();
        }

        private void UpdateAddToCombo() {
            if (AddToCombo != null && AddToCombo.Items.Count > 0) {
                var item = (ComboBoxItem)AddToCombo.Items[0];
                item.Content = String.Format(LocalizedResources.NewFileFormat, ServiceName.Text);
            }
        }

        private void AddDashboardDisplay_Checked(object sender, RoutedEventArgs e) {
            DashboardTargetPanel.IsEnabled = AddDashboardDisplay.IsChecked.Value;
        }

        private void AddInputForm_Checked(object sender, RoutedEventArgs e) {
            InputTargetPanel.IsEnabled = AddInputForm.IsChecked.Value;
        }
    }
}
