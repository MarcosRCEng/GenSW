"""Restore a DevOPS-decrypted set to a NEW isolated database and image volume.

No authentication mutation is performed here. Exact comparison completes before
any subsequent disposable credential setup. The clear archive/staging must be
inside the ACL-restricted private task directory and never enter the repository.
"""
import argparse,datetime,hashlib,json,os,pathlib,re,shutil,subprocess,sys,tarfile,time

def sha256_file(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()

def manifest(path):
    result={}
    for item in sorted(path.rglob('*')):
        if item.is_symlink():raise RuntimeError('Links are forbidden in restored image set')
        if item.is_file():result[item.relative_to(path).as_posix()]={'bytes':item.stat().st_size,'sha256':sha256_file(item)}
    return result

def main():
    started=time.monotonic()
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',required=True)
    parser.add_argument('--archive',required=True)
    parser.add_argument('--backup-id',required=True)
    parser.add_argument('--report',required=True)
    args=parser.parse_args()
    config_path=pathlib.Path(args.config).resolve(strict=True)
    private=config_path.parent
    cfg=json.loads(config_path.read_text(encoding='utf-8-sig'))
    if cfg['SourceAlias']!='PILOT-472-CLEAN' or cfg['Environment']!='Homologation':raise RuntimeError('Approved Homologation config required')
    if not re.fullmatch(r'gensw-[A-Za-z0-9][A-Za-z0-9_-]{0,73}',args.backup_id):raise RuntimeError('Unsafe backup identifier')
    archive_path=pathlib.Path(args.archive).resolve(strict=True)
    if private not in archive_path.parents:raise RuntimeError('Decrypted archive must remain inside private task storage')
    target_db=cfg['validationDatabase'];image_target=pathlib.Path(cfg['validationImages']).resolve()
    if (not target_db.startswith('gensw_restore_475_') or target_db in {cfg['sourceDatabase'],cfg['legacyDatabase'],*cfg['forbiddenP0Databases']}):raise RuntimeError('New isolated restore database required')
    if private not in image_target.parents or image_target.exists():raise RuntimeError('New private isolated image volume required')
    extracted=private/('restore-extracted-'+args.backup_id)
    if extracted.exists():raise RuntimeError('Restore extraction target already exists')
    extracted.mkdir(mode=0o700)
    with tarfile.open(archive_path,'r:*') as archive:
        for member in archive.getmembers():
            name=pathlib.PurePosixPath(member.name)
            if not name.parts and member.isdir():continue
            if (name.is_absolute() or not name.parts or
                    '..' in name.parts or '\\' in member.name or ':' in member.name or
                    not (member.isfile() or member.isdir())):raise RuntimeError('Unsafe archive member')
            parts=name.parts[1:] if name.parts[0]==args.backup_id else name.parts
            if not parts and member.isdir():continue
            if not parts or parts[0] not in {'database.dump','images','manifest.json','capture-metadata.json','source-snapshot-private.json','capture-private.log'}:raise RuntimeError('Unexpected archive layout')
            destination=extracted.joinpath(*parts)
            if member.isdir():destination.mkdir(parents=True,exist_ok=True)
            else:
                destination.parent.mkdir(parents=True,exist_ok=True)
                with archive.extractfile(member) as source,destination.open('xb') as target:shutil.copyfileobj(source,target)
    bundle=extracted
    metadata=json.loads((bundle/'capture-metadata.json').read_text(encoding='utf-8-sig'))
    expected_manifest=json.loads((bundle/'manifest.json').read_text(encoding='utf-8-sig'))
    expected_snapshot=json.loads((bundle/'source-snapshot-private.json').read_text(encoding='utf-8-sig'))
    if expected_snapshot.get('snapshotTimeZone')!='UTC' or expected_snapshot.get('digestSortCollation')!='C':raise RuntimeError('Canonical UTC/C source snapshot required')
    if (metadata['task']!=475 or metadata['backupId']!=args.backup_id or metadata['sourceAlias']!='PILOT-472-CLEAN' or metadata['environment']!='Homologation'):raise RuntimeError('Backup set identity mismatch')
    if sha256_file(bundle/'database.dump')!=metadata['databaseDumpSha256'] or manifest(bundle/'images')!=expected_manifest:raise RuntimeError('Decrypted archive integrity mismatch')
    sys.path.insert(0,str(pathlib.Path(cfg['SourceRuntimePath']).parent.parent/'p0-470'/'lib-native'))
    import psycopg
    from psycopg import sql
    def connect(db,**kwargs):return psycopg.connect(**cfg['pg'],dbname=db,application_name='GenSW-PILOT-475-RESTORE',**kwargs)
    with connect('postgres',autocommit=True) as conn:
        exists=conn.execute('SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=%s)',(target_db,)).fetchone()[0]
        if exists:raise RuntimeError('Restore database already exists; never overwrite')
        conn.execute(sql.SQL('CREATE DATABASE {}').format(sql.Identifier(target_db)))
    pg_restore=pathlib.Path(os.environ.get('ProgramFiles','C:/Program Files'))/'PostgreSQL'/'18'/'bin'/'pg_restore.exe'
    env=os.environ.copy();env.update({'PGHOST':str(cfg['pg']['host']),'PGPORT':str(cfg['pg']['port']),'PGUSER':str(cfg['pg']['user']),'PGPASSWORD':cfg['pg']['password'],'PGDATABASE':target_db,'PGCONNECT_TIMEOUT':'5'})
    with (private/'restore-pg-private.log').open('wb') as log:subprocess.run([str(pg_restore),'-w','--exit-on-error','--no-owner','--no-acl','-d',target_db,str(bundle/'database.dump')],env=env,stdout=log,stderr=log,check=True)
    shutil.copytree(bundle/'images',image_target)
    with connect(target_db) as conn:
        conn.execute('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY')
        conn.execute("SET LOCAL TIME ZONE 'UTC'")
        with conn.cursor() as c:
            c.execute("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename");tables=[r[0] for r in c.fetchall()];restored={}
            for table in tables:
                c.execute(sql.SQL("SELECT count(*),md5(coalesce(string_agg(h,',' ORDER BY h COLLATE \"C\"),'')) FROM (SELECT md5(row_to_json(t)::text) h FROM {} t) q").format(sql.Identifier(table)))
                n,h=c.fetchone();restored[table]={'rows':n,'digestMd5':h}
            c.execute('SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId"');migrations=[r[0] for r in c.fetchall()]
            c.execute("SELECT count(*) FROM pg_constraint WHERE contype='f' AND NOT convalidated");bad=c.fetchone()[0]
            refs=0;image_rows=0;active_ui=[]
            for table,owner in (('ImagensAnimal','AnimalId'),('ImagensVariedade','VariedadeId')):
                c.execute(sql.SQL('SELECT {},"Id","ArquivoKey","MiniaturaKey","Ativa" FROM {}').format(sql.Identifier(owner),sql.Identifier(table)))
                for owner_id,image_id,filekey,thumbkey,active in c.fetchall():
                    image_rows+=1
                    for key in (filekey,thumbkey):
                        if key not in expected_manifest or not (image_target/key).is_file():raise RuntimeError('Restored referenced image derivative absent')
                        refs+=1
                    if active:active_ui.append({'kind':'animais' if table=='ImagensAnimal' else 'variedades','ownerId':str(owner_id),'imageId':str(image_id),'fileKey':filekey,'thumbnailKey':thumbkey})
    required_dir=pathlib.Path(cfg['WorkspaceRoot'])/'src'/'Backend'/'GenSW.Infrastructure'/'Persistence'/'Migrations'
    required=sorted(p.stem for p in required_dir.glob('*.cs') if re.fullmatch(r'\d{14}_[A-Za-z0-9_]+',p.stem))
    image_match=manifest(image_target)==expected_manifest
    report={'task':475,'backupId':args.backup_id,'capturedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceAlias':'PILOT-472-CLEAN','environment':'Homologation','restoreAlias':'RESTORE-475-ISOLATED','decryptedArchiveSha256':sha256_file(archive_path),'databaseDumpHashMatchesDevopsVerifiedSet':True,'snapshotTimeZone':'UTC','digestSortCollation':'C','restoredTables':len(restored),'allTablesIdsCountsAndRowDigestsMatch':restored==expected_snapshot['tables'],'restoredMigrations':len(migrations),'migrationHistoryMatchesBackup':migrations==expected_snapshot['migrations'],'migrationHistoryMatchesServedRevision':migrations==required,'unvalidatedForeignKeys':bad,'restoredImageFiles':len(expected_manifest),'allImageHashesMatch':image_match,'allActiveAndInactiveReferencesPresent':True,'checkedImageRows':image_rows,'checkedDerivativeReferences':refs,'credentialsChangedBeforeComparison':False,'authenticationPerformedBeforeComparison':False,'legacyOrServedHomologationDatabaseModified':False}
    report['restoreDurationSeconds']=round(time.monotonic()-started,3)
    report['durationScope']='Local archive extraction, new database and image volume restore, full comparison before authentication; excludes prior DevOPS decryption and Linux restore'
    pathlib.Path(args.report).write_text(json.dumps(report,indent=2),encoding='utf-8')
    (private/'restored-snapshot-before-auth-private.json').write_text(json.dumps({'tables':restored,'migrations':migrations},indent=2),encoding='utf-8')
    (private/'restore-ui-records.json').write_text(json.dumps({'images':active_ui},indent=2),encoding='utf-8')
    if not (report['allTablesIdsCountsAndRowDigestsMatch'] and report['migrationHistoryMatchesBackup'] and report['migrationHistoryMatchesServedRevision'] and bad==0 and image_match):raise RuntimeError('Restored set comparison failed')
    print(json.dumps(report))

if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'restore':'FAILED','errorType':type(error).__name__,'details':'Review restricted restore artifacts; no authentication or source modification performed.'}))
        raise SystemExit(1)
