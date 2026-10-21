"""Verify the served Homologation target; never logs credentials or cookie data."""
import argparse,base64,datetime,hashlib,json,pathlib,subprocess,sys

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',required=True)
    parser.add_argument('--report',required=True)
    parser.add_argument('--allow-negative-probe',action='store_true')
    args=parser.parse_args()
    cfg=json.loads(pathlib.Path(args.config).read_text(encoding='utf-8-sig'))
    if cfg['SourceAlias']!='PILOT-472-CLEAN' or cfg['Environment']!='Homologation':
        raise RuntimeError('Approved served Homologation required')
    reference=json.loads(pathlib.Path(cfg['SourceRuntimePath']).read_text(encoding='utf-8-sig'))
    if (cfg['sourceDatabase']!=cfg['approvedHomologDatabase'] or
            cfg['sourceDatabase']!=reference['validationDatabase'] or
            cfg['sourceDatabase']==reference['sourceDatabase'] or
            cfg['sourceDatabase'] in cfg['forbiddenP0Databases']):
        raise RuntimeError('Forbidden source target')
    sys.path.insert(0,str(pathlib.Path(cfg['SourceRuntimePath']).parent.parent/'p0-470'/'lib-native'))
    import psycopg
    from psycopg import sql
    def connect(db):return psycopg.connect(**cfg['pg'],dbname=db,application_name='GenSW-PILOT-475-GUARD')
    reserved=base64.urlsafe_b64encode(bytes(32)).decode().rstrip('=')
    reserved_hash=hashlib.sha256(reserved.encode('ascii')).digest()
    def snapshot(db):
        with connect(db) as conn:
            conn.execute('SET TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY')
            with conn.cursor() as c:
                c.execute("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename")
                tables=[r[0] for r in c.fetchall()];out={}
                for table in tables:
                    c.execute(sql.SQL("SELECT count(*),md5(coalesce(string_agg(h,',' ORDER BY h),'')) FROM (SELECT md5(row_to_json(t)::text) h FROM {} t) q").format(sql.Identifier(table)))
                    n,h=c.fetchone();out[table]={'rows':n,'digestMd5':h}
                c.execute('SELECT count(*) FROM "RefreshSessions" WHERE "TokenHash"=%s',(reserved_hash,))
                return out,c.fetchone()[0]
    before_source,source_cookie_matches=snapshot(cfg['sourceDatabase'])
    before_legacy,legacy_cookie_matches=snapshot(cfg['legacyDatabase'])
    if source_cookie_matches or legacy_cookie_matches:
        raise RuntimeError('Reserved diagnostic cookie unexpectedly matches a session')
    probe_count=0;probe_status=None
    if args.allow_negative_probe:
        # Separate PowerShell process uses Windows trust and no IAB cookie jar.
        # The reviewed unknown-cookie branch performs one SELECT and returns 401.
        script='$ErrorActionPreference="Stop"; $taskReserved=[Convert]::ToBase64String([byte[]]::new(32)).TrimEnd("=").Replace("+","-").Replace("/","_"); $taskResponse=Invoke-WebRequest -Uri "https://localhost:7003/api/v1/auth/refresh" -Method Post -Headers @{Cookie=("GenSW.RefreshSession="+$taskReserved)} -SkipHttpErrorCheck; @{status=$taskResponse.StatusCode;tlsTrustWithoutBypass=$true} | ConvertTo-Json -Compress'
        result=subprocess.run([cfg['PowerShellExe'],'-NoProfile','-NonInteractive','-Command',script],capture_output=True,text=True,check=True)
        probe_status=json.loads(result.stdout)['status'];probe_count=1
        if probe_status!=401:raise RuntimeError('Negative diagnostic request returned an unexpected result')
    with connect('postgres') as conn:
        conn.execute('SET TRANSACTION READ ONLY')
        with conn.cursor() as c:
            c.execute("SELECT datname,usename,client_port FROM pg_stat_activity WHERE application_name=%s AND backend_type='client backend'",(cfg['servedApiApplicationName'],));activity=c.fetchall()
            c.execute("SELECT count(*) FROM pg_stat_activity WHERE datname=%s AND pid<>pg_backend_pid() AND application_name<>%s",(cfg['sourceDatabase'],cfg['servedApiApplicationName']));unexpected=c.fetchone()[0]
    ports=[p for d,u,p in activity]
    port_literal=','.join(str(int(p)) for p in ports) or '0'
    network_script='$ErrorActionPreference="Stop"; $taskListen=@(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique); $taskPorts=@('+port_literal+'); $taskLinks=@(Get-NetTCPConnection -State Established | Where-Object {$_.LocalPort -in $taskPorts -and $_.RemotePort -eq '+str(int(cfg['pg']['port']))+'} | Select-Object LocalPort,OwningProcess); $taskStart=$null; if($taskListen.Count -eq 1){$taskStart=(Get-Process -Id $taskListen[0]).StartTime.ToUniversalTime().ToString("o")}; @{listenerPids=$taskListen;links=$taskLinks;processStartTimeUtc=$taskStart} | ConvertTo-Json -Depth 4 -Compress'
    network=json.loads(subprocess.run([cfg['PowerShellExe'],'-NoProfile','-NonInteractive','-Command',network_script],capture_output=True,text=True,check=True).stdout)
    state=json.loads(pathlib.Path(cfg['ApiProcessStatePath']).read_text(encoding='utf-8-sig'))
    pids=network['listenerPids'];links=network['links']
    startup_matches=(len(pids)==1 and pids[0]==state['apiProcessId'] and
                     datetime.datetime.fromisoformat(network['processStartTimeUtc'].replace('Z','+00:00'))==datetime.datetime.fromisoformat(state['apiStartTimeUtc'].replace('Z','+00:00')))
    chain=(bool(activity) and startup_matches and len(links)==len(activity) and
           {link['LocalPort'] for link in links}==set(ports) and
           all(link['OwningProcess']==pids[0] for link in links))
    after_source,_=snapshot(cfg['sourceDatabase']);after_legacy,_=snapshot(cfg['legacyDatabase'])
    report={'task':475,'sourceAlias':'PILOT-472-CLEAN','environment':'Homologation','capturedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'apiPid':pids[0] if len(pids)==1 else None,'startupProcessIdentityMatchesGuardState':startup_matches,'apiPidSocketPostgresChainPassed':chain,'observedApiSessionsInApprovedHomologation':sum(d==cfg['sourceDatabase'] for d,u,p in activity),'observedApiSessionsInLegacy':sum(d==cfg['legacyDatabase'] for d,u,p in activity),'allApiSessionsUseApprovedHomologation':bool(activity) and all(d==cfg['sourceDatabase'] and u==cfg['pg']['user'] for d,u,p in activity),'unexpectedHomologationSessions':unexpected,'negativeRefreshRequests':probe_count,'negativeRefreshStatus':probe_status,'reservedDiagnosticCookieAbsentBeforeRequest':True,'credentialOrCookieValuesExported':False,'sourceAllTablesUnchangedDuringGuard':before_source==after_source,'legacyAllTablesUnchangedDuringGuard':before_legacy==after_legacy,'manualLoginPerformed':False}
    pathlib.Path(args.report).write_text(json.dumps(report,indent=2),encoding='utf-8')
    if not (chain and report['allApiSessionsUseApprovedHomologation'] and unexpected==0 and before_source==after_source and before_legacy==after_legacy):
        raise RuntimeError('Runtime target or invariants guard failed')
    print(json.dumps(report))

if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'status':'FAILED','errorType':type(error).__name__,'details':'Review private operator artifacts; no login or backup publication performed.'}))
        raise SystemExit(1)
