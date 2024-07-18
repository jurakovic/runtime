FROM polinux/mkdocs:1.5.2

WORKDIR /

ENV ADD_MODULES="mkdocs-bootstrap mkdocs-gitbook mkdocs-bootstrap4 mkdocs-exclude mkdocs-material mkdocs-awesome-pages-plugin"
ENV FAST_MODE="true"
ENV DEV_ADDR="0.0.0.0:80"
ENV DOCS_DIRECTORY="/botr"

COPY mkdocs.yml ${DOCS_DIRECTORY}/mkdocs.yml
COPY . ${DOCS_DIRECTORY}/docs

RUN rm ${DOCS_DIRECTORY}/docs/mkdocs.yml

CMD ["/usr/bin/python3", "/bootstrap/main.py", "start"]
