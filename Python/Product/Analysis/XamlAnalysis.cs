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
#if DESKTOP
using System.Collections.Generic;
using System.IO;
using System.Xaml;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Walks the XAML file and provides data based upon things which should be provided via intellisense.
    /// </summary>
    sealed class XamlAnalysis {
        private readonly Dictionary<string, XamlTypeReference> _knownTypes = new Dictionary<string, XamlTypeReference>();
        private readonly Dictionary<string, XamlMemberReference> _eventInfo = new Dictionary<string, XamlMemberReference>();

        enum MemberType {
            Unknown,
            XName,
            Event
        }

        public XamlAnalysis(string filename) {
            try {
                var settings = new XamlXmlReaderSettings();
                settings.ProvideLineInfo = true;

                using (XamlXmlReader reader = new XamlXmlReader(filename, settings)) {
                    Analyze(reader);
                }
            } catch {
                // ignore malformed XAML - XamlReader does a bad job of documenting what it throws
                // so we just need to try/catch here.
            }
        }

        public XamlAnalysis(TextReader textReader) {
            try {
                var settings = new XamlXmlReaderSettings();
                settings.ProvideLineInfo = true;

                var context = new XamlSchemaContext(new XamlSchemaContextSettings() { FullyQualifyAssemblyNamesInClrNamespaces = true });

                using (XamlXmlReader reader = new XamlXmlReader(textReader, context, settings)) {
                    Analyze(reader);
                }
            } catch {
                // ignore malformed XAML - XamlReader does a bad job of documenting what it throws
                // so we just need to try/catch here.
            }
        }

        private void Analyze(XamlXmlReader reader) {
            Stack<XamlType> objectTypes = new Stack<XamlType>();
            Stack<MemberType> nameStack = new Stack<MemberType>();
            Stack<XamlMember> eventStack = new Stack<XamlMember>();
            while (reader.Read()) {
                switch (reader.NodeType) {
                    case XamlNodeType.StartObject: objectTypes.Push(reader.Type); break;
                    case XamlNodeType.EndObject:
                        if (objectTypes.Count > 0) {
                            objectTypes.Pop();
                        }
                        break;
                    case XamlNodeType.NamespaceDeclaration:
                        break;
                    case XamlNodeType.StartMember:
                        var property = reader.Member;
                        if (property.Name == "Name" && property.Type.UnderlyingType == typeof(string)) {
                            nameStack.Push(MemberType.XName);
                        } else if (property.IsEvent) {
                            nameStack.Push(MemberType.Event);
                            eventStack.Push(property);
                        } else {
                            nameStack.Push(MemberType.Unknown);
                        }
                        break;

                    case XamlNodeType.EndMember:
                        if (nameStack.Pop() == MemberType.Event) {
                            eventStack.Pop();
                        }
                        break;
                    case XamlNodeType.GetObject:

                    case XamlNodeType.Value:
                        object value = reader.Value;
                        if (value is string) {
                            switch (nameStack.Peek()) {
                                case MemberType.XName:
                                    // we are writing a x:Name, save it so we can later get the name from the scope                                    
                                    _knownTypes[(string)value] = new XamlTypeReference(objectTypes.Peek(), reader.LineNumber, reader.LinePosition);
                                    break;
                                case MemberType.Event:
                                    // we have an event handler, save the method name and the XamlMember for the event
                                    _eventInfo[(string)value] = new XamlMemberReference(eventStack.Peek(), reader.LineNumber, reader.LinePosition);
                                    break;
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Dictionary from object name to type information for that object.
        /// </summary>
        public Dictionary<string, XamlTypeReference> NamedObjects {
            get {
                return _knownTypes;
            }
        }

        /// <summary>
        /// Dictionary from event handler name to event handler member info.  The name referes
        /// to something which will be fetched from the root object for binding the event 
        /// handler to.  
        /// </summary>
        public Dictionary<string, XamlMemberReference> EventHandlers {
            get {
                return _eventInfo;
            }
        }
    }

    struct XamlMemberReference {
        public readonly XamlMember Member;
        public readonly int LineNumber;
        public readonly int LineOffset;

        public XamlMemberReference(XamlMember member, int lineNo, int lineOffset) {
            Member = member;
            LineNumber = lineNo;
            LineOffset = lineOffset;
        }
    }

    struct XamlTypeReference {
        public readonly XamlType Type;
        public readonly int LineNumber;
        public readonly int LineOffset;

        public XamlTypeReference(XamlType type, int lineNo, int lineOffset) {
            Type = type;
            LineNumber = lineNo;
            LineOffset = lineOffset;
        }
    }
}
#endif
