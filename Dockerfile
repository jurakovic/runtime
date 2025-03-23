
# https://github.com/squidfunk/mkdocs-material/releases
# https://hub.docker.com/r/squidfunk/mkdocs-material/tags
# https://github.com/squidfunk/mkdocs-material/blob/master/Dockerfile
# https://pypi.org/project/mkdocs-awesome-pages-plugin/

FROM squidfunk/mkdocs-material:9.5.27

LABEL org.opencontainers.image.source=https://github.com/jurakovic/runtime

# upgrade package if there is no newer docker image
RUN pip install --no-cache-dir 'mkdocs-material==9.5.29' --upgrade

# install required package
RUN pip install --no-cache-dir 'mkdocs-awesome-pages-plugin==2.9.2'
