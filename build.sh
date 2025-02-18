#!/bin/bash

# save current branch
branch=$(git branch --show-current)

echo "Checking out 'main' branch"
git checkout main
git pull

# clear any leftovers
rm -rf site

# temp config
echo "$(git show $branch:mkdocs.yml)" > mkdocs.yml

# copy out-of-scope files
cp docs/design/coreclr/botr/../jit/ryujit-overview.md docs/design/coreclr/botr/ryujit-overview.md
cp docs/design/coreclr/botr/../jit/porting-ryujit.md docs/design/coreclr/botr/porting-ryujit.md

# hide toc on all pages (no other proposed solutions work)
mapfile -t files < <(find docs/design/coreclr/botr -type f -iwholename "*.md")
for file in "${files[@]}"; do
  sed -i '1s/^/---\nhide:\n  - toc\n---\n/' "$file"
  sed -i 's|(\.\./\.\./\.\./\.\./|(https://github.com/dotnet/runtime/blob/main/|g' "$file"
  sed -i 's|]: \.\./\.\./\.\./\.\./|]: https://github.com/dotnet/runtime/blob/main/|g' "$file"
  sed -i 's|(\.\./\.\./\.\./|(https://github.com/dotnet/runtime/blob/main/docs/|g' "$file"
  sed -i 's|]: \.\./\.\./\.\./|]: https://github.com/dotnet/runtime/blob/main/docs/|g' "$file"
  sed -i 's|(\.\./\.\./|(https://github.com/dotnet/runtime/blob/main/docs/design/|g' "$file"
  sed -i 's|]: \.\./\.\./|]: https://github.com/dotnet/runtime/blob/main/docs/design/|g' "$file"
  sed -i 's|(\.\./|(https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/|g' "$file"
  sed -i 's|]: \.\./|]: https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/|g' "$file"
  sed -i -r 's|(\.\.\/jit\/)(\.*\.md)|\2|g' "$file"
done

echo "Staring mkdocs build"
docker run --rm -it -v ${PWD}:/docs mkdocs-botr build
#for debugging:
#docker run --rm -it -v ${PWD}:/docs --entrypoint /bin/sh mkdocs-botr

# undoing temp changes
rm mkdocs.yml
rm docs/design/coreclr/botr/ryujit-overview.md
rm docs/design/coreclr/botr/porting-ryujit.md
git restore '*.md'

echo "Checking out '$branch' branch"
git checkout $branch

# clear old build
rm -rf docs

# rename
mv site docs

# change dotnet repo to fork; fix api url
find docs -type f -iwholename "*.html" -exec sed -i -r 's|(href="https://github.com/)(dotnet)(/runtime" title="Go to repository")|\1jurakovic\3|' {} +
find docs/assets/javascripts -type f -iwholename "*.min.js" -exec sed -i -r 's|(https://api.github.com/repos/)(\$\{e\}/\$\{t\})|\1dotnet/runtime|' {} +

# change view url from raw to github
find docs -type f -iwholename "*.html" -exec sed -i -r 's|(href="https://github\.com/dotnet/runtime/)(raw)(.*" title="View source of this page")|\1blob\3 target="_blank"|' {} +
sed -i -r 's|(href="https://github\.com/dotnet/runtime/blob/main/docs/design/coreclr/)(botr)(.*" title="View source of this page")|\1jit\3"|' docs/ryujit-overview/*.html docs/porting-ryujit/*.html

# fix index urls
sed -i -r 's|(href=")(../botr)(">All Book of the Runtime \(BOTR\) chapters on GitHub)|\1https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr" target="_blank\3|' docs/index.html

# add footer url
text='in <a href="https://github.com/jurakovic/runtime" target="_blank">jurakovic/runtime</a>'
mapfile -t files < <(find docs -type f -iwholename "*.html")
for file in "${files[@]}"; do
  total_lines=$(wc -l < "$file")
  insert_line=$((total_lines - 44))
  sed -i "${insert_line}i$text" "$file"
done

echo "Done"
