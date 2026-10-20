#!/usr/bin/env sh
# Nightly backup of the database and uploaded documents (run from the folder
# that holds docker-compose.yml, e.g. via cron: 0 2 * * * /opt/pro/deploy/backup.sh).
# Copy the output folder off the server (S3, another region, ...): a backup on
# the same disk is not a backup. Documents contain passports and Emirates IDs:
# keep backups encrypted and access-controlled.
set -eu
STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="${BACKUP_DIR:-./backups}/$STAMP"
mkdir -p "$OUT"

docker compose exec -T db pg_dump -U pro -d pro --format=custom > "$OUT/pro.dump"
docker compose run --rm --no-deps -v "$(pwd)/$OUT:/backup" --entrypoint sh api \
  -c "tar -czf /backup/storage.tar.gz -C /app storage"

# Keep 14 days locally.
find "${BACKUP_DIR:-./backups}" -mindepth 1 -maxdepth 1 -type d -mtime +14 -exec rm -rf {} +
echo "Backup written to $OUT"
