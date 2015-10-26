#! C:\IronPython27\ipy.exe
import clr
import sys
import threading

from Microsoft.Win32 import Registry
for vsver in ["14.0", "12.0", "11.0", "10.0"]:
    key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\PythonTools\\" + vsver)
    if not key:
        continue
    value = key.GetValue("InstallDir")
    key.Close()
    if value:
        print 'Adding %s to sys.path' % value
        sys.path.append(value)
        break

try:
    clr.AddReference("System.ComponentModel.Composition")
except IOError:
    print 'Failed to import System.ComponentModel.Composition'
    sys.exit(2)
try:
    clr.AddReference("Microsoft.PythonTools.Analysis")
except IOError:
    print 'Failed to import Microsoft.PythonTools.Analysis'
    sys.exit(3)
try:
    clr.AddReference("Microsoft.PythonTools.VSInterpreters")
except IOError:
    print 'Failed to import Microsoft.PythonTools.VSInterpreters'
    sys.exit(4)

from System.ComponentModel.Composition.Hosting import *
from Microsoft.PythonTools.Interpreter import *
container = CompositionContainer(AssemblyCatalog(clr.GetClrType(IInterpreterOptionsService).Assembly))

service = container.GetExportedValue[IInterpreterOptionsService]()

lock = threading.Lock()

for factory in service.Interpreters:
    print 'Refreshing %s... ' % factory.Description,
    lock.acquire()
    try:
        factory.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged, lambda i: lock.release())
        lock.acquire()
    finally:
        lock.release()
    print 'done!'
