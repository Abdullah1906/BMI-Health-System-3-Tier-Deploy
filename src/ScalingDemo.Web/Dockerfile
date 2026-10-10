FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first so this layer is cached until package files change.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:1.28-alpine AS runtime

COPY nginx/nginx.conf /etc/nginx/nginx.conf
COPY nginx/upstream.conf /etc/nginx/upstream.conf
COPY --from=build /app/dist/scaling-demo-web/browser /usr/share/nginx/html

EXPOSE 80
