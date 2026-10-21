"""Verify a DevOPS-decrypted set in a NEW network-isolated PostgreSQL 18 container.

No Windows component, DPAPI, existing container, published port, host database or
application credential is used. All substantive output is sanitized JSON.
"""
import argparse,datetime,hashlib,json,pathlib,re,subprocess,sys,time,uuid

def sha256_file(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()

def manifest(path):
    result={}
    for item in sorted(path.rglob('*')):
        if item.is_symlink():raise RuntimeError('Links are forbidden')
        if item.is_file():result[item.relative_to(path).as_posix()]={'bytes':item.stat().st_size,'sha256':sha256_file(item)}
    return result

def main():
    started=time.monotonic()
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--crypto-config',required=True)
    parser.add_argument('--backup-id',required=True)
    args=parser.parse_args()
    if sys.platform!='linux':raise RuntimeError('This independent restore must execute on Linux DevOPS')
    if not re.fullmatch(r'gensw-[A-Za-z0-9][A-Za-z0-9_-]{0,73}',args.backup_id):raise RuntimeError('Unsafe set identifier')
    cfg=json.loads(pathlib.Path(args.crypto_config).resolve(strict=True).read_text(encoding='utf-8-sig'))
    set_scratch=pathlib.Path(cfg['scratchRoot']).resolve(strict=True)/args.backup_id
    bundle=set_scratch/'contents'
    metadata=json.loads((bundle/'capture-metadata.json').read_text(encoding='utf-8-sig'))
    expected=json.loads((bundle/'source-snapshot-private.json').read_text(encoding='utf-8-sig'))
    expected_images=json.loads((bundle/'manifest.json').read_text(encoding='utf-8-sig'))
    if metadata['task']!=475 or metadata['backupId']!=args.backup_id or metadata['sourceAlias']!='PILOT-472-CLEAN' or metadata['environment']!='Homologation':raise RuntimeError('Unexpected set identity')
    if expected.get('snapshotTimeZone')!='UTC' or expected.get('digestSortCollation')!='C':raise RuntimeError('Canonical UTC/C snapshot required')
    if sha256_file(bundle/'database.dump')!=metadata['databaseDumpSha256'] or manifest(bundle/'images')!=expected_images:raise RuntimeError('Decrypted set integrity mismatch')
    def run(command,**kwargs):return subprocess.run(command,check=True,capture_output=True,text=True,**kwargs).stdout.strip()
    image='postgres:18'
    initial_image=subprocess.run(['docker','image','inspect',image],capture_output=True,text=True)
    local_image=False
    if initial_image.returncode==0:
        initial_data=json.loads(initial_image.stdout)[0]
        local_image=any(value.startswith(('postgres@sha256:','docker.io/library/postgres@sha256:')) for value in initial_data.get('RepoDigests',[]))
    if not local_image:
        pull=subprocess.run(['docker','pull',image],capture_output=True,text=True)
        (set_scratch/'postgres-image-pull-private.log').write_text(pull.stdout+pull.stderr)
        if pull.returncode:raise RuntimeError('Official PostgreSQL 18 image unavailable; no existing container will be reused')
    final_image=json.loads(run(['docker','image','inspect',image]))[0]
    if not any(value.startswith(('postgres@sha256:','docker.io/library/postgres@sha256:')) for value in final_image.get('RepoDigests',[])):raise RuntimeError('Official PostgreSQL image repository digest unavailable')
    image_id=final_image['Id']
    container='gensw-restore-475-'+uuid.uuid4().hex[:12]
    container_id=None;report=None;cleanup_done=False
    try:
        container_id=run(['docker','run','--detach','--name',container,'--label','gensw.task=475','--label','gensw.purpose=isolated-restore','--network','none','--memory','512m','--cpus','1','--pids-limit','256','--tmpfs','/var/lib/postgresql:rw,nosuid,nodev,size=256m','--tmpfs','/tmp:rw,nosuid,nodev,size=64m','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_DB=gensw_restore',image_id,'-c','shared_buffers=64MB','-c','max_connections=20'])
        inspection=json.loads(run(['docker','inspect',container_id]))[0]
        if inspection['HostConfig']['NetworkMode']!='none' or inspection['HostConfig']['PortBindings'] or inspection['HostConfig']['Binds']:raise RuntimeError('Container isolation guard failed')
        ready=False
        for _ in range(60):
            if subprocess.run(['docker','exec',container_id,'pg_isready','-U','postgres','-d','gensw_restore'],capture_output=True).returncode==0:ready=True;break
            time.sleep(0.5)
        if not ready:raise RuntimeError('New isolated PostgreSQL failed readiness')
        with (bundle/'database.dump').open('rb') as dump:
            restored=subprocess.run(['docker','exec','-i',container_id,'pg_restore','--exit-on-error','--no-owner','--no-acl','-U','postgres','-d','gensw_restore'],stdin=dump,capture_output=True)
        (set_scratch/'postgres-restore-private.log').write_bytes(restored.stdout+restored.stderr)
        if restored.returncode:raise RuntimeError('Isolated database restore failed')
        def query(sql):return run(['docker','exec','-e','PGOPTIONS=-c timezone=UTC',container_id,'psql','-X','-qAt','-v','ON_ERROR_STOP=1','-U','postgres','-d','gensw_restore','-c',sql])
        server_major=int(query('SHOW server_version_num'))//10000
        if server_major!=18:raise RuntimeError('New isolated database is not PostgreSQL 18')
        def identifier(value):return '"'+value.replace('"','""')+'"'
        tables=json.loads(query("SELECT coalesce(json_agg(tablename ORDER BY tablename),'[]'::json) FROM pg_tables WHERE schemaname='public'"))
        actual={}
        for table in tables:
            actual[table]=json.loads(query("SELECT json_build_object('rows',count(*),'digestMd5',md5(coalesce(string_agg(h,',' ORDER BY h COLLATE \"C\"),''))) FROM (SELECT md5(row_to_json(t)::text) h FROM "+identifier(table)+" t) q"))
        migrations=json.loads(query('SELECT coalesce(json_agg("MigrationId" ORDER BY "MigrationId"),\'[]\'::json) FROM "__EFMigrationsHistory"'))
        foreign_keys=int(query("SELECT count(*) FROM pg_constraint WHERE contype='f' AND NOT convalidated"))
        keys=json.loads(query('SELECT coalesce(json_agg(json_build_array("ArquivoKey","MiniaturaKey")),\'[]\'::json) FROM (SELECT "ArquivoKey","MiniaturaKey" FROM "ImagensAnimal" UNION ALL SELECT "ArquivoKey","MiniaturaKey" FROM "ImagensVariedade") q'))
        references=0
        for pair in keys:
            for key in pair:
                if not isinstance(key,str) or '/' in key or '\\' in key or key not in expected_images or not (bundle/'images'/key).is_file():raise RuntimeError('Restored database references an absent image derivative')
                references+=1
        report={'task':475,'backupId':args.backup_id,'capturedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceAlias':'PILOT-472-CLEAN','environment':'Homologation','restoreAlias':'DEVOPS-RESTORE-475-ISOLATED','databaseRestoreExecutedOnDevopsLinux':True,'windowsProfileUsed':False,'dpapiUsed':False,'container':container,'officialPostgresImage':image,'postgresMajorVersion':server_major,'postgresImageAlreadyPresent':local_image,'networkMode':'none','publishedPorts':0,'hostDatabaseDirectoriesMounted':False,'existingContainersUsedOrModified':False,'newTemporaryDatabase':True,'memoryLimitMiB':512,'snapshotTimeZone':'UTC','digestSortCollation':'C','restoredTableCount':len(actual),'allIdsCountsAndRowDigestsMatch':actual==expected['tables'],'restoredMigrations':len(migrations),'migrationHistoryMatchesBackup':migrations==expected['migrations'],'unvalidatedForeignKeys':foreign_keys,'imageFiles':len(expected_images),'imageManifestMatchesDecryptedFiles':manifest(bundle/'images')==expected_images,'allActiveAndInactiveImageReferencesPresent':True,'checkedDerivativeReferences':references,'authenticationOrCredentialMutationPerformed':False,'legacyOrServedHomologationDatabaseTouched':False}
        if not (report['allIdsCountsAndRowDigestsMatch'] and report['migrationHistoryMatchesBackup'] and foreign_keys==0 and report['imageManifestMatchesDecryptedFiles']):raise RuntimeError('Independent restore comparison failed')
    finally:
        if container_id:
            # Operate only on the exact newly returned container id and labels.
            current=json.loads(run(['docker','inspect',container_id]))[0]
            if current['Config']['Labels'].get('gensw.task')!='475' or current['Config']['Labels'].get('gensw.purpose')!='isolated-restore':raise RuntimeError('Container cleanup identity changed')
            run(['docker','rm','--force','--volumes',container_id]);cleanup_done=True
    report['newIsolatedContainerRemovedAfterVerification']=cleanup_done
    report['restoreDurationSeconds']=round(time.monotonic()-started,3)
    report['durationScope']='Artifact validation, optional official image pull, new container initialization, database restore, checks and container cleanup; excludes prior decryption'
    (set_scratch/'postgres-restore-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'restore':'FAILED','errorType':type(error).__name__,'details':'Review restricted DevOPS artifacts; existing services are not fallback targets.'}))
        raise SystemExit(1)
