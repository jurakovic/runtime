#!/bin/bash

#curr=$(git log -n 1 --format="%h" --abbrev=40 -- docs/design/coreclr/botr)
curr=$(curl -s "https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1" | jq -r '.[0].sha')
prev=$(cat commit.txt)

if [ -z "$curr" ] || [ "$curr" = "null" ]; then
  echo "Failed to fetch latest commit"
  exit 1
fi

if [ $curr = $prev ]
then
  echo "There are no changes"
else
  echo "There are changes"
  read -p "Continue with build? (y/n) " yn
fi

if [ -z "$yn" ] || [ "$yn" != "y" ]; then exit 1; fi;

./build.sh

echo "$curr" > commit.txt

git diff --name-only $prev $curr -- docs/design/coreclr/botr
