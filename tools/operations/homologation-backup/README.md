# Ferramentas da homologação #475

Fontes da rotina local validada nas tarefas #475/#477. A configuração em uso e a identidade de recuperação ficam fora do Git. Estes scripts têm guardas deliberadamente específicas da cópia `PILOT-472-CLEAN`, da revisão preparada e dos namespaces aprovados; não constituem um instalador genérico de produção.

O [runbook](../../../docs/operations/homologation-backup.md) descreve arquitetura, configuração, operação e recuperação. O [registro de validação](../../../docs/validation/2026-10-21-homologation-backup-477.md) identifica os checkpoints reais e suas limitações.

| Grupo | Fontes |
| --- | --- |
| Windows / PowerShell 7 | `daily-backup.ps1`, `install-scheduled-task.ps1`, `capture-maintenance.ps1` |
| Vínculo com API aprovada | `bindings/start-api.ps1`, `bindings/stop-api.ps1` |
| Captura e prova do destino | `capture-source.py`, `verify-source-runtime.py` |
| Linux / receptor, decriptação e retenção | `receiver.py`, `verify-set.py`, `retention.py` |
| Ensaio isolado | `restore-postgres-remote.py`, `restore-isolated.py` |
| Autenticidade da ferramenta age | `verify-age-release.py` |
| Testes sintéticos Linux | `test_receiver.py`, `test_backup_tools.py` |

`config.example.json` permanece desabilitado e contém apenas placeholders. Não preencher ou executar esse exemplo dentro do checkout: copiar para o diretório privado, estabelecer ACL e configurar as dependências conforme o runbook. `capture-maintenance.ps1` deve ser implantado diretamente na raiz privada, pois exige que o staging pertença àquela raiz. Os bindings da API precisam de seu próprio `runtime.json` privado e estado de processo no mesmo diretório.

Os testes sintéticos geram arquivos e uma identidade age temporários. Não acessam configurações, backups, bancos ou imagens reais. O CI executa esses testes e o parser PowerShell; não afirma que um restore PostgreSQL ou uma implantação real ocorreu no runner do GitHub.
