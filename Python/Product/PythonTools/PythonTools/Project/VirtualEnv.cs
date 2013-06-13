using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    static class VirtualEnv {
        private static readonly KeyValuePair<string, string>[] UnbufferedEnv = new[] { 
            new KeyValuePair<string, string>("PYTHONUNBUFFERED", "1")
        };

        public static Task Install(IPythonInterpreterFactory factory, Redirector output = null) {
            return Pip.Install(factory, "virtualenv", output);
        }

        public static Task Create(IPythonInterpreterFactory factory, string path, Redirector output = null) {
            return Create(factory, null, path, output);
        }

        public static Task Create(
            IPythonInterpreterFactory factory,
            IServiceProvider site,
            string path,
            Redirector output = null) {

            Task task;
            if (site != null) {
                task = Task.Factory.StartNew((Action)(() => {
                    bool hasPip = false, hasVirtualEnv = false;
                    foreach (var mp in ModulePath.GetModulesInLib(factory)) {
                        if (!hasPip && mp.ModuleName == "pip") {
                            hasPip = true;
                        }
                        if (!hasVirtualEnv && mp.ModuleName == "virtualenv") {
                            hasVirtualEnv = true;
                        }
                        if (hasPip && hasVirtualEnv) {
                            break;
                        }
                    }

                    if (!hasVirtualEnv) {
                        if (!hasPip) {
                            Pip.QueryInstallPip(factory,
                                site,
                                SR.GetString(SR.InstallVirtualEnvAndPip),
                                output).Wait();
                            Pip.Install(factory, "virtualenv", output).Wait();
                        } else {
                            Pip.QueryInstall(factory, "virtualenv",
                                site,
                                SR.GetString(SR.InstallVirtualEnv),
                                output).Wait();
                        }
                    }
                }));
            } else {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetResult(null);
                task = tcs.Task;
            }

            return task.ContinueWith(t1 => {
                path = CommonUtils.TrimEndSeparator(path);
                var name = Path.GetFileName(path);
                var dir = Path.GetDirectoryName(path);
                int? exitCode = null;

                if (output != null) {
                    output.WriteLine(SR.GetString(SR.VirtualEnvCreating, path));
                    output.Show();
                }
                using (var proc = ProcessOutput.Run(factory.Configuration.InterpreterPath,
                    new[] { "-m", "virtualenv", "--distribute", name },
                    dir,
                    new Dictionary<string, string> { { "PYTHONUNBUFFERED", "1" } },
                    false,
                    output)) {
                    proc.Wait();
                    exitCode = proc.ExitCode;

                    if (output != null) {
                        if (exitCode == 0) {
                            output.WriteLine(SR.GetString(SR.VirtualEnvCreationSucceeded, path));
                        } else {
                            output.WriteLine(SR.GetString(SR.VirtualEnvCreationFailedExitCode, path, exitCode ?? -1));
                        }
                        output.Show();
                    }
                }

                if (exitCode != 0 || !Directory.Exists(path)) {
                    throw new InvalidOperationException(SR.GetString(SR.VirtualEnvCreationFailed, path));
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private static IPythonInterpreterFactory FindBaseInterpreterFromVirtualEnv(
            string libPath,
            IInterpreterOptionsService service) {
            string prefixFile = Path.Combine(libPath, "orig-prefix.txt");
            if (File.Exists(prefixFile)) {
                try {
                    var lines = File.ReadAllLines(prefixFile);
                    if (lines.Length >= 1 && lines[0].IndexOfAny(Path.GetInvalidPathChars()) == -1) {
                        return service.Interpreters.FirstOrDefault(interp =>
                            CommonUtils.IsSamePath(interp.Configuration.PrefixPath, lines[0])
                        );
                    }
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (System.Security.SecurityException) {
                }
            }
            return null;
        }

        private static string FindFile(string root, string file, int depthLimit = 2) {
            var candidate = Path.Combine(root, file);
            if (File.Exists(candidate)) {
                return candidate;
            }
            candidate = Path.Combine(root, "Scripts", file);
            if (File.Exists(candidate)) {
                return candidate;
            }

            // Do a BFS of the filesystem to ensure we find the match closest to
            // the root directory.
            var dirQueue = new Queue<string>();
            dirQueue.Enqueue(root);
            dirQueue.Enqueue("<EOD>");
            while (dirQueue.Any()) {
                var dir = dirQueue.Dequeue();
                if (dir == "<EOD>") {
                    depthLimit -= 1;
                    if (depthLimit <= 0) {
                        return null;
                    }
                    continue;
                }
                var result = Directory.EnumerateFiles(dir, file, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (result != null) {
                    return result;
                }
                foreach (var subDir in Directory.EnumerateDirectories(dir)) {
                    dirQueue.Enqueue(subDir);
                }
                dirQueue.Enqueue("<EOD>");
            }
            return null;
        }


        public static InterpreterFactoryCreationOptions FindInterpreterOptions(
            string prefixPath,
            IInterpreterOptionsService service,
            IPythonInterpreterFactory baseInterpreter = null) {

            var result = new InterpreterFactoryCreationOptions();

            // Find site.py to find the library
            var libPath = FindFile(prefixPath, "site.py");
            if (!File.Exists(libPath)) {
                return null;
            }
            libPath = Path.GetDirectoryName(libPath);


            if (baseInterpreter == null) {
                baseInterpreter = FindBaseInterpreterFromVirtualEnv(libPath, service);
                if (baseInterpreter == null) {
                    return null;
                }
            }

            // The interpreter name should be the same as the base interpreter.
            var interpExe = Path.GetFileName(baseInterpreter.Configuration.InterpreterPath);
            result.InterpreterPath = FindFile(prefixPath, interpExe);
            interpExe = Path.GetFileName(baseInterpreter.Configuration.WindowsInterpreterPath);
            result.WindowInterpreterPath = FindFile(prefixPath, interpExe);

            result.PrefixPath = prefixPath;
            result.LibraryPath = libPath;
            result.Description = Path.GetFileName(CommonUtils.TrimEndSeparator(prefixPath));

            result.Id = baseInterpreter.Id;
            result.LanguageVersion = baseInterpreter.Configuration.Version;
            result.Architecture = baseInterpreter.Configuration.Architecture;
            result.PathEnvironmentVariableName = baseInterpreter.Configuration.PathEnvironmentVariable;
            result.WatchLibraryForNewModules = true;

            return result;
        }

        // This helper function is not yet needed, but may be useful at some point.

        //public static string FindLibPathFromInterpreter(string interpreterPath) {
        //    using (var output = ProcessOutput.RunHiddenAndCapture(interpreterPath, "-c", "import site; print(site.__file__)")) {
        //        output.Wait();
        //        return output.StandardOutputLines
        //            .Where(line => !string.IsNullOrWhiteSpace(line) && line.IndexOfAny(Path.GetInvalidPathChars()) == -1)
        //            .Select(line => Path.GetDirectoryName(line))
        //            .LastOrDefault(dir => Directory.Exists(dir));
        //    }
        //}
    }
}
