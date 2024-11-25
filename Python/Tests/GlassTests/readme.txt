For developers working on Python Tools at Microsoft: Please refer to internal documentation for more information:
https://devdiv.visualstudio.com/DevDiv/_git/Concord?path=/Glass.md&version=GBmain&anchor=**when-should-i-run-glass-tests%3F**&_a=preview

Presentation on glass is here:
https://microsoft.sharepoint.com/:p:/t/VisualStudioProductTeam/EUp9Ve4joC1Aj_hHxdta9hIBJ99uTf7c_X0Z02_7OZU1Ig?e=IVOK2o

In order to test with glass, you need to be internal to Microsoft as the drop.exe we use to pull internal testing bits is not public.

Setup Glass Testing Environment
    Run `python build\setup_glass.py`
    or 
    Run `python build\setup_glass.py help` to see what steps are available

Running Glass Tests through Visual Studio
    Install the Glass.TestAdapter.vsix that gets downloaded as part of setup_glass.py
    Run VS as admin and load "\GlassTests\PythonTests" in open folder environment
    Run the tests through test explorer

Running Glass Tests through Command Line
    Run `python build\run_glass.py`
    or
    Run `python build\run_glass.py <test name>` 
        where <test name> is one of the tests listed when you run setup_glass "verify-listing"


Debugging test failures
   Running with actual product
   - Run PTVS under the debugger
   - In the EXP version that runs, open folder on where the test runs
   - create launch.vs.json with settings like so:
{

  "version": "0.2.1",
  "defaults": {},
  "configurations": [
    {
      "type": "python",
      "name": "py_mod.py",
      "project": "py_mod.py",
      "nativeDebug": true,
      "interpreter": "C:\\Users\\rchiodo\\AppData\\Local\\Programs\\Python\\Python313\\python_d.exe"
    }
  ]
}
    - set breakpoints like the test does (look at the testscript.xml)
    - do the same things the testscript does

    Debugging Glass itself
    - Set GLASS_DEBUG=1
    - Run test with `python build\run_glass.py <test name>`
    - Attach VS to the glass2.exe
    - Check for asserts or set breakpoints on events
