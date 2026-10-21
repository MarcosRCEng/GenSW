# Backup e recuperação da homologação

Entrega operacional da [Tarefa #475](https://devops-lab.tailaf9418.ts.net/issues/475), integrada pela [#477](https://devops-lab.tailaf9418.ts.net/issues/477). As [fontes](../../tools/operations/homologation-backup/README.md) registram a implementação local ensaiada; credenciais e configurações em uso permanecem fora do Git. As [evidências](../validation/2026-10-21-homologation-backup-477.md) distinguem checkpoints históricos, teste sintético e recuperação real.

## Ambiente aprovado

| Item | Configuração |
| --- | --- |
| Origem | `PILOT-472-CLEAN`, cópia para aceite sem fixtures P0 |
| Frontend / API | `https://localhost:5175` / `https://localhost:7003` |
| Revisão da aplicação ensaiada | `84d7cc5f44656c8d34865e73fb48f4155ab3585c`; 18 migrations |
| Destino | `DEVOPS-GENSW-MAIN`, disco principal do servidor DevOPS |
| Frequência | Diária, 03h, `America/Sao_Paulo` |
| Condições | Windows ligado, operador conectado e DevOPS acessível |
| Custódia | Solicitante/operador da homologação; identidade privada no Windows e segunda cópia privada no DevOPS, separada dos conjuntos |

A rotina não usa a fonte legada `LOCAL-REAL-SOURCE` nem as bases P0. Os aliases, portas e revisão são guardas intencionais dessa homologação. Uma mudança de revisão, banco ou volume exige nova preparação e validação; não basta trocar uma connection string. O merge das ferramentas e documentos não atualiza automaticamente o checkout que serve a API.

## Captura, transporte e retenção

O runner adquire lock exclusivo, confere o destino SSH e chama o wrapper privado de manutenção. Este valida por leitura a conexão efetiva, usuário, servidor, volume exato e lista das 18 migrations. Se a API estiver parada e a porta livre, inicia somente o binding aprovado. Identidade do processo e cadeia PID/socket/PostgreSQL são verificadas antes de qualquer sonda ou parada. Listener desconhecido provoca falha sem encerrar processo alheio.

A API é parada durante toda a captura conjunta do banco e dos arquivos, incluindo referências e arquivos inativos. Sessões concorrentes são recusadas. Tabelas são comparadas por IDs/contagens/digests com timezone UTC e ordenação C; dump e manifesto integral recebem SHA-256. A retomada fica em `finally`, inclusive quando a captura falha, e exige nova prova do destino efetivo. A rotina não aplica migrations nem modifica dados para corrigir diferenças.

O pacote recebe criptografia age antes do transporte. A implantação usou age v1.3.2 com autenticidade conferida por SHA-256 e provas Sigsum oficiais, além de identidade híbrida ML-KEM-768 + X25519. O receptor recebe somente ciphertext, confere tamanho/hash e publica atomicamente sem sobrescrita. Reconhecer o cabeçalho age comprova apenas formato; o verificador independente decripta no DevOPS e confere tar seguro, dump e manifesto antes de registrar integridade. Runtime e chaves de assinatura/recuperação não entram no pacote.

Somente após a conferência independente o runner remove o staging em claro, tar e ciphertext locais daquele conjunto. Falhas preservam staging privado para diagnóstico e retornam código diferente de zero. Material de tentativas anteriores não é apagado por um recibo de sucesso posterior.

A retenção mantém a união do conjunto mais recente de **7 datas, 4 semanas ISO e 12 meses** no calendário de São Paulo, além do atual e da âncora do último restore completo. Conjuntos preexistentes e não verificados são preservados. O primeiro prune exige registro posterior a um restore PostgreSQL completo aprovado. Cada candidato tem escopo, dono, modo, tipo e hash conferidos antes de qualquer remoção; namespaces alheios ficam fora da seleção. A verificação diária do pacote não é apresentada como um restore PostgreSQL diário.

## Contrato de implantação privada

O [exemplo da configuração principal](../../tools/operations/homologation-backup/config.example.json) está desabilitado e contém placeholders. Ele não provisiona outra máquina. Os caminhos, conexões, identidades e permissões reais precisam ser definidos pelo operador fora do repositório; os arquivos privados usados no ensaio não são distribuídos.

| Configuração | Contrato |
| --- | --- |
| `config.local.json` | Ferramentas executáveis, raiz/staging/logs privados, entradas de captura/crypto, custódia, agendamento e âncora de recuperação |
| `homologation.config.json` | `task=475`, `Environment=Homologation`, `SourceAlias=PILOT-472-CLEAN`, banco/volume aprovados, legado/P0 proibidos, revisão e configuração de referência, conexões privadas, ferramentas, workspace, estado do processo e launchers |
| Configuração de captura | `sourceDatabase`, `approvedHomologDatabase` e a base de validação do runtime precisam coincidir; `validationDatabase` representa um **novo destino descartável de restore**, distinto da origem, legado e P0 |
| Volume de captura | `sourceImages`, `approvedHomologImages` e o volume do runtime devem indicar a mesma pasta exata dos arquivos, distinta do legado; chaves e miniaturas precisam existir |
| Runtime dos bindings | `runtime.json` privado, com `sourceDatabase`/`sourceConnection` do legado apenas para guarda, `validationDatabase`/`validationConnection` da cópia aprovada, `validationImages` e `signingKey`; o startup usa a conexão da cópia |
| `crypto.config.json` Windows | `AgeExe`, `AgeKeygenExe`, `RecipientsFile`, `IdentityFile`, `SshAlias`, `SshHostName`, `RemoteSetRoot`, `RemoteIdentityFile`, `RemoteReceiver`, `RemoteVerifier`, `RemoteRetention` |
| Crypto Linux | `crypto.json` privado no namespace de recuperação, com `ageExe`, `identityFile`, `setRoot` e `scratchRoot`; o helper valida diretórios fixos e identidade fora dos conjuntos |

No Windows, configurar ACL explícita protegida para a raiz, staging, logs, configuração e identidade, limitada ao operador e aos principals administrativos permitidos pela rotina. No Linux, diretórios privados usam `0700` e arquivos `0600`. Não criar identidade ou dumps no checkout. O `.gitignore` das ferramentas é proteção adicional, não substitui o armazenamento privado.

Implantar `capture-maintenance.ps1` diretamente na raiz privada; ele exige staging abaixo do próprio diretório. Implantar `daily-backup.ps1` em `ops/` privado e copiar os helpers Python para uma localização persistente referenciada pela configuração. Os bindings `start-api.ps1` e `stop-api.ps1` ficam juntos ao seu `runtime.json` e `api-process.json` privados. O binding exige o workspace informado e os assemblies Debug da revisão preparada, incluindo Npgsql; conserva guardas CLR explícitas de banco e metadados de processo. O usuário não deve executar os bindings a partir da pasta do repositório.

A captura Windows requer PowerShell 7, Python/psycopg, PostgreSQL 18 (`pg_dump`/`pg_restore`), .NET, Git, tar, OpenSSH e age. O driver local validado é resolvido pelo helper; não imprimir sua configuração de conexão. O DevOPS usa Python 3/Linux, age e SSH com host/chave conhecidos. O ensaio PostgreSQL independente requer Docker disponível e imagem oficial PostgreSQL 18. Nenhum serviço ou container existente é reutilizado pelo restore.

### Contexto Windows e Agendador

O processo empacotado do aplicativo e o Agendador podem ter visões diferentes de `LOCALAPPDATA`. A implantação corrigiu referências para os **mesmos arquivos físicos**, confirmando equivalência e preservando ACLs; não moveu banco/imagens. `TaskContext.LocalAppData` usa a localização física e `PathEntries` indica os diretórios aprovados de Git/.NET. `LOCALAPPDATA` e `PATH` mudam somente no runner e seus filhos, sem gravação global de ambiente.

O instalador só registra a tarefa depois de `Enabled=true`, `Schedule.Enabled=true` e `Recovery.InitialRestoreVerified=true`, com âncora válida previamente registrada no DevOPS. Usar `install-scheduled-task.ps1 -ConfigPath <arquivo-privado>` após concluir os ensaios abaixo. A tarefa usa usuário limitado, `InteractiveToken`, `StartWhenAvailable`, uma instância e retentativas. Se o usuário estiver desconectado ou a máquina desligada, não há execução naquele momento; a retomada ocorre quando disponível. O horário não constitui garantia de backup sem essas condições.

## Operar e verificar

Em PowerShell 7 no perfil autorizado, obter o caminho canônico pela definição da tarefa evita reutilizar um caminho virtualizado. Não imprimir o conteúdo das configurações.

```powershell
$task = Get-ScheduledTask -TaskName 'GenSW-Homologation-Backup'
$configPath = [regex]::Match($task.Actions[0].Arguments, '-ConfigPath "([^"]+)"').Groups[1].Value
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
Get-ScheduledTaskInfo -TaskName $task.TaskName

# Backup manual completo usando a mesma configuração protegida.
& $config.Tools.PowerShellExe -NoProfile -NonInteractive -File $config.DailyRunner -ConfigPath $configPath

# Invocação normal da tarefa: não recaptura se já houve sucesso Scheduled hoje.
Start-ScheduledTask -TaskName $task.TaskName
```

Conferir `last-run.json`, `last-success.json` e recibo do ID no diretório privado. Sucesso exige captura consistente, origem preservada, transporte, decriptação e integridade aprovados. `LastTaskResult=0` também pode representar deduplicação: verificar o ID e a data do último backup, não apenas o código. A tarefa persistida não mantém `-Force`. Logs detalhados permanecem privados; evidências públicas trazem aliases/booleans/contagens, sem dados, caminhos de imagem, conexões ou credenciais.

## Ensaiar recuperação independente

1. Selecionar um ID com ciphertext e SHA-256 conferidos. Usar a identidade independente do DevOPS; a recuperação não depende de DPAPI ou do perfil Windows original.
2. Executar a entrada remota `RemoteVerifier --backup-id ID --sha256 HEX --retain-for-restore`. Conferir `decryptionVerified` e `packageIntegrityVerified`; isso ainda não prova restauração PostgreSQL.
3. Executar `restore-postgres-remote.py --crypto-config <json-privado-remoto> --backup-id ID`. O helper cria container PostgreSQL 18 exclusivo sem rede, portas ou volumes de banco do host, restaura e compara todas as tabelas/migrations/FKs/referências/hashes. Remove somente o container recém-criado após a prova. Seu tempo informado inclui eventual pull/init/restore/verificação/limpeza e exclui a decriptação anterior.
4. Após PASS completo, registrar `RemoteRetention --record-restore --restore-protect ID --sha256 HEX`. Conferir plano com `--plan --protect CURRENT --restore-protect LASTRESTORED`; habilitar a rotina/prune somente depois do gate registrado.
5. Quando necessário, devolver o bundle por SSH com `--stream-bundle`, conferir o digest do arquivo em claro e executar `restore-isolated.py --config <fonte-privada> --archive <tar-privado> --backup-id ID --report <relatorio-privado>`. Banco e volume precisam ser novos e descartáveis; o helper recusa sobrescrita e destinos iguais à origem/legado/P0.

Comparar os dados **antes** de qualquer credencial/login de ensaio. Uma credencial exclusiva da cópia descartável pode então validar a API e imagens autenticadas, sem resetar usuário da homologação servida. Guardar evidência de `200 image/png` autenticado, `401` anônimo, TLS confiável e destino efetivo. Parar somente a API temporária criada para o ensaio. Renderização no navegador e aceite de dados reais continuam sendo verificações humanas próprias.

Se houver falha, manter o conjunto anterior e a âncora, conferir destino/SSH/espaço/configuração e saúde da API após a janela. Não aplicar `Down`, apagar arquivos inativos, sobrescrever bancos ou alterar o legado como recuperação. O ponto de retorno é o último conjunto completo verificado; a autoridade operacional é o solicitante responsável pela homologação.

## Testes das ferramentas

Em Linux com Python 3, age e age-keygen no `PATH`:

```bash
python3 -m unittest discover -s tools/operations/homologation-backup -p 'test_*.py' -v
```

Também é possível informar `GENSW_TEST_AGE_EXE` e `GENSW_TEST_AGE_KEYGEN_EXE`. Os testes usam diretório temporário do sistema no filesystem principal, identidade X25519 efêmera e dados sintéticos. Exercitam receptor, tar, integridade, adulteração e retenção; não abrem as configurações privadas ou o namespace real. O workflow fixa age v1.3.2 por digest oficial e valida sintaxe PowerShell. A identidade híbrida real, a custódia e o restore PostgreSQL são comprovados pelas evidências operacionais, não por esses fixtures.
