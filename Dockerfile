
# https://github.com/squidfunk/mkdocs-material/releases
# https://hub.docker.com/r/squidfunk/mkdocs-material/tags
# https://github.com/squidfunk/mkdocs-material/blob/master/Dockerfile
# https://pypi.org/project/mkdocs-material/#history
# https://pypi.org/project/mkdocs-awesome-pages-plugin/#history

FROM squidfunk/mkdocs-material:9.6.23

LABEL org.opencontainers.image.source=https://github.com/jurakovic/runtime

# upgrade package if there is no newer docker image
RUN pip install --no-cache-dir 'mkdocs-material==9.6.23' --upgrade

# install required package
RUN pip install --no-cache-dir 'mkdocs-awesome-pages-plugin==2.10.1'
