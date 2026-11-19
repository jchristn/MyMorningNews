FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS production
RUN apk add --no-cache curl
WORKDIR /usr/share/nginx/html
COPY --from=build /app/dist .
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=5s --timeout=5s --retries=2 CMD curl -f http://127.0.0.1/health || exit 1
CMD ["nginx", "-g", "daemon off;"]
