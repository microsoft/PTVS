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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TestUtilities
{
    public sealed class AssemblyLoader : IDisposable
    {
        private readonly Dictionary<string, List<string>> _knownAssemblies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        internal AssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        public void AddPaths(params string[] paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            if (paths.Length == 0)
            {
                throw new ArgumentException($"{nameof(paths)} should not be empty", nameof(paths));
            }

            foreach (var path in paths)
            {
                EnumerateAssemblies(path);
            }
        }

        public static void EnsureLoaded(params string[] assemblyNames)
        {
            foreach (var assemblyName in assemblyNames)
            {
                var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    ?? Assembly.Load(new AssemblyName { Name = assemblyName });

                if (loadedAssembly == null)
                {
                    throw new AssertFailedException($"Can't find {assemblyName} assembly");
                }

                loadedAssembly.GetTypes();
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            return ResolveAssembly(assemblyName, AssemblyLoadFrom);
        }

        private Assembly ResolveAssembly(string assemblyName, Func<string, string, Assembly> assemblyLoader)
        {
            if (!string.Equals(Path.GetExtension(assemblyName), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName += ".dll";
            }

            if (!_knownAssemblies.TryGetValue(assemblyName, out var assemblyPaths))
            {
                return null;
            }

            foreach (var assemblyPath in assemblyPaths)
            {
                var assembly = assemblyLoader(assemblyName, assemblyPath);
                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
        }

        private static Assembly AssemblyLoad(string assemblyName, string assemblyPath)
        {
            try
            {
                return Assembly.Load(new AssemblyName
                {
                    Name = assemblyName,
                    CodeBase = new Uri(assemblyPath).AbsoluteUri
                });
            }
            catch (FileLoadException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        private static Assembly AssemblyLoadFrom(string assemblyName, string assemblyPath)
        {
            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch (FileLoadException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        private void EnumerateAssemblies(string directory)
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(path);
                if (name != null)
                {
                    if (_knownAssemblies.TryGetValue(name, out var paths))
                    {
                        paths.Add(path);
                    }
                    else
                    {
                        _knownAssemblies[name] = new List<string> { path };
                    }
                }
            }
        }
    }
}
