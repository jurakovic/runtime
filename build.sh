#!/bin/bash

#git fetch upstream main
#git checkout main
#git rebase upstream/main
#git push


git checkout docs-init

curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
prev=$(git show botr:commit)


if [! $curr = $prev ]
then
	echo "diff"
else
	echo "no diff"
fi;
