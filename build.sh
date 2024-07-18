#!/bin/bash

#git fetch upstream main
#git checkout main
#git rebase upstream/main
#git push


git checkout docs-init

rm -rf docs_temp

curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
prev=$(git show botr-init:commit)


if [ ! $curr = $prev ]
then
	echo "diff"
	
	cd docs/design/coreclr

	mkdocs build --site-dir '../../../docs_temp'

else
	echo "no diff"
fi;


git checkout botr-init


cd ../../..

pwd
ls -l


rm -rf docs
mv docs_temp docs

git add .
