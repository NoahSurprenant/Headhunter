{{/* Labels on every object. */}}
{{- define "headhunter.labels" -}}
app.kubernetes.io/part-of: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Labels on pod templates: headhunter.labels without the chart's version, so a new chart version alone
restarts nothing (nor makes a new migration Job). No app.kubernetes.io/name: each workload's selector
(app, or app.kubernetes.io/name for sql-exporter) is its own and can't change on an existing release.
*/}}
{{- define "headhunter.podLabels" -}}
app.kubernetes.io/part-of: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
repository:tag, repository:tag@digest, or repository@digest. tag may itself carry a digest
(tag@sha256:... or sha256:...); digest, when set, replaces it. Takes an image values block.
*/}}
{{- define "headhunter.image" -}}
{{- $ref := .repository -}}
{{- $tag := toString (.tag | default "") -}}
{{- if .digest }}{{ $tag = regexReplaceAll "@.*$" $tag "" }}{{ if hasPrefix "sha256:" $tag }}{{ $tag = "" }}{{ end }}{{ end -}}
{{- if hasPrefix "sha256:" $tag -}}
{{- printf "%s@%s" $ref $tag -}}
{{- else -}}
{{- if $tag }}{{ $ref = printf "%s:%s" $ref $tag }}{{ end -}}
{{- if .digest }}{{ $ref = printf "%s@%s" $ref .digest }}{{ end -}}
{{- $ref -}}
{{- end -}}
{{- end }}

{{- define "headhunter.appImage" -}}
{{- include "headhunter.image" .Values.app.image -}}
{{- end }}

{{/* A Secret's name: the one given, or secret.name. Takes dict "secretName" and "root". */}}
{{- define "headhunter.secretName" -}}
{{- .secretName | default .root.Values.secret.name -}}
{{- end }}

{{/* "true" when the database is PostgreSQL (database.provider Postgres); nothing for SQL Server. */}}
{{- define "headhunter.postgres" -}}
{{- $p := .Values.database.provider | default "SqlServer" -}}
{{- if not (has $p (list "SqlServer" "Postgres")) }}{{ fail "database.provider must be SqlServer or Postgres" }}{{ end -}}
{{- if eq $p "Postgres" }}true{{ end -}}
{{- end }}

{{/* One value per provider. Takes dict "root", "sql", "pg". */}}
{{- define "headhunter.byProvider" -}}
{{- if include "headhunter.postgres" .root }}{{ .pg }}{{ else }}{{ .sql }}{{ end -}}
{{- end }}

{{/* The chart's own database server is on: the chart's PostgreSQL or SQL Server, by provider. */}}
{{- define "headhunter.ownDatabase" -}}
{{- if include "headhunter.postgres" . }}{{ if .Values.postgres.enabled }}true{{ end }}{{ else }}{{ if .Values.mssql.enabled }}true{{ end }}{{ end -}}
{{- end }}

{{- define "headhunter.dbHost" -}}
{{- if .Values.database.host -}}
{{- .Values.database.host -}}
{{- else if include "headhunter.ownDatabase" . -}}
{{- include "headhunter.byProvider" (dict "root" . "sql" .Values.mssql.service.name "pg" .Values.postgres.service.name) -}}
{{- else -}}
{{- fail (printf "database.host is required with %s false" (include "headhunter.byProvider" (dict "root" . "sql" "mssql.enabled" "pg" "postgres.enabled"))) -}}
{{- end -}}
{{- end }}

{{- define "headhunter.dbPort" -}}
{{- if .Values.database.port -}}
{{- .Values.database.port -}}
{{- else if and (include "headhunter.ownDatabase" .) (not .Values.database.host) -}}
{{- include "headhunter.byProvider" (dict "root" . "sql" .Values.mssql.service.port "pg" .Values.postgres.service.port) -}}
{{- else -}}
{{- include "headhunter.byProvider" (dict "root" . "sql" "1433" "pg" "5432") -}}
{{- end -}}
{{- end }}

{{/* The administrator: database.user, else sa (SQL Server) or postgres. */}}
{{- define "headhunter.dbUser" -}}
{{- .Values.database.user | default (include "headhunter.byProvider" (dict "root" . "sql" "sa" "pg" "postgres")) -}}
{{- end }}

{{/* The connection string's options: database.options, else the provider's default. */}}
{{- define "headhunter.dbOptions" -}}
{{- .Values.database.options | default (include "headhunter.byProvider" (dict "root" . "sql" "Encrypt=True;TrustServerCertificate=True" "pg" "SSL Mode=Prefer")) | trimSuffix ";" -}}
{{- end }}

{{/* The administrator's password: database.password, else the chart's database's own (mssql.saPassword, postgres.password). */}}
{{- define "headhunter.dbPasswordRef" -}}
{{- $p := .Values.database.password -}}
{{- $own := include "headhunter.byProvider" (dict "root" . "sql" "mssql" "pg" "postgres") -}}
{{- $secretName := $p.secretName | default (ternary .Values.postgres.password.secretName .Values.mssql.saPassword.secretName (eq $own "postgres")) -}}
{{- $key := $p.key | default (ternary .Values.postgres.password.key .Values.mssql.saPassword.key (eq $own "postgres")) -}}
name: {{ include "headhunter.secretName" (dict "secretName" $secretName "root" .) }}
key: {{ $key }}
{{- end }}

{{/*
The administrator's connection for sqlcmd or psql: DB_HOST, DB_PORT, DB_NAME, DB_USER and the password in
SQLCMDPASSWORD (sqlcmd) or PGPASSWORD (libpq, which also reads PGHOST, PGPORT and PGUSER), so it never goes
on a command line.
*/}}
{{- define "headhunter.dbEnv" -}}
- name: DB_HOST
  value: {{ include "headhunter.dbHost" . | quote }}
- name: DB_PORT
  value: {{ include "headhunter.dbPort" . | quote }}
- name: DB_NAME
  value: {{ .Values.database.name | quote }}
- name: DB_USER
  value: {{ include "headhunter.dbUser" . | quote }}
{{- if include "headhunter.postgres" . }}
- name: PGHOST
  value: {{ include "headhunter.dbHost" . | quote }}
- name: PGPORT
  value: {{ include "headhunter.dbPort" . | quote }}
- name: PGUSER
  value: {{ include "headhunter.dbUser" . | quote }}
- name: PGCONNECT_TIMEOUT
  value: "5"
- name: PGPASSWORD
{{- else }}
- name: SQLCMDPASSWORD
{{- end }}
  valueFrom:
    secretKeyRef:
      {{- include "headhunter.dbPasswordRef" . | nindent 6 }}
{{- end }}

{{/* The connection string for the administrator; $(DB_PASSWORD) is expanded by Kubernetes. Takes dict "root" and "suffix". */}}
{{- define "headhunter.builtConnectionString" -}}
{{- $r := .root -}}
{{- if include "headhunter.postgres" $r -}}
{{- printf "Host=%s;Port=%s;Database=%s;Username=%s;Password=\"$(DB_PASSWORD)\";%s%s" (include "headhunter.dbHost" $r) (include "headhunter.dbPort" $r) $r.Values.database.name (include "headhunter.dbUser" $r) (include "headhunter.dbOptions" $r) .suffix -}}
{{- else -}}
{{- printf "Server=%s,%s;Database=%s;User Id=%s;Password=\"$(DB_PASSWORD)\";%s%s" (include "headhunter.dbHost" $r) (include "headhunter.dbPort" $r) $r.Values.database.name (include "headhunter.dbUser" $r) (include "headhunter.dbOptions" $r) .suffix -}}
{{- end -}}
{{- end }}

{{/* Database__Provider, for PostgreSQL (SQL Server is the app's default). */}}
{{- define "headhunter.providerEnv" -}}
{{- if include "headhunter.postgres" . }}
- name: Database__Provider
  value: Postgres
{{- end }}
{{- end }}

{{/*
ConnectionStrings__Headhunter: database.connectionString's Secret, or one built from the administrator
connection. Built, the password comes in through DB_PASSWORD ($(DB_PASSWORD), expanded by Kubernetes),
quoted, so it may contain ';' but not '"'.
*/}}
{{- define "headhunter.connectionEnv" -}}
{{- $cs := .Values.database.connectionString -}}
{{- with include "headhunter.providerEnv" . }}{{ . | trim }}
{{ end -}}
{{- if $cs.key -}}
- name: ConnectionStrings__Headhunter
  valueFrom:
    secretKeyRef:
      name: {{ include "headhunter.secretName" (dict "secretName" $cs.secretName "root" .) }}
      key: {{ $cs.key }}
{{- else -}}
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      {{- include "headhunter.dbPasswordRef" . | nindent 6 }}
- name: ConnectionStrings__Headhunter
  value: {{ include "headhunter.builtConnectionString" (dict "root" . "suffix" "") | quote }}
{{- end -}}
{{- end }}

{{/*
The migrations always connect as the administrator (they change the schema), whatever
database.connectionString gives the app.
*/}}
{{- define "headhunter.migrationConnectionEnv" -}}
{{- with include "headhunter.providerEnv" . }}{{ . | trim }}
{{ end -}}
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      {{- include "headhunter.dbPasswordRef" . | nindent 6 }}
- name: ConnectionStrings__Headhunter
  value: {{ include "headhunter.builtConnectionString" (dict "root" . "suffix" (printf ";Command Timeout=%d" (int .Values.migrations.activeDeadlineSeconds))) | quote }}
{{- end }}

{{/*
One setting as an environment variable value: strings as they are, numbers without float notation.
Kubernetes expands $(VAR) in env values, so '$' is doubled to keep a value literal.
*/}}
{{- define "headhunter.configValue" -}}
{{- $v := .value -}}
{{- if kindIs "float64" $v -}}
  {{- if eq $v (floor $v) -}}
    {{- printf "%.0f" $v -}}
  {{- else -}}
    {{- toString $v -}}
  {{- end -}}
{{- else -}}
  {{- toString $v | replace "$" "$$" -}}
{{- end -}}
{{- end }}

{{/*
appsettings-style nested settings as env entries: maps become Section__Key, lists Section__0, null is
skipped. Takes dict "name" (prefix, "" at the top) and "value".
*/}}
{{- define "headhunter.configEnv" -}}
{{- $name := .name -}}
{{- if kindIs "map" .value -}}
  {{- range $k, $v := .value -}}
    {{- include "headhunter.configEnv" (dict "name" (ternary $k (printf "%s__%s" $name $k) (eq $name "")) "value" $v) -}}
  {{- end -}}
{{- else if kindIs "slice" .value -}}
  {{- range $i, $v := .value -}}
    {{- include "headhunter.configEnv" (dict "name" (printf "%s__%d" $name $i) "value" $v) -}}
  {{- end -}}
{{- else if not (kindIs "invalid" .value) -}}
{{- printf "\n- name: %s\n  value: %s" ($name | quote) (include "headhunter.configValue" (dict "name" $name "value" .value) | quote) -}}
{{- end -}}
{{- end }}

{{/*
With onDemand and the chart's SQL Server, asks Sablier to start the group (its poke strategy answers at once), as a
visit would, so the migration Job, run while nobody visits, finds the database up. It retries for a minute: a network
policy may drop the first tries. Nothing otherwise. The app's own wait needs none: Sablier started it with the group.
*/}}
{{- define "headhunter.pokeSablierScript" -}}
{{- if include "headhunter.onDemandWakesDatabase" . }}
{{- $url := printf "%s/api/strategies/poke?group=%s&session_duration=%s" (include "headhunter.sablierUrl" .) (include "headhunter.onDemandGroup" . | urlquery) (urlquery .Values.onDemand.sessionDuration) }}
poke_end=$(( $(date +%s) + 60 ))
until timeout 12 wget -q -T 10 -O /dev/null {{ $url | squote }}; do
  if [ "$(date +%s)" -ge "$poke_end" ]; then echo "Sablier didn't answer ({{ include "headhunter.sablierUrl" . }}): waiting for the database anyway."; break; fi
  sleep 2
done
{{- end }}
{{- end }}

{{/*
Until SQL Server accepts a login and the database, if it exists, is ONLINE (SQL Server accepts logins
before it has finished recovering its databases after a restart). Fails after WAIT_SECONDS. Needs
headhunter.dbEnv.
*/}}
{{- define "headhunter.mssqlWaitForDatabaseScript" -}}
end=$(( $(date +%s) + WAIT_SECONDS ))
# A function, not inline in $(...): BusyBox sh would run $(DB_NAME) as a command there.
database_state() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -C -b -l 5 -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    SELECT CASE WHEN DB_ID(N'\$$(DB_NAME)') IS NULL THEN 'ONLINE'
      ELSE CONVERT(nvarchar(60), DATABASEPROPERTYEX(N'\$$(DB_NAME)', 'Status')) END" 2>&1
}
last=""
until state=$(database_state) && [ "$state" = ONLINE ]; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "Database $DB_NAME not ready: $(echo "$state" | head -c 300)"; exit 1; fi
  msg="Waiting for SQL Server: $(echo "$state" | head -c 300 | tr '\n' ' ')"
  [ "$msg" = "$last" ] || echo "$msg"
  last=$msg
  sleep 5
done
echo "SQL Server is up."
{{- end }}

{{/* The migration IDs the database has, into /work/applied.txt (nothing if it doesn't exist yet). */}}
{{- define "headhunter.mssqlAppliedMigrationsQuery" -}}
sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    IF DB_ID(N'\$$(DB_NAME)') IS NOT NULL AND OBJECT_ID(N'[\$$(DB_NAME)].dbo.__EFMigrationsHistory') IS NOT NULL
      EXEC(N'SELECT MigrationId FROM [\$$(DB_NAME)].dbo.__EFMigrationsHistory');" \
  > /work/applied.txt
{{- end }}


{{/* The database's wait, by provider (needs headhunter.dbEnv and WAIT_SECONDS). */}}
{{- define "headhunter.waitForDatabaseScript" -}}
{{- if include "headhunter.postgres" . }}{{ include "headhunter.pgWaitForDatabaseScript" . }}{{ else }}{{ include "headhunter.mssqlWaitForDatabaseScript" . }}{{ end -}}
{{- end }}

{{/*
Until PostgreSQL answers a query as the administrator (it refuses connections while it starts, or
recovers after an unclean stop). Fails after WAIT_SECONDS. libpq reads the connection from PGHOST, PGPORT,
PGUSER and PGPASSWORD (headhunter.dbEnv).
*/}}
{{- define "headhunter.pgWaitForDatabaseScript" -}}
end=$(( $(date +%s) + WAIT_SECONDS ))
# A function, not inline in $(...): BusyBox sh would run $(DB_NAME) as a command there.
server_state() { psql -X -A -t -q -d postgres -c 'SELECT 1' 2>&1; }
last=""
until state=$(server_state) && [ "$state" = 1 ]; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "PostgreSQL not ready: $(echo "$state" | head -c 300)"; exit 1; fi
  msg="Waiting for PostgreSQL: $(echo "$state" | head -c 300 | tr '\n' ' ')"
  [ "$msg" = "$last" ] || echo "$msg"
  last=$msg
  sleep 5
done
echo "PostgreSQL is up."
{{- end }}

{{/* The migration IDs the database has, into /work/applied.txt, by provider. */}}
{{- define "headhunter.appliedMigrationsQuery" -}}
{{- if include "headhunter.postgres" . }}{{ include "headhunter.pgAppliedMigrationsQuery" . }}{{ else }}{{ include "headhunter.mssqlAppliedMigrationsQuery" . }}{{ end -}}
{{- end }}

{{/*
PostgreSQL's: nothing if the database or its history table doesn't exist yet. psql quotes the name
(:'db' a literal, :"db" an identifier); the script goes through stdin with a quoted here-document, so the
shell expands nothing in it.
*/}}
{{- define "headhunter.pgAppliedMigrationsQuery" -}}
psql -X -A -t -q -v ON_ERROR_STOP=1 -d postgres -v db="$DB_NAME" > /work/applied.txt <<'SQL'
SELECT NOT EXISTS (SELECT FROM pg_database WHERE datname = :'db') AS missing \gset
\if :missing
\else
\connect :"db"
SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL AS history \gset
\if :history
SELECT "MigrationId" FROM public."__EFMigrationsHistory";
\endif
\endif
SQL
{{- end }}

{{/* The image's list of migrations for the provider's migrations project. */}}
{{- define "headhunter.migrationsFile" -}}
{{- include "headhunter.byProvider" (dict "root" . "sql" "/app/migrations.txt" "pg" "/app/migrations-postgres.txt") -}}
{{- end }}

{{- define "headhunter.migrationBundle" -}}
{{- include "headhunter.byProvider" (dict "root" . "sql" "/app/efbundle" "pg" "/app/efbundle-postgres") -}}
{{- end }}

{{/*
The image's migrations the database lacks (/work/applied.txt), one per line. awk, not grep -vxF -f:
BusyBox grep matches every line against an empty pattern file.
*/}}
{{- define "headhunter.pendingMigrations" -}}
awk 'FILENAME == ARGV[1] { if ($0 != "") have[$0]; next } $0 != "" && !($0 in have)' /work/applied.txt {{ include "headhunter.migrationsFile" . }}
{{- end }}

{{/* Small steps (sqlcmd waits and checks): migrations.stepResources. */}}
{{- define "headhunter.stepContainer" -}}
image: {{ include "headhunter.appImage" . }}
imagePullPolicy: {{ .Values.app.image.pullPolicy }}
{{- with .Values.migrations.securityContext }}
securityContext:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- end }}

{{/* Sablier's API (onDemand): onDemand.sablierUrl, else a Service named sablier in namespace sablier. No trailing slash. */}}
{{- define "headhunter.sablierUrl" -}}
{{- .Values.onDemand.sablierUrl | default "http://sablier.sablier.svc:10000" | trimSuffix "/" -}}
{{- end }}

{{/* The release's Sablier group (onDemand): onDemand.group, else the release's namespace. */}}
{{- define "headhunter.onDemandGroup" -}}
{{- .Values.onDemand.group | default .Release.Namespace -}}
{{- end }}

{{/*
The labels of a workload in the onDemand group: Sablier stops it while nobody visits and starts it on a
visit. Both key forms (sablier.* and sablierapp.dev/*), the same values. Nothing while onDemand is off.
*/}}
{{- define "headhunter.onDemandLabels" -}}
{{- if .Values.onDemand.enabled }}
{{- $group := include "headhunter.onDemandGroup" . }}
{{- range $prefix := list "sablier." "sablierapp.dev/" }}
{{ $prefix }}enable: "true"
{{ $prefix }}group: {{ $group | quote }}
{{- end }}
{{- end }}
{{- end }}

{{/* A workload's replicas line: none in the onDemand group, where Sablier sets the count (one here would be
   set back by every Helm upgrade or Argo CD sync). Takes a dict "root", "replicas". */}}
{{- define "headhunter.replicas" -}}
{{- if not .root.Values.onDemand.enabled }}replicas: {{ .replicas }}{{ end -}}
{{- end }}

{{/* "true" when the release's own SQL Server is in the onDemand group: the migration Job starts the group first. */}}
{{- define "headhunter.onDemandWakesDatabase" -}}
{{- if and .Values.onDemand.enabled (include "headhunter.ownDatabase" .) (not .Values.database.host) }}true{{ end -}}
{{- end }}

{{/* The port of a URL (80 or 443 by scheme when it names none). */}}
{{- define "headhunter.urlPort" -}}
{{- $u := . -}}
{{- if regexMatch ":[0-9]+(/|$)" (trimPrefix "https://" (trimPrefix "http://" $u)) -}}
{{- regexFind ":[0-9]+" (trimPrefix "https://" (trimPrefix "http://" $u)) | trimPrefix ":" -}}
{{- else if hasPrefix "https://" $u }}443{{ else }}80{{ end -}}
{{- end }}
