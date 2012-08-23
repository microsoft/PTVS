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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using EnvDTE;
//using EnvDTE80;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    public class VSUtility
    {
        public const string VSAddInName = "TcVsIdeTestHost";

        private static readonly string ProgId = "VisualStudio.DTE." + Version;
#if DEV11
        public static readonly string Version = "11.0";
#else
        public static readonly string Version = "10.0";
#endif
        private static readonly TimeSpan _ideStartupTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan _addinWaitTimeout = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan _baseSleepDuration = TimeSpan.FromMilliseconds(250);
        
        private IVsIdeTestHostAddin _testHostAddin;
        private int _processId;
        private EnvDTE.DTE dte;
        private object _hostSideLock = new object();
        
        public VSUtility(int processId)
        {
            _processId = processId;
            GetDTE();
        }

        public int ProjectCount
        {
            get
            {
                return dte.Solution.Projects.Count;
            }
        }

        public Collection<string> ProjectNames
        {
            get
            {
                Collection<string> names = new Collection<string>();
                EnvDTE.Projects pps = dte.Solution.Projects;
                foreach (EnvDTE.Project pp in pps)
                {
                    names.Add(pp.Name);
                }
                return names;
            }
        }

        public bool FindProjectItem(string projectName, string name)
        {
            bool found = false;
            EnvDTE.Projects vsProjects = dte.Solution.Projects;
            foreach (EnvDTE.Project vsProject in vsProjects)
            {
                if (vsProject.Name.Equals(projectName))
                {
                    EnvDTE.ProjectItem item = vsProject.ProjectItems.Item(name);
                    if (item != null)
                    {
                        found = true;
                    }
                }
            }
            return found;
        }

        public object Invoke(string functionName, Type typeInfo, params object[] functionParameters)
        {
            InitHostSide();

            object returnedObject = ExecuteVSMethod(functionName, typeInfo, functionParameters);
            return returnedObject;
        }

        private void GetDTE()
        {
            dte = VsIdeHostAdapter.VisualStudioIde.GetDteFromRot(ProgId, _processId);
        }

        private object ExecuteVSMethod(string functionName, Type typeInfo, params object[] functionParameters)
        {
            string className = typeInfo.FullName;
            Uri path = new Uri(typeInfo.Assembly.CodeBase);
            string localPath = path.LocalPath;
            object returnedObject = null;

            try
            {
                returnedObject = _testHostAddin.RemoteExecute(localPath, className, functionName, functionParameters);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    Assert.Fail("{0} : {1} : {2}", e.Message, functionName, e.InnerException.Message);
                }
                else
                {
                    Assert.Fail("{0} : {1}", e.Message, functionName);
                }
            }
            return returnedObject;
        }

        private void InitHostSide()
        {
            lock (_hostSideLock)
            {
                CreateHostSide();
                // If test run was started under debugger, attach debugger.
                // CheckAttachDebugger();
            }
        }

        private void CreateHostSide()
        {
            Stopwatch timer = Stopwatch.StartNew();
            do
            {
                try
                {
                    dte.MainWindow.Visible = true;    // This could be in TestRunConfig options for this host type.
                    break;
                }
                catch (Exception)
                {
                }
                System.Threading.Thread.Sleep(_baseSleepDuration);
            } 
            while (timer.Elapsed < _ideStartupTimeout);

            GetHostSideFromAddin();
        }

        /// <summary>
        /// Obtain host side from the addin.
        /// </summary>
        /// <returns></returns>
        private void GetHostSideFromAddin()
        {
            // Find the Addin.
            // Note: After VS starts addin needs some time to load, so we try a few times.
            AddIn addinLookingFor = null;

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                do
                {
                    try
                    {
                        // There is no index-by-name API, so we have to check all addins.
                        foreach (AddIn addin in dte.AddIns)
                        {
                            if (addin.Name == VSAddInName)
                            {
                                addinLookingFor = addin;
                                break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Catch all exceptions to prevent intermittent failures such as COMException (0x8001010A)
                        // while VS has not started yet. Just retry again until we timeout.
                    }

                    if (addinLookingFor != null)
                    {
                        break;
                    }

                    System.Threading.Thread.Sleep(_baseSleepDuration);
                } 
                while (timer.Elapsed < _addinWaitTimeout);
            }
            finally
            {
                timer.Stop();
            }

            if (addinLookingFor == null)
            {
                throw new VsIdeTestHostException("Timed out getting Vs Ide Test Host Add-in from Visual Studio. Please make sure that the Add-in is installed and started when VS starts (use Tools->Add-in Manager).");
            }

            _testHostAddin = (IVsIdeTestHostAddin)addinLookingFor.Object;
            ITestAdapter hostSide = _testHostAddin.GetHostSide();
            Contract.Assert(hostSide != null);
        }       
    }
}
