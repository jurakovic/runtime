#!/bin/bash

function add_worktree {
  echo "Adding new 'dotnet' worktree with 'main' branch"
  git worktree add dotnet main
  cd dotnet
  git pull
}

function copy_mkdocs_config {
  cp ../mkdocs.yml .
  cp ../custom.css docs/design/coreclr/botr/custom.css
  mkdir -p overrides/partials
  cp ../favicon.svg docs/design/coreclr/botr/favicon.svg
  cp ../copyright.html overrides/partials/copyright.html
}

function pre_build {
  # copy out-of-scope files
  cp docs/design/coreclr/botr/../jit/ryujit-overview.md docs/design/coreclr/botr/ryujit-overview.md
  cp docs/design/coreclr/botr/../jit/porting-ryujit.md docs/design/coreclr/botr/porting-ryujit.md

  # fix profiling.md; e.g. List<int> => List&lt;int&gt;
  sed -i -r 's/(\w+)(<)([a-zA-Z,]+)(>)/\1\&lt;\3\&gt;/g' docs/design/coreclr/botr/profiling.md

  # temp fix to images path in jit file
  sed -i -r 's;]\(images;]\(https://raw.githubusercontent.com/dotnet/runtime/refs/heads/main/docs/design/coreclr/jit/images;g' docs/design/coreclr/botr/ryujit-overview.md

  mapfile -t files < <(find docs/design/coreclr/botr -type f -iwholename "*.md")
  for file in "${files[@]}"; do
    # hide toc on all pages (no other proposed solutions work)
    sed -i '1s/^/---\nhide:\n  - toc\n---\n/' "$file"
    # update links to jit files, because they are copied to botr dir (cp commands above)
    sed -i -r 's;(\.\.\/jit\/)(.*\.md);\2;g' "$file"
    # change relative links for out-of-scope files to github
    sed -i -r 's;(\(|]: )\.\./\.\./\.\./\.\./;\1https://github.com/dotnet/runtime/blob/main/;g' "$file"
    sed -i -r 's;(\(|]: )\.\./\.\./\.\./;\1https://github.com/dotnet/runtime/blob/main/docs/;g' "$file"
    sed -i -r 's;(\(|]: )\.\./\.\./;\1https://github.com/dotnet/runtime/blob/main/docs/design/;g' "$file"
    sed -i -r 's;(\(|]: )\.\./;\1https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/;g' "$file"
  done
}

function build {
  echo "Staring mkdocs build"

  # clear any leftovers
  rm -rf site

  docker run --rm -v ${PWD}:/docs ghcr.io/jurakovic/mkdocs-botr:latest build </dev/null
  #for debugging:
  #docker run --rm -it -v ${PWD}:/docs --entrypoint /bin/sh ghcr.io/jurakovic/mkdocs-botr:latest

  # back to repo root
  cd ..

  # clear old build
  rm -rf docs

  # move to docs dir
  mv dotnet/site docs
}

function post_build {
  # change dotnet repo to fork; fix api url
  find docs -type f -iwholename "*.html" -exec sed -i -r 's|(href="https://github.com/)(dotnet)(/runtime" title="Go to repository")|\1jurakovic\3|' {} +
  find docs/assets/javascripts -type f -iwholename "*.min.js" -exec sed -i -r 's|(https://api.github.com/repos/)(\$\{e\}/\$\{t\})|\1dotnet/runtime|' {} +

  # change view url from raw to github
  find docs -type f -iwholename "*.html" -exec sed -i -r 's|(href="https://github\.com/dotnet/runtime/)(raw)(.*" title="View source of this page")|\1blob\3 target="_blank"|' {} +
  sed -i -r 's|(href="https://github\.com/dotnet/runtime/blob/main/docs/design/coreclr/)(botr)(.*" title="View source of this page")|\1jit\3"|' docs/ryujit-overview/*.html docs/porting-ryujit/*.html

  # fix index urls
  sed -i -r 's|(">All Book of the Runtime \(BOTR\) chapters on GitHub)|" target="_blank\1|' docs/index.html
}

function remove_worktree {
  echo "Removing 'dotnet' worktree"
  rm -rf dotnet
  git worktree remove dotnet --force
}

function main {
  cd dotnet
  #add_worktree
  copy_mkdocs_config
  #pre_build
  build
  post_build
  #remove_worktree
  echo "Done"
}

main
