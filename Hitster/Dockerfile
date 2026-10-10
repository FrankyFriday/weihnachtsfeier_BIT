FROM node:24-alpine

ENV NODE_ENV=production
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --include=dev

COPY src ./src
COPY public ./public
COPY data/songs.json ./data/songs.json

USER node
EXPOSE 3000

CMD ["npm", "start"]
