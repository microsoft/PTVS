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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.EnvironmentsList {
    struct Pep440Version : IComparable<Pep440Version>, IEquatable<Pep440Version> {
        public static readonly Pep440Version Empty = new Pep440Version(new int[0]);

        private static readonly Regex LocalVersionRegex = new Regex(@"^[a-zA-Z0-9][a-zA-Z0-9.]*(?<=[a-zA-Z0-9])$");
        private static readonly Regex FullVersionRegex = new Regex(@"^
            v?
            ((?<epoch>\d+)!)?
            (?<release>\d+(\.\d+)*)
            ([.\-_]?(?<preName>a|alpha|b|beta|rc|c|pre|preview)[.\-_]?(?<pre>\d+)?)?
            ([.\-_]?(post|rev|r)[.\-_]?(?<post>\d+)?|-(?<post>\d+))?
            ([.\-_]?dev[.\-_]?(?<dev>\d+))?
            (\+(?<local>[a-zA-Z0-9][a-zA-Z0-9.\-_]*(?<=[a-zA-Z0-9])))?
            $", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private string _normalized;

        private Pep440Version(
            IEnumerable<int> release,
            int epoch = 0,
            Pep440PreReleaseName preReleaseName = Pep440PreReleaseName.None,
            int preRelease = 0,
            int postRelease = 0,
            int devRelease = 0,
            string localVersion = null,
            string originalForm = null
        ) : this() {
            _normalized = null;
            Epoch = epoch;
            Release = release.ToArray();
            PreReleaseName = preReleaseName;
            PreRelease = preReleaseName != Pep440PreReleaseName.None ? preRelease : 0;
            PostRelease = postRelease;
            DevRelease = devRelease;
            LocalVersion = localVersion;
            OriginalForm = originalForm;
        }

        public int Epoch { get; set; }

        public IList<int> Release { get; private set; }

        public Pep440PreReleaseName PreReleaseName { get; set; }

        public int PreRelease { get; set; }

        public int PostRelease { get; set; }

        public int DevRelease { get; set; }

        public string LocalVersion { get; set; }

        public string OriginalForm { get; set; }

        public bool IsEmpty {
            get {
                return Release == null || Release.Count == 0;
            }
        }

        public bool IsFinalRelease {
            get {
                return PreReleaseName == Pep440PreReleaseName.None &&
                    DevRelease == 0 &&
                    string.IsNullOrEmpty(LocalVersion);
            }
        }

        private bool Validate(out Exception error) {
            error = null;
            if (Epoch < 0) {
                error = new FormatException("Epoch must be 0 or greater");
                return false;
            }
            if (Release != null && Release.Any(i => i < 0)) {
                error = new FormatException("All components of Release must be 0 or greater");
                return false;
            }
            if (PreReleaseName == Pep440PreReleaseName.None) {
                if (PreRelease != 0) {
                    error = new FormatException("PreRelease must be 0 when PreReleaseName is None");
                    return false;
                }
            } else {
                if (PreRelease < 0) {
                    error = new FormatException("PreRelease must be 0 or greater");
                    return false;
                }
            }
            if (PostRelease < 0) {
                error = new FormatException("PostRelease must be 0 or greater");
                return false;
            }
            if (DevRelease < 0) {
                error = new FormatException("DevRelease must be 0 or greater");
                return false;
            }
            if (!string.IsNullOrEmpty(LocalVersion) && !LocalVersionRegex.IsMatch(LocalVersion)) {
                error = new FormatException("LocalVersion has invalid characters");
                return false;
            }
            return true;
        }

        public override string ToString() {
            return string.IsNullOrEmpty(OriginalForm) ? NormalizedForm : OriginalForm;
        }

        public string NormalizedForm {
            get {
                if (_normalized == null) {
                    var sb = new StringBuilder();
                    if (Epoch != 0) {
                        sb.Append(Epoch);
                        sb.Append('!');
                    }
                    if (Release == null) {
                        sb.Append("0");
                    } else {
                        sb.Append(string.Join(".", Release.Select(i => i.ToString())));
                    }
                    if (PreReleaseName != Pep440PreReleaseName.None) {
                        switch (PreReleaseName) {
                            case Pep440PreReleaseName.Alpha:
                                sb.Append('a');
                                break;
                            case Pep440PreReleaseName.Beta:
                                sb.Append('b');
                                break;
                            case Pep440PreReleaseName.RC:
                                sb.Append("rc");
                                break;
                            default:
                                Debug.Fail("Unhandled Pep440PreReleaseName value: " + PreReleaseName.ToString());
                                break;
                        }
                        sb.Append(PreRelease);
                    }
                    if (PostRelease > 0) {
                        sb.Append(".post");
                        sb.Append(PostRelease);
                    }
                    if (DevRelease > 0) {
                        sb.Append(".dev");
                        sb.Append(DevRelease);
                    }
                    if (!string.IsNullOrEmpty(LocalVersion)) {
                        sb.Append('-');
                        sb.Append(LocalVersion);
                    }

                    _normalized = sb.ToString();
                }
                return _normalized;
            }
        }

        public override bool Equals(object obj) {
            if (obj is Pep440Version) {
                return CompareTo((Pep440Version)obj) == 0;
            }
            return false;
        }

        public bool Equals(Pep440Version other) {
            return CompareTo(other) == 0;
        }

        public override int GetHashCode() {
            return NormalizedForm.GetHashCode();
        }

        public int CompareTo(Pep440Version other) {
            Exception error;
            if (!Validate(out error)) {
                throw error;
            }
            if (!other.Validate(out error)) {
                throw new ArgumentException("Invalid version", "other", error);
            }

            int c = Epoch.CompareTo(other.Epoch);
            if (c != 0) {
                return c;
            }

            if (Release != null && other.Release != null) {
                for (int i = 0; i < Release.Count || i < other.Release.Count; ++i) {
                    c = Release.ElementAtOrDefault(i).CompareTo(other.Release.ElementAtOrDefault(i));
                    if (c != 0) {
                        return c;
                    }
                }
            } else if (Release == null) {
                // No release, so we sort earlier if other has one
                return other.Release == null ? 0 : -1;
            } else {
                // We have a release and other doesn't, so we sort later
                return 1;
            }

            if (PreReleaseName != other.PreReleaseName) {
                // Regular comparison mishandles None
                if (PreReleaseName == Pep440PreReleaseName.None) {
                    return 1;
                } else if (other.PreReleaseName == Pep440PreReleaseName.None) {
                    return -1;
                }
                // Neither value is None, so CompareTo will be correct
                return PreReleaseName.CompareTo(other.PreReleaseName);
            }

            c = PreRelease.CompareTo(other.PreRelease);
            if (c != 0) {
                return c;
            }

            c = PostRelease.CompareTo(other.PostRelease);
            if (c != 0) {
                return c;
            }

            c = DevRelease.CompareTo(other.DevRelease);
            if (c != 0) {
                if (DevRelease == 0 || other.DevRelease == 0) {
                    // When either DevRelease is zero, the sort order needs to
                    // be reversed.
                    return -c;
                }
                return c;
            }

            if (string.IsNullOrEmpty(LocalVersion)) {
                if (string.IsNullOrEmpty(other.LocalVersion)) {
                    // No local versions, so we are equal
                    return 0;
                }
                // other has a local version, so we sort earlier
                return -1;
            } else if (string.IsNullOrEmpty(other.LocalVersion)) {
                // we have a local version, so we sort later
                return 1;
            }

            var lv1 = LocalVersion.Split('.');
            var lv2 = other.LocalVersion.Split('.');
            for (int i = 0; i < lv1.Length || i < lv2.Length; ++i) {
                if (i >= lv1.Length) {
                    // other has a longer local version, so we sort earlier
                    return -1;
                } else if (i >= lv2.Length) {
                    // we have a longer local version, so we sort later
                    return 1;
                }
                var p1 = lv1[i];
                var p2 = lv2[i];

                int i1, i2;
                if (int.TryParse(p1, out i1)) {
                    if (int.TryParse(p2, out i2)) {
                        c = i1.CompareTo(i2);
                    } else {
                        // we have a number and other doesn't, so we sort later
                        return 1;
                    }
                } else if (int.TryParse(p2, out i2)) {
                    // other has a number and we don't, so we sort earlier
                    return -1;
                } else {
                    c = p1.CompareTo(p2);
                }

                if (c != 0) {
                    return c;
                }
            }

            // After all that, we are equal!
            return 0;
        }

        public static IEnumerable<Pep440Version> TryParseAll(IEnumerable<string> versions) {
            foreach (var s in versions) {
                Pep440Version value;
                if (TryParse(s, out value)) {
                    yield return value;
                }
            }
        }

        public static bool TryParse(string s, out Pep440Version value) {
            Exception error;
            return ParseInternal(s, out value, out error);
        }

        public static Pep440Version Parse(string s) {
            Pep440Version value;
            Exception error;
            if (!ParseInternal(s, out value, out error)) {
                throw error;
            }
            return value;
        }

        private static bool ParseInternal(string s, out Pep440Version value, out Exception error) {
            value = default(Pep440Version);
            error = null;

            if (string.IsNullOrEmpty(s)) {
                error = new ArgumentNullException("s");
                return false;
            }
            var trimmed = s.Trim(" \t\n\r\f\v".ToCharArray());
            var m = FullVersionRegex.Match(trimmed);
            if (!m.Success) {
                error = new FormatException(trimmed);
                return false;
            }

            int epoch = 0, pre = 0, dev = 0, post = 0;
            string local = null;
            var release = new List<int>();
            foreach (var v in m.Groups["release"].Value.Split('.')) {
                int i;
                if (!int.TryParse(v, out i)) {
                    error = new FormatException(
                        string.Format("'{0}' is not a valid version", m.Groups["release"].Value)
                    );
                    return false;
                }
                release.Add(i);
            }

            if (m.Groups["epoch"].Success) {
                if (!int.TryParse(m.Groups["epoch"].Value, out epoch)) {
                    error = new FormatException(string.Format("'{0}' is not a number", m.Groups["epoch"].Value));
                    return false;
                }
            }

            var preName = Pep440PreReleaseName.None;
            if (m.Groups["preName"].Success) {
                switch(m.Groups["preName"].Value.ToLowerInvariant()) {
                    case "a":
                    case "alpha":
                        preName = Pep440PreReleaseName.Alpha;
                        break;
                    case "b":
                    case "beta":
                        preName = Pep440PreReleaseName.Beta;
                        break;
                    case "rc":
                    case "c":
                    case "pre":
                    case "preview":
                        preName = Pep440PreReleaseName.RC;
                        break;
                    default:
                        error = new FormatException(string.Format("'{0}' is not a valid prerelease name", preName));
                        return false;
                }
            }

            if (m.Groups["pre"].Success) {
                if (!int.TryParse(m.Groups["pre"].Value, out pre)) {
                    error = new FormatException(string.Format("'{0}' is not a number", m.Groups["pre"].Value));
                    return false;
                }
            }

            if (m.Groups["dev"].Success) {
                if (!int.TryParse(m.Groups["dev"].Value, out dev)) {
                    error = new FormatException(string.Format("'{0}' is not a number", m.Groups["dev"].Value));
                    return false;
                }
            }

            if (m.Groups["post"].Success) {
                if (!int.TryParse(m.Groups["post"].Value, out post)) {
                    error = new FormatException(string.Format("'{0}' is not a number", m.Groups["post"].Value));
                    return false;
                }
            }

            if (m.Groups["local"].Success) {
                local = Regex.Replace(m.Groups["local"].Value, "[^a-zA-Z0-9.]", ".");
            }

            value = new Pep440Version(
                release,
                epoch,
                preName,
                pre,
                post,
                dev,
                local,
                s
            );

            return value.Validate(out error);
        }
    }

    enum Pep440PreReleaseName {
        None = 0,
        Alpha = 1,
        Beta = 2,
        RC = 3
    }
}
