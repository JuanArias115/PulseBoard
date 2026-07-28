# GitHub Actions

`ci.yml` runs web and API tests on pushes and pull requests.

`deploy.yml` runs only on `main`, builds both Docker images, publishes them to GHCR, copies `docker-compose.prod.yml` to `/opt/pulseboard`, and restarts only the PulseBoard containers.
