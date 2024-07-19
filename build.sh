#!/bin/bash

mkdocs_config=$(cat mkdocs.yml)

#curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
curr=$(curl -s "https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1" | jq -r '.[0].sha')
prev="foo" #$(git cat commit.txt)

if [ ! $curr = $prev ]
then
  echo "diff"

  git checkout main

  cd docs/design/coreclr

  rm -rf docs_temp
  echo "$mkdocs_config" | mkdocs build --site-dir ../../../docs_temp -f -

else
  echo "no diff"
fi;

git checkout update #botr
cd ../../..

if [ ! $curr = $prev ]
then

  rm -rf docs
  mv docs_temp docs

  echo "$curr" > commit.txt

  #git add .
  #git commit -m "$curr"
fi;
