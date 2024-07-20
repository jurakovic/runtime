#!/bin/bash

mkdocs_config=$(cat mkdocs.yml)

#curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
curr="foo" #$(curl -s "https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1" | jq -r '.[0].sha')
prev="bar" #$(git cat commit.txt)

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

  # change dotnet repo to fork; fix js api
  find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github.com\/)(dotnet)(\/runtime" title="Go to repository")/\1jurakovic\3/' {} +
  find docs/assets/javascripts -type f -iwholename "*.min.js" -exec sed -i -r 's/(https:\/\/api.github.com\/repos\/)(\$\{e\}\/\$\{t\})/\1dotnet\/runtime/' {} +

  # change view raw url
  find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github\.com\/dotnet\/runtime\/)(raw)(.*" title="View source of this page")/\1blob\3/' {} +

  #echo "$curr" > commit.txt

  #git add .
  #git commit -m "$curr"
fi;
