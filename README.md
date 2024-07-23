
## The Book of the Runtime Build Repo

This repository is a fork of the [dotnet/runtime](https://github.com/dotnet/runtime), specifically created to build [_The Book of the Runtime_](https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr) using MkDocs.

The site built from this fork is hosted on GitHub Pages and can be accessed at https://jurakovic.github.io/runtime/.

### Overview

This repo has two *main* branches:

- [main](https://github.com/jurakovic/runtime/tree/main)
	- used as documentation source for site build
	- kept in sync with upstream main branch
- [docs](https://github.com/jurakovic/runtime/tree/docs)
	- main branch in this repo
	- created as [orphan](https://git-scm.com/docs/git-checkout#Documentation/git-checkout.txt---orphanltnew-branchgt) branch
	- contains built docs together with required scripts and files
