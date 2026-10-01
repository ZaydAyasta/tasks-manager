FROM node:24.15.0-alpine AS build
WORKDIR /app

COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.29-alpine AS runtime
# The official Nginx entrypoint renders templates at startup. Keeping this
# value as a service variable lets the CSP stay aligned with the API URL used
# for the Vite production build.
COPY src/frontend/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
