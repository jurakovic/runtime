#!/bin/bash

mkdocs_config=$(cat mkdocs.yml)

#curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
curr="foo" #$(curl -s "https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1" | jq -r '.[0].sha')
prev="foo" #$(cat commit.txt)

if [ $curr = $prev ]
then
  echo "There are no changes"
  read -p "Continue with build anyway? (y/n) " yn
else
  echo "There are changes"
  read -p "Continue with build? (y/n) " yn
fi

if [ -z "$yn" ] || [ "$yn" != "y" ]; then exit 0; fi;

# get current branch
branch=$(git branch --show-current)

echo "Checking out 'main' branch"
git checkout main

#cd docs/design/coreclr

# clear any leftovers
rm -rf site

echo "Staring mkdocs build"
echo "$mkdocs_config" > mkdocs.yml
docker run --rm -it -v ${PWD}:/docs mkdocs-botr build
#for debugging:
#docker run --rm -it -v ${PWD}:/docs --entrypoint /bin/sh mkdocs-botr
rm mkdocs.yml

echo "Checking out '$branch' branch"
git checkout $branch
#cd ../../..

# clear old build
rm -rf docs

# rename
mv site docs

# change dotnet repo to fork; fix api url
find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github.com\/)(dotnet)(\/runtime" title="Go to repository")/\1jurakovic\3/' {} +
find docs/assets/javascripts -type f -iwholename "*.min.js" -exec sed -i -r 's/(https:\/\/api.github.com\/repos\/)(\$\{e\}\/\$\{t\})/\1dotnet\/runtime/' {} +

# change view url
find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github\.com\/dotnet\/runtime\/)(raw)(.*" title="View source of this page")/\1blob\3/' {} +

# fix chapters url
sed -i -r 's/(href=")(..\/botr)(">All Book of the Runtime \(BOTR\) chapters on GitHub)/\1https:\/\/github.com\/dotnet\/runtime\/blob\/main\/docs\/design\/coreclr\/botr\3/' docs/index.html

# add footer url
text='<br> Build repo: <a href="https://github.com/jurakovic/runtime" target="_blank" rel="noopener">jurakovic/runtime</a>'
mapfile -t apps < <(find docs -type f -iwholename "*.html")
for file in "${apps[@]}"; do
  total_lines=$(wc -l < "$file")
  insert_line=$((total_lines - 44))
  if [ "$insert_line" -gt 0 ]; then sed -i "${insert_line}i$text" "$file"; fi
done

echo "$curr" > commit.txt

#git add .
#git commit -m "$curr"

echo "Done"
