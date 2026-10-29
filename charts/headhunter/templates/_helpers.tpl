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

{{- define "headhunter.dbHost" -}}
{{- if .Values.database.host -}}
{{- .Values.database.host -}}
{{- else if .Values.mssql.enabled -}}
{{- .Values.mssql.service.name -}}
{{- else -}}
{{- fail "database.host is required with mssql.enabled false" -}}
{{- end -}}
{{- end }}

{{- define "headhunter.dbPort" -}}
{{- if .Values.database.port -}}
{{- .Values.database.port -}}
{{- else if and .Values.mssql.enabled (not .Values.database.host) -}}
{{- .Values.mssql.service.port -}}
{{- else -}}
1433
{{- end -}}
{{- end }}

{{/* The administrator's password: database.password, else (the chart's SQL Server) mssql.saPassword. */}}
{{- define "headhunter.dbPasswordRef" -}}
{{- $p := .Values.database.password -}}
{{- $secretName := $p.secretName | default .Values.mssql.saPassword.secretName -}}
{{- $key := $p.key | default .Values.mssql.saPassword.key -}}
name: {{ include "headhunter.secretName" (dict "secretName" $secretName "root" .) }}
key: {{ $key }}
{{- end }}

{{/*
The administrator's connection for sqlcmd: DB_HOST, DB_PORT, DB_NAME, DB_USER and SQLCMDPASSWORD, which
sqlcmd reads, so the password never goes on a command line.
*/}}
{{- define "headhunter.dbEnv" -}}
- name: DB_HOST
  value: {{ include "headhunter.dbHost" . | quote }}
- name: DB_PORT
  value: {{ include "headhunter.dbPort" . | quote }}
- name: DB_NAME
  value: {{ .Values.database.name | quote }}
- name: DB_USER
  value: {{ .Values.database.user | quote }}
- name: SQLCMDPASSWORD
  valueFrom:
    secretKeyRef:
      {{- include "headhunter.dbPasswordRef" . | nindent 6 }}
{{- end }}

{{/*
ConnectionStrings__Headhunter: database.connectionString's Secret, or one built from the administrator
connection. Built, the password comes in through DB_PASSWORD ($(DB_PASSWORD), expanded by Kubernetes),
quoted, so it may contain ';' but not '"'.
*/}}
{{- define "headhunter.connectionEnv" -}}
{{- $cs := .Values.database.connectionString -}}
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
  value: {{ printf "Server=%s,%s;Database=%s;User Id=%s;Password=\"$(DB_PASSWORD)\";%s" (include "headhunter.dbHost" .) (include "headhunter.dbPort" .) .Values.database.name .Values.database.user .Values.database.options | quote }}
{{- end -}}
{{- end }}

{{/*
The migrations always connect as the administrator (they change the schema), whatever
database.connectionString gives the app.
*/}}
{{- define "headhunter.migrationConnectionEnv" -}}
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      {{- include "headhunter.dbPasswordRef" . | nindent 6 }}
- name: ConnectionStrings__Headhunter
  value: {{ printf "Server=%s,%s;Database=%s;User Id=%s;Password=\"$(DB_PASSWORD)\";%s;Command Timeout=%d" (include "headhunter.dbHost" .) (include "headhunter.dbPort" .) .Values.database.name .Values.database.user (.Values.database.options | trimSuffix ";") (int .Values.migrations.activeDeadlineSeconds) | quote }}
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
Until SQL Server accepts a login and the database, if it exists, is ONLINE (SQL Server accepts logins
before it has finished recovering its databases after a restart). Fails after WAIT_SECONDS. Needs
headhunter.dbEnv.
*/}}
{{- define "headhunter.waitForDatabaseScript" -}}
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
{{- define "headhunter.appliedMigrationsQuery" -}}
sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    IF DB_ID(N'\$$(DB_NAME)') IS NOT NULL AND OBJECT_ID(N'[\$$(DB_NAME)].dbo.__EFMigrationsHistory') IS NOT NULL
      EXEC(N'SELECT MigrationId FROM [\$$(DB_NAME)].dbo.__EFMigrationsHistory');" \
  > /work/applied.txt
{{- end }}

{{/*
The image's migrations the database lacks (/work/applied.txt), one per line. awk, not grep -vxF -f:
BusyBox grep matches every line against an empty pattern file.
*/}}
{{- define "headhunter.pendingMigrations" -}}
awk 'FILENAME == ARGV[1] { if ($0 != "") have[$0]; next } $0 != "" && !($0 in have)' /work/applied.txt /app/migrations.txt
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
