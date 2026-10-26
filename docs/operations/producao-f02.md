# Operação de ordens e execução física — F02

Contrato D05/O01/R01/D06/E01/Q02 e implementação V1–V5 autorizados explicitamente na [Tarefa #495](https://devops-lab.tailaf9418.ts.net/issues/495), journal 1426, filha da #430, em 25/10/2026. A autorização referencia o [adendo aceito](../superpowers/specs/2026-10-24-f02-ordens-execucao-487-design.md) no commit `5b9f8a18aba470ce9825c91efab81a06fbb36153`. Aceite funcional e integração são posteriores à entrega técnica; a validação geral do piloto permanece na #493.

## Fluxo diário

1. Em Produção → Ordens, crie uma ordem com código, versão publicada da receita, tamanho, unidade e responsável ativo. Propriedade é contexto opcional, sem criar local ou permissão.
2. Planeje a ordem. O snapshot conserva versão exata, escala, rótulos, referências e conteúdo; não acompanha mudanças futuras. Revisar planejamento retorna ao rascunho e exige novo planejamento, mantendo os planos anteriores no histórico.
3. Inicie escolhendo explicitamente os lotes e locais de cada entrada. A reserva deve atender integralmente todas as linhas, inclusive embalagem/consumível. Várias alocações podem atender uma linha; a prévia mostra quantização e saldos.
4. Durante a transformação, registre interrupções e fatos observados. O físico contábil permanece intacto; o material está reservado. Reservas não expiram por tempo, logout ou queda da aplicação.
5. Para confirmar, confira entradas reais, parcelas de saída com novos códigos de lote/destinos, perdas e observações. A prévia não movimenta estoque. Aceite individualmente cada resíduo calculado, com motivo, antes de confirmar.
6. Confirmação debita entradas, cria novos lotes/créditos, congela reais, registra genealogia/ocorrências e liquida reservas na mesma transação. Real menor libera o restante; real maior ou troca de lote/local exige disponibilidade adicional e alocação suplementar separada. Falta de saldo conserva a ordem iniciada e as reservas originais.

Saída zero pertence ao realizado e não cria lote ou movimento vazio. A perda de processo discrimina o material já consumido: consumir 100 kg e obter 98 kg com perda 2 kg baixa 100 kg. Não há segundo débito da perda. Sub-receita na execução consome apenas o intermediário disponível em estoque; fabricar o intermediário exige outra ordem explícita.

## Saldos, precisão e rastreabilidade

Por posição, `fisico = livro`, `reservado = soma das reservas ativas` e `disponivel = fisico - reservado`. Apto para uso ordinário é uma condição adicional: material vencido, bloqueado, inativo ou em segregação conserva físico/reserva, com quantidade elegível zero. Não se somam kg, L e un.

As retiradas F01 também respeitam reservas, inclusive transferência, saída, consumo, ajuste negativo, segregação e descarte; Admin não pode ultrapassá-las. Alterar status/validade não libera reservas. Ajuste contado abaixo do reservado exige primeiro resolver a execução.

Quantidades HTTP são strings decimais invariantes. Reusam seis casas, arredondamento ToEven aceito por linha e contagem inteira de F01. Conversões entre dimensões exigem referência documental do próprio item, congelada no apontamento; não se aplica densidade ou fator implícito. Planejado e real permanecem separados. Perda/desvio de MisturaSimples registra processamento no realizado, preservando a receita publicada, sem presumir conservação nutricional.

Cada parcela de saída/destino positivo gera lote novo. Lote de produção não admite recebimento ordinário adicional nem abertura F01; novo recebimento exige outro lote. `origemProducao` deriva do vínculo persistido, sem inferir origem de texto. Genealogia liga entradas → execução → saídas, consulta origens/destinos com paginação/profundidade e não atribui rateio causal ou nutricional na multissaída.

## Exceções e correções

| Situação | Resolução |
| --- | --- |
| Falta antes do início | Revisar/cancelar a planejada; nenhum saldo ou reserva muda. |
| Interrupção após início | Registrar ocorrência; ordem iniciada e reservas permanecem. |
| Nenhum consumo físico | Admin declara ausência de consumo, motivo/evidência e aborta, liberando reservas uma vez. |
| Houve consumo, inclusive perda total | Confirmar todos os reais; perda total exige Admin/evidência, resultado PerdaTotal e nenhum lote vazio. |
| Aptidão/status mudou após fato físico | Admin registra exclusivamente fato passado comprovado, data e aptidão Apto/Irregular. Todas as saídas ficam Bloqueadas em Segregação ativa, mesmo com aptidão passada comprovada. Sem saldo/evidência, diagnosticar a divergência. |
| Lançamento confirmado incorreto e coerência física comprovada | Admin pode reverter integralmente por eventos compensatórios somente com todas as saídas intactas na origem, sem movimento/reserva/alteração posterior e sem fonte encerrada. |
| Saída transferida e devolvida, reservada e liberada, consumida ou alterada | Reversão bloqueada; usar anotação e/ou ajuste presente Admin vinculado. |

Compensação usa exatamente a quantidade canônica originalmente registrada, sem nova conversão ou quantização. O movimento original conserva declaração, resíduo e aceite. Livro, comandos, genealogia e histórico não são apagados. Lotes gerados ficam zerados e encerrados. Reversão não recria material realmente perdido nem desfaz transformação física.

Ajuste F01 de lote produzido exige `execucaoReferenciaId`, contagem atual, data, motivo/evidência e Admin. Aparece como correção vinculada e não aumenta a quantidade atribuída à execução original. Não restaura ancestrais, não muda estado confirmado e não retroage saldos.

## Permissões e falhas de comunicação

Autenticados consultam, criam, planejam, iniciam e confirmam rotina/desvios parciais justificados. Admin aborta, confirma exceções/perda total, reverte, ajusta e consulta reconciliação F02. Usuário/papéis atuais são revalidados no servidor após os locks e antes do replay; o responsável operacional selecionado não eleva permissão.

Comandos materiais usam revisão esperada e `Idempotency-Key`. A identidade F02 é ordem/operação/chave, independente do autor: outro operador atualmente autorizado recupera a resposta original sem gerar outro fato. Mesma chave com conteúdo diferente retorna conflito. Nova chave em estado terminal não repete a execução.

Após timeout, consulte o resultado ou repita exatamente a mesma chave e corpo. O frontend conserva apenas contexto de retry em sessionStorage, sem token, e bloqueia edição enquanto o resultado é incerto. Não crie nova chave por falta de resposta. Após reautenticação, recupere o mesmo comando. Uma resposta histórica de replay conserva os IDs/status/corpo originais; consulte a ordem separadamente para seu estado atual.

Um 404 isolado na consulta do comando não resolve um timeout: a transação original ainda pode estar em andamento. Liberar edição exige rejeição definitiva 400/409 do POST e confirmação de ausência; uma nova tentativa volta a ser incerta antes do envio. Contexto local inválido é preservado com diagnóstico, sem emitir outro comando ou derrubar a página.

## HTTP e navegação

Base autenticada `/api/v1/ordens-producao`; UI `/producao/ordens` e `/producao/genealogia/:loteId`.

| Recursos | Rotas relativas à base |
| --- | --- |
| Ordens | GET/POST coleção; GET/PUT `/{id}` |
| Planejamento | POST `/{id}/planejamento`, `/revisao-planejamento`, `/cancelamento` |
| Execução | POST `/{id}/inicio`, `/previas`, `/confirmacao`, `/confirmacao-ocorrencia`, `/aborto-sem-consumo`, `/reversao` |
| Histórico/ocorrências | GET `/{id}/historico`; GET/POST `/{id}/ocorrencias` |
| Recuperação | GET `/{id}/comandos/{operacao}/{chave}` |
| Reconciliação Admin | GET `/reconciliacao`, paginado |
| Genealogia | GET `/api/v1/estoque/lotes/{id}/genealogia`, direção/profundidade/página |

Listas usam `items/page/pageSize/totalItems/totalPages`, limite de página 100 e ordenação estável. Erros retornam ProblemDetails/código: 400 formato/unidade/aceite; 401 sessão; 403 papel; 404 referência inexistente; 409 revisão/estado/aptidão/saldo/chave/conflito transitório. Invariante persistida divergente retorna falha operacional sanitizada e impede a mutação; nenhuma leitura repara saldo.

## Migration e recuperação

Migration `20261025184750_AddProductionOrdersAndExecution`, após F01 `20261007225228_AddManualInventory`. O [SQL idempotente revisável](sql/2026-10-25-f02-from-inventory.sql) acrescenta dez tabelas com FKs Restrict, índices, checks e triggers diferidos. Não cria ordens ou saldos retroativos. Não modifica `InventoryGuardSql` histórico; a nova migration estende as funções instaladas e mantém a imutabilidade e conciliação do livro.

Reservas e alterações de posição serializam no mesmo recurso físico também para SQL não cooperativo. Os checks diferidos conferem reservado ≤ físico, estado/reservas/execução/reversão, movimentos e saídas, quantização/compensação, genealogia completa/sem ciclo e bloqueios da reversão. Escritas da aplicação mantêm locks catálogo `(413,421)` → inventário `(430,1)` → produção `(430,2)` e linhas em ordem estável; confirmação e reversão usam uma única transação. Leituras compostas usam snapshot consistente.

Antes de executar: confirmar destino efetivo, versão e histórico, revisar todas as pendências e aplicar somente no PostgreSQL novo/cópia isolada autorizado. Banco compartilhado/produção exige autorização específica, escritores compatíveis, backup e restore coordenados. A aplicação não migra no startup. `Down` F02 rejeita remoção destrutiva; recuperação após fatos exige correção aditiva ou restauração coordenada.

Para registros com imagens, conferir raiz privada exata, arquivos/chaves, permissões, originais/miniaturas autenticados `200 image/png` e renderização conforme AGENTS local. Fixture F02 sintética sem imagens não comprova os gates de imagens ou aceite do piloto da #493.

Reconciliação Admin compara estados, reservas, livro/projeção, execução, saídas, genealogia e compensação sem escrever. Reserva antiga deve ser resolvida com responsável e declaração de consumo, sem liberação automática. Divergência exige preservar evidência, suspender mutações afetadas e diagnosticar em cópia isolada.

F03/ovos, custos, solver, venda/fiscal, WIP e orquestração automática permanecem fora deste incremento.
