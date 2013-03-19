param( [string] $direction, [string]$hg_repo, [string]$commit_msg = "", [string]$suppress_push = "", [string]$commit_date = "", [string]$user_name = "")

pushd

function clean_tfs($tfs_repo) {
	cd $tfs_repo
	tfpt scorch /noprompt /recursive /deletes
}

function clean_hg($hg_repo, $branch) {
	cd $hg_repo
	hg update $branch
	hg purge	
}

function sync_hg($hg_repo, $branch) { 
  cd $hg_repo
	hg pull
	hg update $branch	
}

function copy_repo($from, $to) {
	# we only include specific top-level directories here on an opt-in basis
  $included_dirs = "Build", "Release", "Tools", "Prerequisites", "Common", "Python"
  foreach($included_dir in $included_dirs) {
      $cur_from = join-path $from $included_dir
      $cur_to = join-path $to $included_dir
      
      robocopy /MIR $cur_from $cur_to /XD .hg /XF OSSREADME_????.txt /XD PyExpress
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
      if ($commit_date -ne "") {
          if($user_name -ne "") {
              hg commit -m $commit_msg -d $commit_date -u $user_name
          } else {
              hg commit -m $commit_msg -d $commit_date
          }
      } else{
          if($user_name -ne "") {
            hg commit -m $commit_msg -u $user_name
          } else {
            hg commit -m $commit_msg
          }
      }
  } else {
      if($user_name -ne "") {
          hg commit -u $user_name
      } else {
          hg commit
      }
  }    
  if ($suppress_push -eq "") {
      hg push 
	}
}

function test_build($target_repo) {
	cd $target_repo
	& join-path $target_repo Release\Product\Setup\buildRelease.ps1 $env:Temp\TestRelease
}

function get-syncroot() {
  $Invocation = (Get-Variable MyInvocation -Scope 1).Value
  $curdir = Split-Path $Invocation.MyCommand.Path  
  while (-not (test-path $curdir\build.root)) {
    $curdir = split-path $curdir -parent
  }
  return $curdir
}


function get-currentbranch() {
  $Invocation = (Get-Variable MyInvocation -Scope 1).Value
  $curdir = Split-Path $Invocation.MyCommand.Path  
  while (-not (test-path $curdir\build.root)) {
    $curdir = split-path $curdir -parent
  }
  return split-path $curdir -leaf
}

function map-branchname($name) {
  switch($name) {
    "Python_2.0" { "default" }
    "Python_Main" { "default" }
    "Python_1.0" { "PTVS_1.0" }
    "Python_1.1" { "PTVS_1.1" }
    "Python_1.5" { "PTVS_1.5" }
    default { throw "Unknown branch, cannot push " + $name }
  }
}

function map-committer($name) {
  switch($name) {
    "REDMOND\dinov" { "dinov" }
    "REDMOND\pminaev" { "pminaev" }
    "REDMOND\zacha" { "ZachA" }
    "REDMOND\gilbertw" { "RickWinter" }
    "REDMOND\huvalo" { "huguesv" }
    "REDMOND\stevdo" { "zooba" }
    default { $name }
  }
}

$user_name = (map-committer ($user_name))
$tfs_repo = (get-syncroot)
echo $hg_repo
$branch = (map-branchname (get-currentbranch))
echo Hg:
echo $branch in $hg_repo
echo Tfs:
echo $tfs_repo
echo $user_name

if ($direction -eq "pull") {
	# pulling from codeplex
	clean_hg $hg_repo $branch
	copy_repo $hg_repo $tfs_repo
	update_tfs $tfs_repo
	commit_tfs $tfs_repo	
} elseif ($direction -eq "push") {
	# pushing to codeplex
	clean_tfs $tfs_repo
	sync_hg $hg_repo $branch
	copy_repo $tfs_repo $hg_repo
	update_hg $hg_repo
	commit_hg $hg_repo
}else{
	echo "Unknown direction, expected push or pull"
}

popd