# Backend F02 — matriz de validação #495

Execução final local em 26/10/2026, America/Sao_Paulo. `dotnet test GenSW.sln --configuration Release` terminou com código 0: **674/674 aprovados, zero falhas/ignorados**, sendo **66 casos F02** em PostgreSQL real novo/isolado. Nenhum banco operacional ou piloto foi usado.

| Projeto | Aprovados | Falhas | Ignorados |
| --- | ---: | ---: | ---: |
| GenSW.Domain.Tests | 165 | 0 | 0 |
| GenSW.Application.Tests | 147 | 0 | 0 |
| GenSW.Infrastructure.Tests | 114 | 0 | 0 |
| GenSW.API.Tests | 248 | 0 | 0 |

| Gate | Casos aprovados que o exercitam | Evidência |
| --- | ---: | --- |
| T01 | 2 | Planejamento/revisão/cancelamento e edição concorrente sem efeitos físicos. |
| T02 | 1 | Disputa determinística entre duas ordens pela disponibilidade. |
| T03 | 6 | Retiradas F01, incluindo ajuste e descarte, não ultrapassam reservas. |
| T04 | 1 | Insuficiência na última linha reverte todas as reservas. |
| T05 | 1 | Mesmo comando por autores autorizados conserva um conjunto de fatos. |
| T06 | 1 | Divergência de payload/chave e estado terminal são conflitos sem novos efeitos. |
| T07 | 1 | Resposta HTTP perdida após commit recupera o mesmo comando/execução. |
| T08 | 9 | Falhas em cada fase de persistência preservam estado integral e retry seguro. |
| T09 | 4 | Reais menores/maiores, suplementação e troca explícita de posição conservam reservas. |
| T10 | 4 | Aptidão/validade/status e E01 preservam reservas e segregam saídas excepcionais. |
| T11 | 4 | Aborto sem consumo, consumo declarado e perda total não criam saldo fictício. |
| T12 | 5 | Reversão atômica, fonte encerrada, quantização original e referências historicamente nulas. |
| T13 | 2 | Uso, transferência devolvida, alteração e reserva posteriormente liberada bloqueiam reversão. |
| T14 | 1 | Cadeia A→B→C sem débito ancestral duplicado; recebimento vedado e ajuste presente vinculado. |
| T15 | 7 | Precisão, contagem inteira, conversões explícitas/subunidades, resíduo por alocação e multissaída. |
| T16 | 9 | SQL direto: identidade, disponibilidade, serialização RC/RR, liquidação ligada ao livro e conjuntos fechados. |
| T17 | 2 | Reconciliação consistente em corrida e corrupção sintética reportada sem reparo. |
| T18 | 2 | Locks, timeout e deadlock externo retornam conflitos sanitizados com retry seguro. |
| T19 | 4 | Nova base, upgrade/restauração, preservação de linhas, SQL idempotente e recusa de Down destrutivo. |
| T20 | 3 | Autenticação/permissões, referências, payload inválido e perda de papel no replay. |

Um caso combinado pode constar em mais de um gate; a coluna não deve ser somada para obter o total. Nomes, durações e hashes constam no [resumo estruturado](backend-tests-summary.json). Os TRXs brutos permanecem em `output/test-results/f02/final-backend-20261026/`, sem versionar logs de execução.

O SQL executado em T19 é `docs/operations/sql/2026-10-25-f02-from-inventory.sql`, SHA256 `f4126078a113263327890dbb6ed7f6861c2c7c86ef2270d9f24e07388973a4f5`, para a migration `20261025184750_AddProductionOrdersAndExecution`. A criação/migração foi testada em clusters novos e cópias restauradas de fixtures sintéticas.

## Ajustes históricos e rodadas anteriores

As rodadas anteriores não são declaradas verdes: 42 casos dirigidos tiveram 39 aprovações/3 falhas; a rodada seguinte teve 51/56. A suíte completa anterior aprovou Domain 165, Application 147 e API 241, mas falhou em três dos 114 testes de Infrastructure por tentar atravessar o Down físico F02 em exercícios de migrations antigas. As fixtures de transação abortada, JSONB, aplicabilidade e barreira de reconciliação foram corrigidas sem reduzir as expectativas de concorrência/rollback; a ordem de estado/revisão foi corrigida no produto.

Os testes de rollback de Animal/Identificação agora sobem até a migration que exercitam; o teste aditivo do Caixa usa uma base histórica nova com destino financeiro explícito. Outros testes desses módulos continuam no schema atual. O novo T19 confirma que Down F02 recusa destruição e preserva schema, histórico e dados. Novos negativos T16 comprovam que não se pode liquidar reserva sem execução, inserir consumo/reserva em conjunto confirmado, alterar lote produzido sem revisão nem mentir o snapshot de situação da saída.

## Limites

Este arquivo atesta os gates automatizados do backend. Validação em navegador, CI no SHA entregue, merge e aceite humano são evidências separadas. A validação geral e o aceite operacional do piloto permanecem na #493. Não houve execução de migration compartilhada/produção, edição do livro operacional ou autorização de deploy.
