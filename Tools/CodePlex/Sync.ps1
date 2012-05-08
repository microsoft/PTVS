param( [string] $direction, [string]$hg_repo, [string]$tfs_repo, [string]$commit_msg = "", [string]$suppress_push = "")

pushd

function clean_tfs($tfs_repo) {
	cd $tfs_repo
	tfpt scorch /noprompt /recursive /deletes
}

function clean_hg($hg_repo) {
	cd $hg_repo
	hg purge	
}

function copy_repo($from, $to) {
	# we only include specific top-level directories here on an opt-in basis
    $included_dirs = "Build", "Release", "Tools", "Servicing"
    foreach($included_dir in $included_dirs) {
        $cur_from = join-path $from $included_dir
        $cur_to = join-path $to $included_dir
        
        robocopy /MIR $cur_from $cur_to /XD .hg /XF OSSREADME_????.txt
	}

	# update the top level files which we include
	#robocopy $from $to
	foreach ($file in dir($to) | where { ! $_.PSIsContainer }) {
        $new_path = join-path $from $file.Name
		if (-not (test-path $new_path)) {
			del $file.FullName
		}
	}

}

function update_tfs($tfs_repo) {
	cd $tfs_repo
	tfpt online /adds /deletes /recursive /noprompt
}

function update_hg($hg_repo) {
	cd $hg_repo
	hg addremove -s 20
}

function commit_tfs($tfs_repo) {
    cd $tfs_repo
	tf checkin
}

function commit_hg($hg_repo) {
	cd $hg_repo
    if ($commit_msg -ne "") {
        hg commit -m $commit_msg
    } else {
        hg commit
    }    
    if ($suppress_push -eq "") {
        hg push 
	}
}

function test_build($target_repo) {
	cd $target_repo
	& join-path $target_repo Release\Product\Setup\buildRelease.ps1 $env:Temp\TestRelease
}

echo Hg:
echo $hg_repo
echo Tfs:
echo $tfs_repo

if($direction -eq "pull") {
	# pulling from codeplex
	clean_hg $hg_repo
	copy_repo $hg_repo $tfs_repo
	update_tfs $tfs_repo
	commit_tfs $tfs_repo
	
}elseif($direction -eq "push") {
	# pushing to codeplex
	clean_tfs $tfs_repo
	copy_repo $tfs_repo $hg_repo
	update_hg $hg_repo
	commit_hg $hg_repo
}else{
	echo "Unknown direction, expected push or pull"
}

popd