FROM node:22.22.3-bookworm-slim@sha256:e21fc383b50d5347dc7a9f1cae45b8f4e2f0d39f7ade28e4eef7d2934522b752 AS build
WORKDIR /src
RUN npm install --global npm@11.6.2
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.28-alpine@sha256:7377697a821c131a924a7105fafbe7414db4e9fcc77a6f08f776f33f141ec3f8 AS web
COPY deploy/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /src/dist/frontend/browser/ /usr/share/nginx/html/
EXPOSE 8080
# The upstream image runs as its unprivileged nginx user, not root.
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=6 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health/ready || exit 1
