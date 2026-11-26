{{/*
backup_files <folder>: the chart's own backups of DB_NAME in it, oldest first: pg_dump archives (they
start PGDMP) named <database>_YYYYMMDD_HHMMSS.dump, which sort by time. Anything else - hand-named,
half-written (.partial), not a pg_dump archive - is never listed.
*/}}
{{- define "headhunter.pgBackupFilesFunction" -}}
backup_files() {
  ls -1 "$1" | awk -v prefix="${DB_NAME}_" 'index($0, prefix) == 1 && substr($0, length(prefix) + 1) ~ /^[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]_[0-9][0-9][0-9][0-9][0-9][0-9]\.dump$/' \
    | sort | while read -r name; do
      if [ -f "$1/$name" ] && [ "$(head -c 5 "$1/$name")" = PGDMP ]; then echo "$name"; fi
    done
}
{{- end }}

{{/*
backup_database: pg_dump's custom format to BACKUP_DIR/<database>_<date>_<time>.dump (the container's
clock). Written as .partial first (retention only sweeps those once a day old) and moved into place
only once pg_restore has read the whole archive, data included, back.
*/}}
{{- define "headhunter.pgBackupFunction" -}}
backup_database() {
  mkdir -p "$BACKUP_DIR"
  file="$BACKUP_DIR/${DB_NAME}_$(date +%Y%m%d_%H%M%S).dump"
  echo "Backing up $DB_NAME to $file"
  trap 'rm -f "$file.partial"' EXIT
  pg_dump -Fc -d "$DB_NAME" -f "$file.partial"
  pg_restore -f /dev/null "$file.partial"
  mv "$file.partial" "$file"
  trap - EXIT
  echo "Backed up and verified: $file"
}
{{- end }}

{{/* The retention CronJob's script (migrations.backup.retention). It never connects to the database. */}}
{{- define "headhunter.pgRetentionScript" -}}
{{ include "headhunter.pgBackupFilesFunction" . }}
if [ ! -d "$BACKUP_DIR" ]; then echo "No backups in $BACKUP_DIR yet."; exit 0; fi
# A killed backup (deadline, eviction) leaves its .partial behind: sweep those a day old, long past any running dump.
for stale in $(find "$BACKUP_DIR" -maxdepth 1 -name "${DB_NAME}_*.dump.partial" -mmin +1440); do rm -f "$stale"; echo "Deleted stale $stale"; done
# Older than this, by the date and time in the name (the same clock and time zone that named it).
cutoff=$(date -d "@$(( $(date +%s) - KEEP_DAYS * 86400 ))" +%Y%m%d_%H%M%S)
# With ONE_PER_DAY, one that isn't the newest of its day goes too (the newest are first; a kept one counts as its day's).
old=$(backup_files "$BACKUP_DIR" | sort -r | awk -v keep="$KEEP_LAST" -v cutoff="$cutoff" -v one="$ONE_PER_DAY" \
  '{ day = substr($0, length($0) - 19, 8); dup = (day in seen); seen[day] = 1 }
   NR > keep && (substr($0, length($0) - 19, 15) < cutoff || (one == "true" && dup))')
n=0
for name in $old; do
  rm -f "$BACKUP_DIR/$name"
  echo "Deleted $BACKUP_DIR/$name"
  n=$((n + 1))
done
echo "Deleted $n backup(s); kept the newest $KEEP_LAST and anything younger than $KEEP_DAYS days$([ "$ONE_PER_DAY" = true ] && echo " that is the newest of its day")."
{{- end }}

{{/* The backup volume (postgres.backup), for the pods pg_dump and retention run in. */}}
{{- define "headhunter.pgBackupVolume" -}}
- name: backup
  persistentVolumeClaim:
    claimName: {{ .Values.postgres.backup.existingClaim | default .Values.postgres.backup.claimName }}
{{- end }}

{{/* The backup volume's root may be root-owned (a new volume, an NFS export): the dump runs as the app's user. */}}
{{- define "headhunter.fixBackupPermissions" -}}
- name: fix-permissions-backup
  image: {{ include "headhunter.image" .Values.postgres.image }}
  imagePullPolicy: {{ .Values.postgres.image.pullPolicy }}
  command: ["sh", "-c", "chown {{ .Values.migrations.backup.pgOwner }} {{ .Values.migrations.backup.pgDirectory }}"]
  securityContext:
    runAsNonRoot: false
    runAsUser: 0
    readOnlyRootFilesystem: true
  resources:
    requests:
      cpu: 10m
      memory: 16Mi
    limits:
      cpu: 100m
      memory: 64Mi
  volumeMounts:
    - name: backup
      mountPath: {{ .Values.migrations.backup.pgDirectory }}
{{- end }}

{{/* The backup and retention pods share the backup volume, which may be ReadWriteOnce: keep them on PostgreSQL's node. */}}
{{- define "headhunter.pgBackupAffinity" -}}
{{- if .Values.migrations.affinity }}
{{- toYaml .Values.migrations.affinity }}
{{- else if .Values.postgres.enabled }}
podAffinity:
  requiredDuringSchedulingIgnoredDuringExecution:
    - topologyKey: kubernetes.io/hostname
      labelSelector:
        matchLabels:
          app: {{ .Values.postgres.name }}
{{- end }}
{{- end }}
