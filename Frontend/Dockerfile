# Build:  docker build -t fasco-pro-web Frontend
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
# Rendered to /etc/nginx/conf.d/default.conf at start-up (envsubst fills only
# the variables below; nginx's own $variables are left alone). The defaults
# suit docker-compose; Azure overrides them (deploy/azure/main.bicep).
COPY nginx.conf /etc/nginx/templates/default.conf.template
ENV API_UPSTREAM=http://api:8080 \
    API_HOST_HEADER=\$host \
    FORWARDED_PROTO=\$scheme \
    CLIENT_IP=\$remote_addr
EXPOSE 80
