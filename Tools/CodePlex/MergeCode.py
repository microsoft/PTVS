import sys
import os

from System.Diagnostics import Process, ProcessStartInfo

import clr
clr.AddReference('Microsoft.TeamFoundation.VersionControl.Client')
clr.AddReference('Microsoft.TeamFoundation.Client')
clr.AddReference('Microsoft.TeamFoundation.VersionControl.Common')
from Microsoft.TeamFoundation.VersionControl.Client import *
from Microsoft.TeamFoundation.Client import *
from Microsoft.TeamFoundation.VersionControl.Common import *

tfs = TeamFoundationServer('http://tcvstf:8080/tfs/tc')
tfs.Authenticate()

vcs = tfs.GetService(VersionControlServer)
assert isinstance(vcs, VersionControlServer)

base_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.exists(os.path.join(base_dir, 'build.root')):
  base_dir = os.path.dirname(base_dir)
  
  
print 'root enlistment dir is', base_dir

hg_root = r'C:\Source\pytools'
ws = vcs.TryGetWorkspace(base_dir)

merge_from = ItemSpec(base_dir, RecursionType.Full)
cur_version = 32505

powershell = os.path.join(os.environ['WinDir'], r'System32\WindowsPowerShell\v1.0\powershell.exe')


for x in reversed(list(vcs.QueryHistory('$/TCWCS/Python/Open_Source/Feature/Python_2.0', VersionSpec.Latest, 0, RecursionType.Full, None, None, None, int.MaxValue, False, False))): 
    if cur_version >= x.ChangesetId:
        continue

    assert isinstance(x, Changeset)
    print '####################################################################################'
    print 'CHeckin: ' + str(x.ChangesetId)
    status = ws.Get(GetRequest(merge_from, VersionSpec.Parse('C' + str(x.ChangesetId), None)[0]), GetOptions.Overwrite|GetOptions.GetAll)

    if status.GetFailures():
        print 'failed'
        print status.GetFailures()
        sys.exit(1)
    
    if status.NoActionNeeded:
        print 'No action needed', x.ChangesetId
        cur_version = x.ChangesetId
        continue
    print 'Warnings', status.HaveResolvableWarnings
    print 'Conflicts', status.NumConflicts
    for conflict in ws.QueryConflicts((base_dir, ), True):
        assert isinstance(conflict, Conflict)
        print 'Can Merge', conflict.CanMergeContent
        conflict.Resolution = Resolution.AcceptMerge
        conflict.ResolutionOptions.UseInternalEngine = True
        ws.ResolveConflict(conflict)
        if not conflict.IsResolved:
            if conflict.CanMergeContent:
                conflict.Resolution = Resolution.AcceptYours
                ws.ResolveConflict(conflict)
                if not conflict.IsResolved:
                    print 'Failed to resolve conflict', dir(conflict)
                    sys.exit(1)
            else:
                print 'Failed to resolve conflict', dir(conflict)
                sys.exit(1)

    #ws.CheckIn(ws.GetPendingChanges(), x.Comment)    
    comment = x.Comment.Replace("'", "''").Replace(unichr(8217), "''")
    if 'node' in comment.lower():
        f = file('tmp.txt', 'w')
        f.write(comment)
        f.close()
        os.system('notepad tmp.txt')
        f = file('tmp.txt', 'r')
        comment = ''.join(f.readlines())
        f.close()
    
    psi = ProcessStartInfo(powershell, 
                           r'"' + base_dir + "\Tools\CodePlex\Sync.ps1\" push '" + 
                           hg_root + "' '" + comment + 
                           "' -suppress_push True -commit_date '" + x.CreationDate.ToString("yyyy-MM-dd HH:mm:ss") + "'" +
                           " -user_name '" + x.Committer + "'")
    print psi.Arguments
    psi.UseShellExecute = False
    p = Process.Start(psi)
    p.WaitForExit()
    
    cur_version = x.ChangesetId
    