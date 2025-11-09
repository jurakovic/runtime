
# The Book of the Runtime Build Repo

This repository is a fork of the [dotnet/runtime](https://github.com/dotnet/runtime), created solely to build the [_Book of the Runtime_](https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr/README.md) using MkDocs and host it on GitHub Pages.

The site provides easy-to-navigate interface with dark and light themes and interactive search functionality.

It is available at <https://jurakovic.github.io/runtime/>.

## Overview

This repo has two *main* branches:

[docs](https://github.com/jurakovic/runtime/tree/docs)
- contains built docs together with required scripts and files
- created as [orphan](https://git-scm.com/docs/git-checkout#Documentation/git-checkout.txt---orphanltnew-branchgt) branch disconnected from all other branches and commits
- set as default branch in this repo

[main](https://github.com/jurakovic/runtime/tree/main)
- used as documentation source for build
- kept in sync with upstream main branch


## Files

[Dockerfile](./Dockerfile) - defines docker image used for `mkdocs build`

[build.sh](./build.sh) - main script, does docs build and required file modifications before and after build

[check.sh](./check.sh) - checks for [botr docs updates](https://github.com/dotnet/runtime/commits/main/docs/design/coreclr/botr) in upstream repo

[commit.txt](./commit.txt) - tracks [last commit](https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1) used for docs build

[mkdocs.yml](./mkdocs.yml) - mkdocs configuration

## Commands

#### Clone

> [Treeles](https://github.blog/open-source/git/get-up-to-speed-with-partial-clone-and-shallow-clone/) [clone](https://git-scm.com/docs/git-clone#Documentation/git-clone.txt-code--filtercodeemltfilter-specgtem) and [sparse checkout](https://git-scm.com/docs/git-sparse-checkout) are used because we only want `/docs/`, `/.github/`, and `/*` (root) files.  
> Otherwise hundreds MB of data would be downloaded and checked out on `main` branch.

```bash
git clone --branch docs --filter=tree:0 https://github.com/jurakovic/runtime.git --no-checkout
cd runtime
git sparse-checkout set .github docs
git checkout
```

#### Rebase `main`

> Doesn't work with treeless clone. Use [GitHub web UI](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/syncing-a-fork?platform=windows#syncing-a-fork-branch-from-the-web-ui) or normal clone.

```bash
git clone --branch docs https://github.com/jurakovic/runtime.git
cd runtime

git remote add upstream https://github.com/dotnet/runtime.git

git fetch upstream main
git checkout origin/main
git rebase upstream/main

git push
```

> [!IMPORTANT]
> All commands below are run from repo root (`cd runtime`).

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

#### Pull docker image

```
docker pull ghcr.io/jurakovic/mkdocs-botr:latest
```

#### Build docker image from source

```
docker build -t ghcr.io/jurakovic/mkdocs-botr:latest .
```

<!--
#### Push docker images

```
docker build -t ghcr.io/jurakovic/mkdocs-botr:2025-11-09 .
docker tag ghcr.io/jurakovic/mkdocs-botr:2025-11-09 ghcr.io/jurakovic/mkdocs-botr:9.6.23
docker tag ghcr.io/jurakovic/mkdocs-botr:2025-11-09 ghcr.io/jurakovic/mkdocs-botr:latest

export CR_PAT=<PAT>
echo $CR_PAT | docker login ghcr.io -u jurakovic --password-stdin

docker push ghcr.io/jurakovic/mkdocs-botr:2025-11-09
docker push ghcr.io/jurakovic/mkdocs-botr:9.6.23
docker push ghcr.io/jurakovic/mkdocs-botr:latest

git tag 9.6.23
git push origin 9.6.23
```
-->

#### Build docs

```
./build.sh
```

#### Check for updates

```
./check.sh
```


## References

MkDocs  
<https://www.mkdocs.org>  
<https://github.com/mkdocs/mkdocs>  

Material for MkDocs  
<https://squidfunk.github.io/mkdocs-material>  
<https://github.com/squidfunk/mkdocs-material>  
<https://hub.docker.com/r/squidfunk/mkdocs-material>  
<https://pypi.org/project/mkdocs-material/>  

MkDocs Awesome Pages Plugin  
<https://github.com/lukasgeiter/mkdocs-awesome-pages-plugin>  
<https://pypi.org/project/mkdocs-awesome-pages-plugin/>  

#### What others say about BOTR

<https://www.hanselman.com/blog/the-book-of-the-runtime-the-internals-of-the-net-runtime-that-you-wont-find-in-the-documentation>  
<https://mattwarren.org/2018/03/23/Exploring-the-internals-of-the-.NET-Runtime/>  
<https://news.ycombinator.com/item?id=15346747>  
