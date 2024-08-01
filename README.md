
# The Book of the Runtime Build Repo

This repository is a fork of the [dotnet/runtime](https://github.com/dotnet/runtime), created solely to build [_The Book of the Runtime_](https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr/README.md) using MkDocs and host it on GitHub Pages.

It provides easy-to-navigate interface with dark and light themes and interactive search functionality.

Site is available at <https://jurakovic.github.io/runtime/>.

<!-- > This repo only provides GitHub Pages site and does not alter original documentation. For documentation updates please refer to the dotnet/runtime [contributing](https://github.com/dotnet/runtime/blob/main/CONTRIBUTING.md) guidelines. -->

## Overview

This repo has two *main* branches:

- [docs](https://github.com/jurakovic/runtime/tree/docs)
	- contains built docs together with required scripts and files
	- created as [orphan](https://git-scm.com/docs/git-checkout#Documentation/git-checkout.txt---orphanltnew-branchgt) branch disconnected from all the other branches and commits
	- set as default branch in this repo
- [main](https://github.com/jurakovic/runtime/tree/main)
	- used as documentation source for site build
	- kept in sync with upstream main branch

## Commands

#### Clone

> [Treeles](https://github.blog/open-source/git/get-up-to-speed-with-partial-clone-and-shallow-clone/) [clone](https://git-scm.com/docs/git-clone#Documentation/git-clone.txt-code--filtercodeemltfilter-specgtem) and [sparse checkout](https://git-scm.com/docs/git-sparse-checkout) are used because we only want `/docs/` and `/*` (root) files.  
> Otherwise hundreds MB of data would be downloaded and checked out on `main` branch.

```bash
git clone --branch docs --filter=tree:0 https://github.com/jurakovic/runtime.git
cd runtime
git sparse-checkout set docs
```

#### Rebase `main`

```bash
git remote add upstream https://github.com/dotnet/runtime.git

git fetch upstream main
git checkout main
git rebase upstream/main

git push
```

#### Run site locally

> There are many options. Here nginx is used running in docker.

```bash
docker run -d --restart always -p 9903:80 -v ./docs:/usr/share/nginx/html --name botr nginx
```

<!--
# extra commands:
docker rm -f botr
docker run -d --restart always -p 9903:80 -v ./docs:/usr/share/nginx/html --name botr nginx
docker restart botr
-->

Browse <http://localhost:9903>

#### Create docker image

> Used for `mkdocs build`. More details below.

```
docker build -t mkdocs-botr .
```

#### Build docs

```
./build.sh
```

#### Check for updates

```
./check.sh
```

## Files

[Dockerfile](./Dockerfile) - defines docker image used for docs build (ensures *virtually* the same environment regardless of machine used for build)

[build.sh](./build.sh) - does the docs build and all *heavy lifting*

[check.sh](./check.sh) - checks for docs updates in upstream repo

[commit.txt](./commit.txt) - tracks last commit used for docs build

[mkdocs.yml](./mkdocs.yml) - mkdocs configuration

## References

MkDocs  
<https://www.mkdocs.org>  
<https://github.com/mkdocs/mkdocs>  

Material for MkDocs  
<https://squidfunk.github.io/mkdocs-material>  
<https://github.com/squidfunk/mkdocs-material>  

MkDocs Awesome Pages Plugin  
<https://github.com/lukasgeiter/mkdocs-awesome-pages-plugin>  

#### What others say about BOTR

<https://www.hanselman.com/blog/the-book-of-the-runtime-the-internals-of-the-net-runtime-that-you-wont-find-in-the-documentation>  
<https://news.ycombinator.com/item?id=15346747>  
