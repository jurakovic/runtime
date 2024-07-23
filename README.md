
## The Book of the Runtime Build Repo

This repository is a fork of the [dotnet/runtime](https://github.com/dotnet/runtime), created solely to build [_The Book of the Runtime_](https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr) using MkDocs.

The site built from this fork is hosted on GitHub Pages and can be accessed at <https://jurakovic.github.io/runtime/>.  
It provides easy-to-navigate interface with interactive search functionality.

<!-- This repo only provides GitHub Pages site and does not alter original documentation. For any content-related issues refer to the dotnet/runtime [contributing](https://github.com/dotnet/runtime/blob/main/CONTRIBUTING.md) guidelines. -->

### Overview

This repo has two *main* branches:

- [main](https://github.com/jurakovic/runtime/tree/main)
	- used as documentation source for site build
	- kept in sync with upstream main branch
- [docs](https://github.com/jurakovic/runtime/tree/docs)
	- main branch in this repo
	- created as [orphan](https://git-scm.com/docs/git-checkout#Documentation/git-checkout.txt---orphanltnew-branchgt) branch
	- contains built docs together with required scripts and files

### Commands

#### Clone

> [Treeles](https://github.blog/open-source/git/get-up-to-speed-with-partial-clone-and-shallow-clone/) [clone](https://git-scm.com/docs/git-clone#Documentation/git-clone.txt-code--filtercodeemltfilter-specgtem) and [sparse checkout](https://git-scm.com/docs/git-sparse-checkout) are used because we only want `/docs/` and `/*` (root) files. Otherwise all ~2 GB files would be checked out.

```bash
git clone --branch main --no-checkout --filter=tree:0 https://github.com/jurakovic/runtime.git t2
cd runtime
git sparse-checkout set docs
git checkout docs
```

### Run locally

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

### Create docker image

```
docker build . -t mkdocs-botr
```


### Files

TODO

### References

TODO
