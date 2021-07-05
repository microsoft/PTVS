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

namespace Microsoft.PythonTools.CodeCoverage {
    class CoverageExporter {
        private readonly Dictionary<CoverageFileInfo, CoverageMapper> _covInfo;
        private readonly XmlWriter _writer;
        private int _methodCount, _curFile;
        private const string _schema = "<xs:schema id=\"CoverageDSPriv\" xmlns=\"\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\" xmlns:msprop=\"urn:schemas-microsoft-com:xml-msprop\"><xs:element name=\"CoverageDSPriv\" msdata:IsDataSet=\"true\" msdata:UseCurrentLocale=\"true\" msdata:EnforceConstraints=\"False\" msprop:Version=\"8.00\"><xs:complexType><xs:choice minOccurs=\"0\" maxOccurs=\"unbounded\"><xs:element name=\"Module\"><xs:complexType><xs:sequence><xs:element name=\"ModuleName\" type=\"xs:string\" /><xs:element name=\"ImageSize\" type=\"xs:unsignedInt\" /><xs:element name=\"ImageLinkTime\" type=\"xs:unsignedInt\" /><xs:element name=\"LinesCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesPartiallyCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"BlocksCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"BlocksNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"NamespaceTable\" minOccurs=\"0\" maxOccurs=\"unbounded\"><xs:complexType><xs:sequence><xs:element name=\"BlocksCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"BlocksNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesPartiallyCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"ModuleName\" type=\"xs:string\" minOccurs=\"0\" /><xs:element name=\"NamespaceKeyName\" type=\"xs:string\" /><xs:element name=\"NamespaceName\" type=\"xs:string\" minOccurs=\"0\" /><xs:element name=\"Class\" minOccurs=\"0\" maxOccurs=\"unbounded\"><xs:complexType><xs:sequence><xs:element name=\"ClassKeyName\" type=\"xs:string\" /><xs:element name=\"ClassName\" type=\"xs:string\" /><xs:element name=\"LinesCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"LinesPartiallyCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"BlocksCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"BlocksNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" /><xs:element name=\"NamespaceKeyName\" type=\"xs:string\" minOccurs=\"0\" /><xs:element name=\"Method\" minOccurs=\"0\" maxOccurs=\"unbounded\"><xs:complexType><xs:sequence><xs:element name=\"MethodKeyName\" type=\"xs:string\" msdata:Ordinal=\"0\" /><xs:element name=\"MethodName\" type=\"xs:string\" msdata:Ordinal=\"1\" /><xs:element name=\"MethodFullName\" type=\"xs:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" /><xs:element name=\"LinesCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" msdata:Ordinal=\"3\" /><xs:element name=\"LinesPartiallyCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" msdata:Ordinal=\"4\" /><xs:element name=\"LinesNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" msdata:Ordinal=\"5\" /><xs:element name=\"BlocksCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" msdata:Ordinal=\"6\" /><xs:element name=\"BlocksNotCovered\" type=\"xs:unsignedInt\" minOccurs=\"0\" msdata:Ordinal=\"7\" /><xs:element name=\"Lines\" msdata:CaseSensitive=\"False\" minOccurs=\"0\" maxOccurs=\"unbounded\"><xs:complexType><xs:sequence><xs:element name=\"LnStart\" type=\"xs:unsignedInt\" msdata:Ordinal=\"0\" /><xs:element name=\"ColStart\" type=\"xs:unsignedInt\" msdata:Ordinal=\"1\" /><xs:element name=\"LnEnd\" type=\"xs:unsignedInt\" msdata:Ordinal=\"2\" /><xs:element name=\"ColEnd\" type=\"xs:unsignedInt\" msdata:Ordinal=\"3\" /><xs:element name=\"Coverage\" type=\"xs:unsignedInt\" msdata:Ordinal=\"4\" /><xs:element name=\"SourceFileID\" type=\"xs:unsignedInt\" msdata:Ordinal=\"5\" /><xs:element name=\"LineID\" type=\"xs:unsignedInt\" msdata:Ordinal=\"7\" /></xs:sequence><xs:attribute name=\"MethodKeyName\" type=\"xs:string\" use=\"prohibited\" /></xs:complexType></xs:element></xs:sequence><xs:attribute name=\"ClassKeyName\" type=\"xs:string\" use=\"prohibited\" /></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element><xs:element name=\"SourceFileNames\"><xs:complexType><xs:sequence><xs:element name=\"SourceFileID\" type=\"xs:unsignedInt\" /><xs:element name=\"SourceFileName\" type=\"xs:string\" /></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType><xs:unique name=\"LineID\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//Lines\" /><xs:field xpath=\"LineID\" /></xs:unique><xs:unique name=\"MethodKey\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//Method\" /><xs:field xpath=\"MethodKeyName\" /></xs:unique><xs:unique name=\"ClassKey\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//Class\" /><xs:field xpath=\"ClassKeyName\" /></xs:unique><xs:unique name=\"NamespaceKeyName\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//NamespaceTable\" /><xs:field xpath=\"NamespaceKeyName\" /></xs:unique><xs:unique name=\"ModuleKey\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//Module\" /><xs:field xpath=\"ModuleName\" /></xs:unique><xs:unique name=\"SourceFileIDKey\" msdata:PrimaryKey=\"true\"><xs:selector xpath=\".//SourceFileNames\" /><xs:field xpath=\"SourceFileID\" /></xs:unique><xs:keyref name=\"Module_Namespace\" refer=\"ModuleKey\" msdata:IsNested=\"true\"><xs:selector xpath=\".//NamespaceTable\" /><xs:field xpath=\"ModuleName\" /></xs:keyref><xs:keyref name=\"Namespace_Class\" refer=\"NamespaceKeyName\" msdata:IsNested=\"true\"><xs:selector xpath=\".//Class\" /><xs:field xpath=\"NamespaceKeyName\" /></xs:keyref><xs:keyref name=\"Class_Method\" refer=\"ClassKey\" msdata:IsNested=\"true\"><xs:selector xpath=\".//Method\" /><xs:field xpath=\"@ClassKeyName\" /></xs:keyref><xs:keyref name=\"SourceFileNames_Lines\" refer=\"SourceFileIDKey\" msdata:ConstraintOnly=\"true\"><xs:selector xpath=\".//Lines\" /><xs:field xpath=\"SourceFileID\" /></xs:keyref><xs:keyref name=\"Method_Lines\" refer=\"MethodKey\" msdata:IsNested=\"true\"><xs:selector xpath=\".//Lines\" /><xs:field xpath=\"@MethodKeyName\" /></xs:keyref></xs:element></xs:schema>";

        public CoverageExporter(Stream stream, Dictionary<CoverageFileInfo, CoverageMapper> covInfo) {
            _covInfo = covInfo;
            _writer = XmlWriter.Create(stream);
        }

        public void Export() {
            _writer.WriteStartElement("CoverageDSPriv");
            _writer.WriteRaw(_schema);

            foreach (var keyValue in _covInfo) {
                var file = keyValue.Key;
                var collector = keyValue.Value;
                _writer.WriteStartElement("Module");

                WriteElement("ModuleName", collector.ModuleName);
                WriteElement("ImageSize", new FileInfo(file.Filename).Length);
                WriteElement("ImageLinkTime", "0");

                int linesCovered = 0, linesNotCovered = 0, blocksCovered = 0, blocksNotCovered = 0;
                AggregateStats(collector.GlobalScope, ref linesCovered, ref linesNotCovered, ref blocksCovered, ref blocksNotCovered);
                foreach (var klass in collector.Classes) {
                    AggregateStats(klass, ref linesCovered, ref linesNotCovered, ref blocksCovered, ref blocksNotCovered);
                }

                WriteCoverageData(linesCovered, linesNotCovered, blocksCovered, blocksNotCovered);

                _writer.WriteStartElement("NamespaceTable");

                WriteCoverageData(linesCovered, linesNotCovered, blocksCovered, blocksNotCovered);
                WriteElement("ModuleName", Analysis.ModulePath.FromFullPath(file.Filename).ModuleName);
                WriteElement("NamespaceKeyName", file.Filename);
                WriteElement("NamespaceName", "");

                // Write top-level class
                WriteClass(file.Filename, collector.GlobalScope, "<global>");

                foreach (var klass in collector.Classes) {
                    WriteClass(file.Filename, klass, GetQualifiedName(klass.Statement));
                }

                _writer.WriteEndElement();  // NamespaceTable
                _writer.WriteEndElement();  // Module

                _curFile++;
            }

            int curFile = 0;
            foreach (var file in _covInfo.Keys) {
                _writer.WriteStartElement("SourceFileNames");
                WriteElement("SourceFileID", curFile + 1);
                WriteElement("SourceFileName", file.Filename);
                _writer.WriteEndElement();

                curFile++;
            }

            _writer.WriteEndElement();
            _writer.Flush();
        }


        private void WriteClass(string filename, CoverageScope klass, string name) {
            _writer.WriteStartElement("Class");

            WriteElement("ClassKeyName", filename + "!" + name);
            WriteElement("ClassName", name);
            WriteElement("NamespaceKeyName", filename + "!" + name);

            int linesCovered = 0, linesNotCovered = 0, blocksCovered = 0, blocksNotCovered = 0;
            AggregateStats(klass, ref linesCovered, ref linesNotCovered, ref blocksCovered, ref blocksNotCovered);

            WriteCoverageData(linesCovered, linesNotCovered, blocksCovered, blocksNotCovered);

            WriteMethod(klass, klass.Statement.Name);
            WriteMethods(klass);

            _writer.WriteEndElement();
        }

        private void WriteMethods(CoverageScope klass) {
            foreach (var child in klass.Children) {
                FunctionDefinition funcDef = child.Statement as FunctionDefinition;
                if (funcDef != null) {
                    WriteMethod(child, GetQualifiedFunctionName(child.Statement));

                    WriteMethods(child);
                }
            }
        }

        private enum CoverageStatus {
            Covered = 0,
            PartiallyCovered = 1,
            NotCovered = 2
        }

        private void WriteMethod(CoverageScope child, string name) {
            _writer.WriteStartElement("Method");
            WriteElement("MethodKeyName", "method!" + ++_methodCount);
            WriteElement("MethodName", name);
            WriteElement("MethodFullName", name);
            WriteCoverageData(child);

            foreach (var line in child.Lines) {
                var lineNo = line.Key;
                var lineInfo = line.Value;
                _writer.WriteStartElement("Lines");
                WriteElement("LnStart", lineNo);
                WriteElement("ColStart", lineInfo.ColumnStart);
                WriteElement("LnEnd", lineNo);
                WriteElement("ColEnd", lineInfo.ColumnEnd);
                WriteElement("Coverage", (int)(lineInfo.Covered ? CoverageStatus.Covered : CoverageStatus.NotCovered));
                WriteElement("SourceFileID", _curFile + 1);
                WriteElement("LineID", line.Key);
                _writer.WriteEndElement();
            }
            _writer.WriteEndElement();
        }

        private void WriteCoverageData(CoverageScope scope) {
            WriteCoverageData(scope.LinesCovered, scope.LinesNotCovered, scope.BlocksCovered, scope.BlocksNotCovered);
        }

        private void WriteCoverageData(int linesCovered, int linesNotCovered, int blocksCovered, int blocksNotCovered) {
            WriteElement("LinesCovered", linesCovered);
            WriteElement("LinesPartiallyCovered", 0);
            WriteElement("LinesNotCovered", linesNotCovered);
            WriteElement("BlocksCovered", blocksCovered);
            WriteElement("BlocksNotCovered", blocksNotCovered);
        }

        private void WriteElement(string name, object value) {
            WriteElement(name, value.ToString());
        }

        private void WriteElement(string name, string value) {
            _writer.WriteStartElement(name);
            _writer.WriteString(value);
            _writer.WriteEndElement();
        }

        internal static string GetQualifiedName(ScopeStatement statement) {
            if (statement is PythonAst) {
                return null;
            }

            var baseName = GetQualifiedName(statement.Parent);

            if (baseName == null) {
                return statement.Name;
            }

            return baseName + "." + statement.Name;
        }

        internal static string GetQualifiedFunctionName(ScopeStatement statement) {
            if (statement is PythonAst || statement is ClassDefinition) {
                return null;
            }

            var baseName = GetQualifiedFunctionName(statement.Parent);

            if (baseName == null) {
                return statement.Name;
            }

            return baseName + "." + statement.Name;
        }

        private void AggregateStats(CoverageScope scope, ref int linesCovered, ref int linesNotCovered, ref int blocksCovered, ref int blocksNotCovered) {
            linesCovered += scope.LinesCovered;
            linesNotCovered += scope.LinesNotCovered;
            blocksCovered += scope.BlocksCovered;
            blocksNotCovered += scope.BlocksNotCovered;
            foreach (var child in scope.Children) {
                AggregateStats(child, ref linesCovered, ref linesNotCovered, ref blocksCovered, ref blocksNotCovered);
            }
        }

    }
}
