#!/usr/bin/env bash
#
# The chart on a real cluster, as .github/workflows/helm-chart.yml runs it (k3d; any cluster that
# enforces NetworkPolicies and can run APP_IMAGE will do):
#
#   APP_IMAGE=headhunter:e2e charts/headhunter/e2e/run.sh
#
# One release, through what a deploy meets:
#   1. A first install, on no database: the migration Job creates it (no backup: nothing to back up),
#      and the app waits for it, then serves.
#   2. sql-exporter reads SQL Server, both collectors.
#   3. NetworkPolicies: a pod that isn't the app can't reach SQL Server; one labelled as the app can.
#   4. An upgrade with nothing pending: a new Job that neither backs up nor migrates.
#   5. An upgrade with a migration pending: a backup on SQL Server's backup volume, then that migration.
#   6. A database with a migration the image doesn't have: the Job fails, and so does the upgrade.
set -euo pipefail

CHART="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NS=headhunter-e2e
RELEASE=hh
: "${APP_IMAGE:?APP_IMAGE is required, e.g. headhunter:e2e}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

step() { echo; echo "=== $*"; }
fail() { echo "::error::$*"; diagnostics; exit 1; }
diagnostics() {
  echo "::group::Diagnostics"
  kubectl get all,pvc,networkpolicy -n "$NS" -o wide || true
  kubectl get events -n "$NS" --sort-by=.lastTimestamp | tail -40 || true
  for pod in $(kubectl get pods -n "$NS" -o name); do
    echo "--- $pod"
    kubectl logs -n "$NS" "$pod" --all-containers --prefix --tail=80 || true
  done
  echo "::endgroup::"
}

# SQL as sa, in SQL Server's own pod (its tools, its SA_PASSWORD).
sql() {
  kubectl exec -n "$NS" mssql-0 -c mssql -- bash -c \
    'SQLCMDPASSWORD="$SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -d Headhunter -b -h -1 -W -Q "SET NOCOUNT ON; $1"' _ "$1"
}
latest_job() {
  kubectl get jobs -n "$NS" -l app.kubernetes.io/component=migrations \
    --sort-by=.metadata.creationTimestamp -o name | tail -1
}
job_logs() { kubectl logs -n "$NS" "$(latest_job)" --all-containers --prefix; }
upgrade() { helm upgrade "$RELEASE" "$CHART" -n "$NS" -f "$WORK/values.yaml" --wait --wait-for-jobs --timeout 10m "$@"; }

step "Values and secret"
kubectl create namespace "$NS"
# SQL Server's password policy: upper, lower, digit, symbol, 8+.
password="E2e-$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 20)1a"
kubectl create secret generic headhunter-secrets -n "$NS" --from-literal=SA_PASSWORD="$password"
cat > "$WORK/values.yaml" <<EOF
app:
  image: { repository: "${APP_IMAGE%:*}", tag: "${APP_IMAGE##*:}", pullPolicy: Never }
  resources: { requests: { cpu: 50m, memory: 128Mi }, limits: { cpu: "1", memory: 512Mi } }
mssql:
  acceptEula: true
  resources: { requests: { cpu: 250m, memory: 1Gi }, limits: { cpu: "2", memory: 3Gi } }
  backup: { size: 2Gi }
  persistence: { size: 2Gi }
sqlExporter:
  enabled: true
networkPolicy:
  enabled: true
EOF

step "1. First install, on no database"
helm install "$RELEASE" "$CHART" -n "$NS" -f "$WORK/values.yaml" --wait --wait-for-jobs --timeout 15m \
  || fail "helm install failed"
logs=$(job_logs)
echo "$logs"
grep -q "New database: nothing to back up." <<<"$logs" || fail "The first migration backed up (or skipped the backup step)"
expected=$(kubectl exec -n "$NS" deploy/headhunter -c headhunter -- cat /app/migrations.txt | grep -c .)
applied=$(sql "SELECT COUNT(*) FROM __EFMigrationsHistory" | tr -d '[:space:]')
[ "$applied" = "$expected" ] || fail "The database has $applied migrations, the image $expected"
kubectl logs -n "$NS" deploy/headhunter -c wait-for-migrations | grep -q "is up to date" \
  || fail "The app didn't wait for the migrations"
echo "All $applied migrations applied; the app waited for them."

kubectl port-forward -n "$NS" svc/headhunter 18080:8080 >"$WORK/pf-app.log" 2>&1 &
pf_app=$!
for _ in $(seq 30); do curl -fs localhost:18080/health/ready >/dev/null && break; sleep 1; done
curl -fsS localhost:18080/health/ready >/dev/null || fail "/health/ready isn't OK"
body=$(curl -fsS 'localhost:18080/api/CitySuggestions?query=De') || fail "CitySuggestions failed"
[ "$body" = "[]" ] || fail "CitySuggestions on an empty database returned $body"
kill "$pf_app"
echo "The app serves, and queries the database."

step "2. sql-exporter"
kubectl port-forward -n "$NS" svc/sql-exporter 19399:9399 >"$WORK/pf-exp.log" 2>&1 &
pf_exp=$!
metrics=""
for _ in $(seq 30); do
  metrics=$(curl -fs localhost:19399/metrics || true)
  grep -q '^mssql_query_executions_total{database="Headhunter"' <<<"$metrics" && break
  sleep 2
done
kill "$pf_exp"
grep -q '^mssql_query_executions_total{database="Headhunter"' <<<"$metrics" || fail "No per-statement metrics for Headhunter"
grep -q '^mssql_wait_seconds_total' <<<"$metrics" || fail "No wait metrics"
grep -q '^mssql_page_life_expectancy_seconds' <<<"$metrics" || fail "No mssql_standard metrics"
echo "Both collectors report."

step "3. NetworkPolicies"
probe() {
  kubectl run "probe-$1" -n "$NS" --rm -i --restart=Never --quiet --labels="app=$1" \
    --image=docker.io/library/busybox:1.38 --command -- sh -c 'nc -z -w 5 mssql 1433 && echo open || echo closed'
}
[ "$(probe intruder | tr -d '\r' | tail -1)" = closed ] || fail "A pod that isn't the app reached SQL Server"
[ "$(probe headhunter | tr -d '\r' | tail -1)" = open ] || fail "A pod labelled as the app couldn't reach SQL Server"
echo "SQL Server takes the app, and refuses others."

step "4. Upgrade with nothing pending"
upgrade || fail "helm upgrade failed"
logs=$(job_logs)
echo "$logs"
grep -q "Nothing pending: no backup." <<<"$logs" || fail "It backed up with nothing pending"
grep -q "Nothing to migrate." <<<"$logs" || fail "It ran the bundle with nothing pending"

step "5. Upgrade with a migration pending"
# Undo the image's index migration by hand, so it is pending again.
sql "DROP INDEX IX_Addresses_Latitude_Longitude ON Addresses; DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%[_]AddIndex'"
upgrade || fail "helm upgrade failed"
logs=$(job_logs)
echo "$logs"
grep -q "Pending: .*_AddIndex" <<<"$logs" || fail "The removed migration wasn't pending"
grep -q "BACKUP DATABASE successfully processed" <<<"$logs" || fail "No backup before migrating"
grep -q "Applying migration '.*_AddIndex'" <<<"$logs" || fail "The pending migration wasn't applied"
kubectl exec -n "$NS" mssql-0 -c mssql -- ls /var/opt/mssql/backup | grep -q '^headhunter-premigrate-.*\.bak$' \
  || fail "No backup file on the backup volume"
[ "$(sql "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_Addresses_Latitude_Longitude'" | tr -d '[:space:]')" = 1 ] \
  || fail "The index wasn't recreated"
echo "Backed up, then migrated."

step "6. A database ahead of the image"
sql "INSERT INTO __EFMigrationsHistory VALUES ('29990101000000_Future', '10.0.0')"
if upgrade --timeout 5m; then fail "The upgrade succeeded on a database ahead of the image"; fi
logs=$(job_logs)
echo "$logs"
grep -q "has migrations this image doesn't: 29990101000000_Future" <<<"$logs" || fail "The Job failed for another reason"
grep -q "Applying migration" <<<"$logs" && fail "It migrated anyway"
sql "DELETE FROM __EFMigrationsHistory WHERE MigrationId = '29990101000000_Future'"
echo "Refused, without touching the database."

echo
echo "All end-to-end checks passed."
