#!/usr/bin/env sh
# Starts the Cheater Watcher stack. On first run it creates .env from .env.example
# (with a random PostgreSQL password) so you don't have to configure anything by hand.

set -e
cd "$(dirname "$0")"

if [ -f .env ]; then
  echo "Using existing .env"
else
  cp .env.example .env
  password=$(head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n')
  sed -i.bak "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$password/" .env
  rm -f .env.bak
  echo "Created .env from .env.example with a random POSTGRES_PASSWORD"
fi

docker compose up -d --build