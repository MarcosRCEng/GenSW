# Integração do backup da homologação — #477

Continuação autorizada em 21/10/2026 para commit, push, revisão e merge da entrega técnica da [#475](https://devops-lab.tailaf9418.ts.net/issues/475). Tarefa própria [#477](https://devops-lab.tailaf9418.ts.net/issues/477), filha da Evolução #467, criada e confirmada `Em andamento` antes da investigação técnica desta continuação. A autorização atual permite integrar esta entrega; não concede aceite de fluxos ou saldos do piloto.

## Escopo versionado

Fontes em [tools/operations/homologation-backup](../../tools/operations/homologation-backup/README.md), exemplo desabilitado sem credenciais, bindings sanitizados e [runbook](../operations/homologation-backup.md). Os testes antes vinculados à configuração do DevOPS foram tornados isolados e executáveis no CI com identidade temporária. O wrapper de manutenção e os bindings agora também possuem fonte revisável no repositório. O exemplo remove o caminho pessoal do workspace do binding; a implantação existente não foi substituída silenciosamente.

Base da branch `codex/477-fechamento-piloto`: `origin/main` em `84d7cc5f44656c8d34865e73fb48f4155ab3585c`, conferida por fetch em 21/10. A mesma revisão continua fixada no checkout que serve a aplicação; a integração altera ferramentas e documentação, sem mudanças de API/frontend/modelo/migrations. Configurações privadas, identidades, dumps, imagens, staging, logs, AGENTS.md, arquivos ZIP de anexos e inventários transitórios não entram no Git.

## Checkpoints operacionais

Os arquivos abaixo são recibos sanitizados datados, sem atualizá-los para aparentar uma nova execução. Horários/PIDs/próxima execução dos checkpoints de 20/10 têm significado apenas naquele instante.

| Verificação | Resultado e evidência |
| --- | --- |
| Custódia/criptografia | Duas cópias privadas da identidade; SHA/Sigsum oficiais e recuperação sem Windows/DPAPI. [Preparação 18/10](evidence/477/crypto-preparation-20261018.json) |
| Restore independente real | PostgreSQL18 exclusivo no DevOPS, sem rede/portas, 49 tabelas integralmente iguais, 18 migrations, FK inválida0, 12 imagens/referências íntegras, container removido. [Recibo](evidence/477/devops-postgres-restore-20261018.json) |
| API da cópia restaurada | Login/me200, 12 imagens autenticadas200 PNG e anônimas401, TLS sem bypass; senha alterada somente no destino descartável após comparação. [20/10](evidence/477/restore-api-images-20261020.json) |
| Agendador real | `Scheduled`, sucesso completo e retenção. [Backup20/10](evidence/477/scheduled-backup-20261020.json) e [definição/resultado](evidence/477/scheduler-verification-20261020.json) |
| Deduplicação | Nova chamada normal no mesmo dia retornou0 sem nova captura ou troca do sucesso anterior; nenhum `-Force` persistido. [Recibo](evidence/477/scheduler-deduplication-20261020.json) |
| Retenção | União7datas/4semanas/12meses, atual+âncora preservados; um conjunto intradiário redundante do teste removido. [Aplicação](evidence/477/retention-application-20261020.json) / [plano final](evidence/477/retention-plan-20261020.json) |
| Preservação | Fonte e homologação com49tabelas e hashes de imagens inalterados versus início da475. Preserva o estado pós-incidente473, sem ocultar/resetar aquele incidente. [20/10](evidence/477/preservation-20261020.json) |
| Revisão operacional independente | PASS na475, com distinção de aceite humano. [Relatório](evidence/477/independent-review-475.md) / [checks](evidence/477/independent-review-475.json) |
| Revalidação em21/10 | Tarefa `Ready`, resultado0, backup `Scheduled` de21/10 com49tabelas/12imagens e retenção aplicada; próxima execução observada22/10 às03h. [Checkpoint](evidence/477/backup-status-20261021.json) |

A falha inicial de código64 era diferença entre caminhos virtualizados no aplicativo Windows e arquivos físicos vistos pelo Agendador. A correção preservou os mesmos arquivos, conexões e ACLs, usando contexto físico e ambiente limitado ao processo. O ensaio com API inicialmente parada e o segundo com API já ativa passaram. As fontes versionadas preservam essas guardas.

Um staging privado de captura anterior não publicada e o scratch do ensaio permanecem para diagnóstico. Uma revisão automática recusou limpeza não essencial de probes sintéticos; eles foram mantidos sem contornar a recusa. Não contêm dados no Git e não impedem a rotina validada.

## Validação desta integração

| Check anterior ao commit | Resultado |
| --- | --- |
| Testes sintéticos Linux | 21 aprovados, sem falhas; age v1.3.2 e digest oficial conferidos, namespace temporário isolado |
| Parser PowerShell, incluindo bindings | 5 scripts, zero erros |
| Revisão independente do conjunto | PASS; nenhum bloqueio material, segredo ou caminho pessoal identificado |
| JSON e links locais | 12 JSON válidos, nenhum link local quebrado |

Os resultados finais de testes sintéticos, parser, scan de credenciais, revisão do diff e CI, bem como os SHAs de commit/push/merge e link da PR, são registrados na #477 e na PR. Os workflows Backend e Frontend existentes permanecem ativos; o workflow adicional exercita somente as ferramentas operacionais, sem acessar o ambiente real. Nenhum teste sintético é descrito como restore PostgreSQL real.

## Transição para a próxima tarefa

O escopo técnico de backup/recuperação pode ser encerrado após revisão e merge. A rotina diária exige máquina ligada, usuário conectado e DevOPS acessível; `StartWhenAvailable` não executa com a máquina desligada. Custódia, destino e retenção atuais foram aprovados para homologação. Uma publicação em AWS/VPS requer configuração própria.

As tarefas [#472](https://devops-lab.tailaf9418.ts.net/issues/472) e [#473](https://devops-lab.tailaf9418.ts.net/issues/473) conservam a revisão humana do piloto: login/sessão real em5175, fotos renderizadas nessa sessão, aceite dos fluxos essenciais e conferência de saldos/inventário. O responsável Admin foi indicado; isso não aceita as demais áreas. A nova tarefa pode partir desses pontos e da matriz de aceite já anexada no Redmine. Não criar saldos, estoque ou outros dados fictícios para preencher essa confirmação, nem fechar tarefas legadas por inferência.
