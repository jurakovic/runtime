#!/bin/bash

function main() {

  #git fetch upstream main
  #git checkout main
  #git rebase upstream/main
  #git push


  git checkout docs-init

  rm -rf docs_temp

  mkdocs_config=$(git show botr-init:mkdocs.yml)

  curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
  prev=$(git show botr-init:commit.txt)


  if [ ! $curr = $prev ]
  then
  	echo "diff"

  	cd docs/design/coreclr

  	echo "$mkdocs_config" | mkdocs build --site-dir ../../../docs_temp -f -

  else
    echo "no diff"
  fi;

  git checkout botr-init
  cd ../../..

  if [ ! $curr = $prev ]
  then

    rm -rf docs
    mv docs_temp docs

    echo "$curr" > commit.txt

    #git add .
  fi;
}

main
