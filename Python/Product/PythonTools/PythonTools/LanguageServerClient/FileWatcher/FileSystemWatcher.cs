using System;
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

using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.LanguageServerClient.FileWatcher {
    /// <summary>
    /// Enum representing the type of changes to watch for.
    /// </summary>
    [Flags]
    [DataContract]
    public enum WatchKind {
        /// <summary>
        /// Create events.
        /// </summary>
        Create = 1,
        /// <summary>
        /// Change events.
        /// </summary>
        Change = 2,
        /// <summary>
        /// Delete events.
        /// </summary>
        Delete = 4
    }

    [JsonConverter(typeof(GlobPatternConverter))]
    public sealed class GlobPattern {
        public GlobPattern(string pattern, Uri baseUri = null) {
            Pattern = pattern;
            BaseUri = baseUri;
        }

        public string Pattern { get; }
        public Uri BaseUri { get; }
    }

    internal sealed class GlobPatternConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(GlobPattern);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.String) {
                return CreatePattern(token.Value<string>(), null);
            }

            if (token is JObject relativePattern) {
                var baseUriToken = relativePattern["baseUri"];
                var baseUriValue = baseUriToken?.Type == JTokenType.String
                    ? baseUriToken.Value<string>()
                    : (baseUriToken as JObject)?["uri"]?.Value<string>();
                if (!Uri.TryCreate(baseUriValue, UriKind.Absolute, out var baseUri)) {
                    return null;
                }
                return CreatePattern(relativePattern.Value<string>("pattern"), baseUri);
            }

            return null;
        }

        private static GlobPattern CreatePattern(string pattern, Uri baseUri) =>
            string.IsNullOrEmpty(pattern) ? null : new GlobPattern(pattern, baseUri);

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            throw new NotSupportedException();
    }

    [DataContract]
    public class FileSystemWatcher {
        /// <summary>
        /// Gets or sets the glob pattern to watch.
        /// </summary>
        [DataMember]
        public GlobPattern GlobPattern {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="T:Microsoft.VisualStudio.LanguageServer.Protocol.WatchKind" /> values that are of interest.
        /// </summary>
        [DataMember]
        public WatchKind Kind {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        } = WatchKind.Create | WatchKind.Change | WatchKind.Delete;
    }
}
