#!/bin/bash

# save current branch
branch=$(git branch --show-current)

# save config for later
mkdocs_config=$(cat mkdocs.yml)

echo "Checking out 'main' branch"
git checkout main

# clear any leftovers
rm -rf site

# temp file
echo "$mkdocs_config" > mkdocs.yml

# hide toc on all pages (no other proposed solutions work)
mapfile -t files < <(find docs/design/coreclr/botr -type f -iwholename "*.md")
for file in "${files[@]}"; do
  sed -i '1s;^;---\nhide:\n  - toc\n---\n;' "$file"
done

echo "Staring mkdocs build"
docker run --rm -it -v ${PWD}:/docs mkdocs-botr build
#for debugging:
#docker run --rm -it -v ${PWD}:/docs --entrypoint /bin/sh mkdocs-botr

# undoing temp changes
rm mkdocs.yml
git restore '*.md'

echo "Checking out '$branch' branch"
git checkout $branch

# clear old build
rm -rf docs

# rename
mv site docs

# change dotnet repo to fork; fix api url
find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github.com\/)(dotnet)(\/runtime" title="Go to repository")/\1jurakovic\3/' {} +
find docs/assets/javascripts -type f -iwholename "*.min.js" -exec sed -i -r 's/(https:\/\/api.github.com\/repos\/)(\$\{e\}\/\$\{t\})/\1dotnet\/runtime/' {} +

# change view url from raw to github
find docs -type f -iwholename "*.html" -exec sed -i -r 's/(href="https:\/\/github\.com\/dotnet\/runtime\/)(raw)(.*" title="View source of this page")/\1blob\3/' {} +

# fix chapters url
sed -i -r 's/(href=")(..\/botr)(">All Book of the Runtime \(BOTR\) chapters on GitHub)/\1https:\/\/github.com\/dotnet\/runtime\/blob\/main\/docs\/design\/coreclr\/botr" target="_blank\3/' docs/index.html

# add footer url
text='in <a href="https://github.com/jurakovic/runtime" target="_blank" rel="noopener">jurakovic/runtime</a>'
mapfile -t files < <(find docs -type f -iwholename "*.html")
for file in "${files[@]}"; do
  total_lines=$(wc -l < "$file")
  insert_line=$((total_lines - 44))
  if [ "$insert_line" -gt 0 ]; then sed -i "${insert_line}i$text" "$file"; fi
done

echo "Done"
