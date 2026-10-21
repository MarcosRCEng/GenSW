"""Capture only the explicitly approved served Homologation database and images.

The caller must verify a genuinely ACL-restricted, non-repository output parent
(Windows mkdir mode 0700 alone does not enforce this), hold an exclusive local
pipeline lock, and prevent publication whenever this helper fails. This
script creates one NEW clear staging set, never transfers it, and never creates
or restores a database. Encrypt the completed set before transfer. Failure keeps
the restricted staging for operator review; no automatic deletion is attempted.

The served API must be stopped by its guarded launcher for the entire capture.
The caller restarts it in finally, including on capture/encryption failures.
Legacy real-source and P0 databases/volumes are expressly forbidden.
Never include runtime/signing secrets in the set.
"""
import argparse
import datetime
import hashlib
import json
import os
import pathlib
import re
import shutil
import socket
import subprocess
import sys
import urllib.parse


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def manifest(path):
    result = {}
    for item in sorted(path.rglob('*')):
        if item.is_symlink():
            raise RuntimeError('Source image links require explicit review')
        if item.is_file():
            result[item.relative_to(path).as_posix()] = {
                'bytes': item.stat().st_size,
                'sha256': sha256_file(item),
            }
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True)
    parser.add_argument('--output-root', required=True,
                        help='Existing private staging directory; creates only its new backup-id child')
    parser.add_argument('--backup-id', required=True)
    parser.add_argument('--driver-path', default=str(
        pathlib.Path(os.environ['LOCALAPPDATA']) / 'GenSW' / 'p0-470' / 'lib-native'))
    parser.add_argument('--pg-dump', default=str(
        pathlib.Path(os.environ.get('ProgramFiles', 'C:/Program Files')) /
        'PostgreSQL' / '18' / 'bin' / 'pg_dump.exe'))
    args = parser.parse_args()
    sys.path.insert(0, str(pathlib.Path(args.driver_path).resolve(strict=True)))
    import psycopg
    from psycopg import sql

    config_path = pathlib.Path(args.config).resolve(strict=True)
    cfg = json.loads(config_path.read_text(encoding='utf-8-sig'))
    reference = json.loads(pathlib.Path(cfg['approvedRuntimePath']).resolve(strict=True).read_text(encoding='utf-8-sig'))
    source_db = cfg['sourceDatabase']
    if (cfg.get('task') != 475 or cfg.get('SourceAlias') != 'PILOT-472-CLEAN' or
            cfg.get('Environment') != 'Homologation' or
            cfg.get('originRole') != 'served-homologation'):
        raise RuntimeError('Explicit approved Homologation source required')
    if (not source_db or source_db != cfg['approvedHomologDatabase'] or
            source_db != reference['validationDatabase'] or
            source_db == cfg.get('validationDatabase') or
            source_db == cfg['legacyDatabase'] or
            source_db == reference['sourceDatabase'] or
            source_db in cfg['forbiddenP0Databases']):
        raise RuntimeError('Source database is not the exact approved served Homologation')
    if cfg['pg'] != reference['pg']:
        raise RuntimeError('Database server configuration differs from approved runtime')
    image_source = pathlib.Path(cfg['sourceImages']).resolve(strict=True)
    if (not image_source.is_dir() or
            image_source != pathlib.Path(cfg['approvedHomologImages']).resolve(strict=True) or
            image_source != pathlib.Path(reference['validationImages']).resolve(strict=True) or
            image_source == pathlib.Path(cfg['legacyImages']).resolve(strict=True) or
            image_source == pathlib.Path(reference['sourceImages']).resolve(strict=True)):
        raise RuntimeError('Source volume is not the exact approved served Homologation')
    api = urllib.parse.urlparse(cfg['servedApi'])
    if (api.scheme, api.hostname, api.port) != ('https', 'localhost', 7003):
        raise RuntimeError('Unexpected served Homologation API endpoint')
    def require_maintenance_window():
        try:
            connection = socket.create_connection(('localhost', 7003), timeout=1)
        except OSError:
            return
        connection.close()
        raise RuntimeError('Guarded maintenance window required; served API is still listening')
    require_maintenance_window()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,79}', args.backup_id):
        raise RuntimeError('Backup id must be one safe path component')
    if args.backup_id.upper() in {'CON', 'PRN', 'AUX', 'NUL', *('COM'+str(i) for i in range(1,10)), *('LPT'+str(i) for i in range(1,10))}:
        raise RuntimeError('Reserved backup id')
    output_arg = pathlib.Path(args.output_root)
    if not output_arg.is_absolute():
        raise RuntimeError('An absolute private output path is required')
    staging = output_arg.resolve(strict=True)
    if not staging.is_dir():
        raise RuntimeError('An existing private staging directory is required')
    output = staging / args.backup_id
    if output.exists() or not output.parent.is_dir():
        raise RuntimeError('A new set directory inside an existing private parent is required')
    if (output == image_source or image_source in output.parents or
            output in image_source.parents or config_path in output.parents):
        raise RuntimeError('Output must be separate from source data and configuration')
    # This reviewable helper belongs under output/...; no clear data may be
    # placed in the repository containing it.
    script_path = pathlib.Path(__file__).resolve()
    repository = next((p for p in script_path.parents if (p / '.git').exists()), None)
    if repository and (output == repository or repository in output.parents):
        raise RuntimeError('Clear backup data cannot be staged in the repository')
    pg_dump = pathlib.Path(args.pg_dump).resolve(strict=True)

    def connect():
        connection = psycopg.connect(**cfg['pg'], dbname=source_db,
                                    application_name='GenSW-PILOT-475-CAPTURE')
        if (connection.info.dbname != source_db or
                connection.info.user != cfg['pg']['user'] or
                connection.info.port != int(cfg['pg']['port'])):
            connection.close()
            raise RuntimeError('Effective database connection differs from approved source')
        return connection

    def snapshot():
        with connect() as conn:
            conn.execute('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY')
            conn.execute("SET LOCAL TIME ZONE 'UTC'")
            with conn.cursor() as cursor:
                cursor.execute("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename")
                tables = [row[0] for row in cursor.fetchall()]
                result = {}
                fixture_rows = 0
                for table in tables:
                    cursor.execute(sql.SQL(
                        "SELECT count(*),md5(coalesce(string_agg(h,',' ORDER BY h COLLATE \"C\"),'')),"
                        "count(*) FILTER(WHERE fixture) FROM (SELECT md5(row_to_json(t)::text) h,"
                        "row_to_json(t)::text ~* 'P0[-_]470' fixture FROM {} t) q"
                    ).format(sql.Identifier(table)))
                    count, digest, fixtures = cursor.fetchone()
                    result[table] = {'rows': count, 'digestMd5': digest}
                    fixture_rows += fixtures
                cursor.execute('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"')
                migrations = [row[0] for row in cursor.fetchall()]
                cursor.execute("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()")
                other_sessions = cursor.fetchone()[0]
                cursor.execute('SELECT current_database(),current_user')
                actual_database, actual_user = cursor.fetchone()
                if actual_database != source_db or actual_user != cfg['pg']['user']:
                    raise RuntimeError('Server-reported source differs from approved Homologation')
                return {'tables': result, 'migrations': migrations,
                        'fixtureRows': fixture_rows, 'otherSessions': other_sessions,
                        'snapshotTimeZone': 'UTC', 'digestSortCollation': 'C'}

    def validate_image_references(image_manifest):
        # No Ativa filter: historical/inactive rows must also be recoverable.
        # Read only keys, never owners, captions or other personal metadata.
        image_rows = 0
        references = 0
        with connect() as conn:
            conn.execute('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY')
            conn.execute("SET LOCAL TIME ZONE 'UTC'")
            with conn.cursor() as cursor:
                for table in ('ImagensAnimal', 'ImagensVariedade'):
                    cursor.execute(sql.SQL(
                        'SELECT "ArquivoKey","MiniaturaKey" FROM {}'
                    ).format(sql.Identifier(table)))
                    for keys in cursor.fetchall():
                        image_rows += 1
                        for key in keys:
                            if (not isinstance(key, str) or not key or
                                    key in ('.', '..') or '/' in key or '\\' in key or
                                    pathlib.Path(key).is_absolute() or
                                    key not in image_manifest):
                                raise RuntimeError('A database image derivative is absent from the exact source volume')
                            references += 1
        return {'imageRows': image_rows, 'derivativeReferences': references}

    before = snapshot()
    if before['otherSessions'] != 0 or before['fixtureRows'] != 0:
        raise RuntimeError('Homologation must have no competing sessions and no P0 fixture markers')
    image_before = manifest(image_source)
    references_before = validate_image_references(image_before)
    output.mkdir(mode=0o700)
    env = os.environ.copy()
    env.update({'PGHOST': str(cfg['pg']['host']), 'PGPORT': str(cfg['pg']['port']),
                'PGUSER': str(cfg['pg']['user']), 'PGPASSWORD': cfg['pg']['password'],
                'PGDATABASE': source_db, 'PGCONNECT_TIMEOUT': '5'})
    dump_path = output / 'database.dump'
    with connect() as conn:
        conn.execute('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY')
        exported = conn.execute('SELECT pg_export_snapshot()').fetchone()[0]
        with (output / 'capture-private.log').open('wb') as log:
            subprocess.run([str(pg_dump), '-w', '-Fc', '--no-owner', '--no-acl',
                            '--snapshot=' + exported, '-f', str(dump_path)],
                           env=env, stdout=log, stderr=log, check=True)
        shutil.copytree(image_source, output / 'images')
    image_after = manifest(image_source)
    image_copy = manifest(output / 'images')
    references_after = validate_image_references(image_copy)
    after = snapshot()
    require_maintenance_window()
    if (before['tables'] != after['tables'] or before['migrations'] != after['migrations'] or
            after['otherSessions'] != 0 or image_before != image_after or image_before != image_copy or
            references_before != references_after):
        raise RuntimeError('Source or copy changed during capture; reject the set')
    (output / 'manifest.json').write_text(json.dumps(image_copy, indent=2), encoding='utf-8')
    (output / 'source-snapshot-private.json').write_text(json.dumps(before, indent=2), encoding='utf-8')
    report = {
        'task': 475, 'backupId': args.backup_id,
        'capturedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceAlias': 'PILOT-472-CLEAN', 'environment': 'Homologation',
        'originIsServedHomologation': True, 'legacyAndP0Rejected': True,
        'effectiveDatabaseTargetVerifiedByServer': True,
        'guardedApiMaintenanceWindowObserved': True, 'captureOnly': True,
        'snapshotTimeZone': 'UTC', 'digestSortCollation': 'C',
        'exportedConsistentDatabaseSnapshot': True,
        'noCompetingSourceSessionsObserved': True,
        'sourceTablesUnchanged': True, 'sourceImagesUnchanged': True,
        'imageCopyMatchesSource': True, 'includesInactiveAndUnreferencedFiles': True,
        'allReferencedDerivativesPresentIncludingInactive': True,
        'imageReferencesMatchExactManifestBeforeAndAfter': True,
        'checkedImageRows': references_before['imageRows'],
        'checkedDerivativeReferences': references_before['derivativeReferences'],
        'tableCount': len(before['tables']), 'migrationCount': len(before['migrations']),
        'imageFiles': len(image_copy), 'imageBytes': sum(x['bytes'] for x in image_copy.values()),
        'databaseDumpBytes': dump_path.stat().st_size, 'databaseDumpSha256': sha256_file(dump_path),
        'runtimeOrSigningKeyIncluded': False, 'encrypted': False,
        'requiresEncryptionBeforeTransfer': True, 'restoreExecuted': False,
        'independentRecoveryVerified': False,
    }
    (output / 'capture-metadata.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'backupId': args.backup_id, 'tableCount': report['tableCount'],
                      'imageCount': report['imageFiles'], 'sourceUnchanged': True,
                      'consistent': True}))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # Exception messages from dependencies may contain private paths or
        # connection details; the operator reviews restricted local staging.
        print(json.dumps({'capture': 'FAILED', 'errorType': type(error).__name__,
                          'details': 'Private operator review required; no transfer performed.'}))
        raise SystemExit(1)
