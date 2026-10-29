# Headhunter Helm chart

Deploys Headhunter: the ASP.NET Core API (which also serves the Angular client) and, optionally, the
things around it:

- **SQL Server** (`mssql`): a StatefulSet with its data volume and a backup volume (or an NFS export).
  You can also bring your own server (`database.host`).
- **A migration Job** (`migrations`): applies the image's EF Core migrations, after backing up the
  database. It works the same way as GrifballWebApp's chart.
- **sql-exporter** (`sqlExporter`): Prometheus metrics for SQL Server. It has two collectors:
  - server-wide statistics;
  - the plan cache's top statements and wait types.
- **NetworkPolicies** (`networkPolicy`), and a Kubernetes **Ingress** or Traefik **IngressRoute**.

Published to `oci://ghcr.io/noahsurprenant/charts/headhunter`, signed keyless with cosign by
`.github/workflows/helm-chart.yml`. Versions are `<Chart.yaml major.minor>.<workflow run number>`.

## Quick start

```sh
kubectl create namespace headhunter
kubectl create secret generic headhunter-secrets -n headhunter --from-literal=SA_PASSWORD='<a strong password>'
helm install headhunter oci://ghcr.io/noahsurprenant/charts/headhunter -n headhunter \
  -f charts/headhunter/ci/minimal-values.yaml
```

`ci/minimal-values.yaml` is the smallest working install. `ci/full-values.yaml` turns everything on,
the way the homelab runs it.

## Migrations

Every step runs from the app image being deployed. The image carries:

- `/app/efbundle`, the EF Core migrations bundle;
- `/app/migrations.txt`, the list of migrations it contains;
- `sqlcmd`.

So the migrations always match the app. The Job's steps:

1. **wait-for-database:** waits until SQL Server accepts a login and the database is `ONLINE`.
2. **check-pending:** compares `/app/migrations.txt` with `__EFMigrationsHistory`. It **fails** if the
   database has migrations the image doesn't (an older image on a newer schema).
3. **backup** (`migrations.backup.enabled`): if anything is pending and the database exists, writes a
   `COPY_ONLY, COMPRESSION, CHECKSUM` backup to `<migrations.backup.directory>/<prefix>-<UTC time>.bak`.
   SQL Server writes it, to its backup volume. Old backups are not pruned.
4. **migrate:** runs the bundle, or does nothing when nothing is pending.

The Job doesn't retry (`backoffLimit: 0`). Its name carries a hash of its pod spec and the release
revision, so a new image or new settings make a new Job.

**Ordering.** Under Argo CD the sync waves order the deploy: SQL Server (-2), then the Job (-1), then
everything else. A failed migration stops the sync, and the running app is left as it was. Under plain
Helm everything starts together. The app's `wait-for-migrations` init container (`app.waitForMigrations`)
holds it until the database has every migration in its image, without changing anything.

**Credentials.** The migrations connect as the administrator (`database.user`, default `sa`).
`database.connectionString` can give the app a different connection.

## Moving an existing install to the chart

Object names are fixed rather than prefixed with the release name. Each one that holds state or has an
immutable selector can be named to match an install that started as plain manifests:

| | Values |
|---|---|
| StatefulSet `mssql`, selector `app: mssql`, headless Service `mssql-headless` | `mssql.name`, `mssql.headlessServiceName` |
| Data claim `mssql-data-pvc` | `mssql.persistence.claimName` (or `existingClaim`), plus the same `size`, `storageClass`, `accessModes` |
| Backup claim and NFS PersistentVolume | `mssql.backup.claimName`, `mssql.backup.nfs.pvName`, `accessModes` |
| Deployment `headhunter`, selector `app: headhunter` | `app.name` |
| sql-exporter's selector `app.kubernetes.io/name: sql-exporter` | fixed |

**Data protection.** The claims and the PersistentVolume carry:

- `argocd.argoproj.io/sync-options: Prune=false,Delete=false`;
- `helm.sh/resource-policy: keep`.

So neither Argo CD nor `helm uninstall` deletes the database. The NFS PersistentVolume's reclaim
policy is `Retain` by default.

## Tests

- `helm unittest charts/headhunter`: unit tests (`tests/`).
- `APP_IMAGE=headhunter:e2e charts/headhunter/e2e/run.sh`: end to end on a cluster. The script's
  header lists what it covers.
- Pull requests also lint and schema-check every `ci/*-values.yaml`, and run the end-to-end test on
  k3s.
