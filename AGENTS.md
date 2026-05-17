# Agent Notes

- Production deploys must delete old PM2 processes before starting new ones. Use `pm2 delete <name> || true` before copying/starting `monitoring-frontend` or `monitoring-api`; do not rely on `pm2 stop`/`restart` for deploy replacement.
