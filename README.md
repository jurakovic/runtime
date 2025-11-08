
# The Book of the Runtime Build Repo

This repository is a fork of the [dotnet/runtime](https://github.com/dotnet/runtime), created solely to build the [_Book of the Runtime_](https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr/README.md) using MkDocs and host it on GitHub Pages.

The site provides easy-to-navigate interface with dark and light themes and interactive search functionality.

It is available at <https://jurakovic.github.io/runtime/>.

## Files

[Dockerfile](./Dockerfile) - defines docker image used for `mkdocs build`

[build.sh](./build.sh) - does docs build and required file modifications before and after build

[check.sh](./check.sh) - checks for [botr docs updates](https://github.com/dotnet/runtime/commits/main/docs/design/coreclr/botr) in upstream repo

[commit.txt](./commit.txt) - tracks [last commit](https://api.github.com/repos/dotnet/runtime/commits?path=docs/design/coreclr/botr&per_page=1) used for docs build

[mkdocs.yml](./mkdocs.yml) - mkdocs configuration

## Commands

#### Clone

> [Treeles](https://github.blog/open-source/git/get-up-to-speed-with-partial-clone-and-shallow-clone/) [clone](https://git-scm.com/docs/git-clone#Documentation/git-clone.txt-code--filtercodeemltfilter-specgtem) and [sparse checkout](https://git-scm.com/docs/git-sparse-checkout) are used because we only want `/docs/` and `/*` (root) files.  
> Otherwise hundreds MB of data would be downloaded and checked out on `main` branch.

```bash
git clone --branch docs --filter=tree:0 https://github.com/jurakovic/runtime.git
cd runtime
git sparse-checkout set .github docs
```

> [!TIP]
> All commands below are run from repo root (`cd runtime`).

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

#### Pull docker image

```
docker pull ghcr.io/jurakovic/mkdocs-botr:latest
```

#### Build docker image from source

```
docker build -t ghcr.io/jurakovic/mkdocs-botr:9.6.23 .
docker tag ghcr.io/jurakovic/mkdocs-botr:9.6.23 ghcr.io/jurakovic/mkdocs-botr:latest
```

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

MkDocs Awesome Pages Plugin  
<https://github.com/lukasgeiter/mkdocs-awesome-pages-plugin>  

#### What others say about BOTR

<https://www.hanselman.com/blog/the-book-of-the-runtime-the-internals-of-the-net-runtime-that-you-wont-find-in-the-documentation>  
<https://mattwarren.org/2018/03/23/Exploring-the-internals-of-the-.NET-Runtime/>  
<https://news.ycombinator.com/item?id=15346747>  
