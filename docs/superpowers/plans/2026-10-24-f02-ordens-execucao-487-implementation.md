# F02 — plano incremental condicionado (P2.0 / #487)

24/10/2026. Baseline `425306167764c94519a843deaeb19505844e9114`. **Plano proposto, sem execução técnica F02 autorizada.** Depende do aceite explícito dos IDs D05/O01/R01/D06/E01/Q02 do [adendo](../specs/2026-10-24-f02-ordens-execucao-487-design.md), confirmação da prioridade e autorização em nova Tarefa filha de #430. Não criar antecipadamente todas as Tarefas abaixo. O [relatório P2.0](../../validation/2026-10-24-p2-0-contrato-f02-487.md) contém dependências, evidências e limites.

## 1. Reuso obrigatório e lacunas verificadas

| Dependência | Implementação observada na main | Mudança necessária em F02 |
| --- | --- | --- |
| Catálogo e unidades | `Domain/Catalog/Item.cs`, `CatalogRules.cs`: Item único, capacidades, unidade fixada, decimal, conversão documental | Reusar IDs e unidade; snapshot de identidade/conversão por ordem/real |
| Receita e escala | `Domain/Formulation/Recipe.cs`: versão Publicado imutável, revisão, entrada/saída/perda, fixa/variável | Não editar publicada; congelar plano e tratar real separadamente |
| Simulação | `Application/Formulation/Contracts.cs` e `FormulationService.cs`: snapshot, replay, expansão de sub-receita | Não usar expansão nutricional como lista de débitos físicos; sem autoordens |
| Identidade física F01 | `Domain/Inventory/Entities.cs`: Local, Lote, Posição e eventos | Novos lotes de saída vinculados à execução; nenhuma duplicação de Item |
| Saldo | Posição contém físico/revisão; não há reserva/ordem | Reservas próprias + leitura disponível e validação de todas as retiradas F01 |
| Locks | `InventoryRepository.BeginMutationAsync`: catálogo→inventário, 5 s; consultas RepeatableRead | Produção após ambos; escopo único e conjunto de linhas ordenado |
| Idempotência | Comando F01 por autor/operação/recurso/chave e replay autorizado | Comando F02 por ordem/operação/chave; não copiar escopo por autor inadvertidamente |
| Guardas PostgreSQL | `InventoryGuardSql.cs` e `InventoryModelConfiguration.cs`: tipos fechados, cardinalidade, livro/projeção e imutabilidade | Estender tipos/checks/triggers **por migration futura**, nunca desativá-los para F02 |
| Permissões/UI | `EstoqueController.cs`, `InventoryService.cs`; páginas formulário/Visualizar/histórico/navegação | Matriz D06 explícita no controller e serviço; manter padrões de seletores e retorno |

As fontes acima são relativas a `src/Backend/GenSW.*`; frontend está em `src/Frontend/GenSW.Web/src/features`. Inspecionar de novo na futura baseline, incluindo testes de PostgreSQL real. Não transportar convenções antigas do prospecto como se já implementadas.

## 2. Modelo e migration somente propostos

Nomes são orientativos, a ajustar às convenções verificadas na P2.1; **nenhum arquivo C#/SQL/migration é entregue em P2.0**.

| Estrutura nova proposta | Campos/restrições principais |
| --- | --- |
| `OrdensProducao` | UUID, código único normalizado, estado/revisão concorrente, ReceitaVersaoId FK Restrict, snapshot planejado JSONB/hash, propriedade opcional, responsável, UTCs/datas; sem delete após histórico |
| `ReservasProducao` | UUID, OrdemId/linha, LoteId/LocalId/unidade, quantidade canônica positiva, revisão e estado Ativa/Consumida/Liberada, quantidades consumida/liberada; FK Restrict para posição/ordem; índice por posição/estado; identidade imutável |
| `ExecucoesProducao` | Uma confirmação por OrdemId (índice único), snapshots reais/planejados, resultado Normal/ComDesvio/PerdaTotal, autor/responsável/datas/motivo, algoritmo; imutável |
| `ExecucaoMovimentos` | Execução, linha/ordinal, MovimentoEstoqueId e papel entrada/saída/compensação; unicidade de movimento, FK Restrict, imutável |
| `GenealogiaMateriais` | Liga execução e cada lote de entrada/saída, linhas/movimentos/quantidades; integridade com execução e sem ciclo; não implica rateio causal na multissaída |
| `OcorrenciasProducao` / `HistoricoProducao` | Registro append-only de transição/desvio/interrupção/anotação; autor, responsável, UTC/data observada, revisão, motivo, antes/depois e referência |
| `ComandosProducao` | Ordem/operação/chave únicos, hash canônico, resposta/status/Location, autor e ExigeAdmin, vínculo execução/reversão; imutável e commit conjunto |
| `ReversoesProducao` | ExecucaoId único, eventos compensatórios e evidência/autor/revisão/chave; não exclui confirmação |

Reserva de 60 consumida em 58 preserva o fato original e registra consumida58/liberada2, saldo ativo0; real70 exige alocação suplementar10 criada/consumida na confirmação, nunca consumida70 sobre original60. Vale `original = ativa + consumida + liberada` em toda reserva; troca de posição libera o original não usado e cria alocação nova vinculada. Histórico de reserva posterior continua consultável para bloquear R01. Não manter `reservado` independente na projeção: derivar da soma ativa sob a mesma transação/snapshot. Qualquer futura otimização de agregados exige reconciliação própria.

Estender F01 de forma aditiva/compatível: referências explícitas de execução em ligação própria e referência opcional de ajuste presente; respostas de posição ganham `fisico/reservado/disponivel/usoOrdinarioPermitido/motivos`, preservando `quantidade` como físico. Tipos propostos de evento F01: `ProducaoConsumo` (um débito por alocação), `ProducaoSaida` (um crédito por parcela), `ReversaoProducaoSaida` (débito) e `ReversaoProducaoConsumo` (crédito). Vários eventos vinculados pertencem à **mesma transação da execução**; não fingir que um evento unitário F01 suporta múltiplas linhas. Perda de processo é evidência da execução, sem débito duplicado. Sem evento de quantidade zero. Preservar sequência do livro, origem, referência compensada, snapshot físico e quantização exigidos pelas guardas.

Migration futura precisa ampliar enumerações/checks/cardinalidades/guardas coerentemente, acrescentar integridade diferida entre reserva/posição/estado/execução, impedir reservado acima do físico (inclusive SQL concorrente), preservar imutabilidade do livro e igualdade projeção/livro. Guardas de escrita de reserva/posição precisam serializar a checagem no mesmo recurso físico (por exemplo, lock da linha de posição antes de somar reservas), sem leitura agregada sujeita a write skew. Testes de SQL direto/conexões não cooperativas devem provar a invariante; advisory lock apenas no serviço não basta. Atualização F01 deve validar reservas em cada retirada/ajuste/status relevante. Mudança de status/validade pode impedir uso, mas não apagar reservas. Snapshot/comando/reversão sem UPDATE/DELETE posterior. Não retirar guardas para fazer testes passarem.

Migração aditiva sem inferir ordens/saldos/custos anteriores: reservas/ordens novas vazias; posições antigas continuam com físico e reservado0. Testar base nova e cópia isolada da main, preservando IDs/contagens/hashes de tabelas anteriores. Gerar SQL revisável com origem/destino exatos só na implementação autorizada; revisar riscos/locks e backup/restore. `Down` não é recuperação segura após uso: preferir correção aditiva ou restore coordenado. Nunca aplicar em compartilhado/produção sem autorização específica.

## 3. Verticais executáveis após autorização

| Vertical | Modelo, API e UI de ponta a ponta | Critério de saída verificável |
| --- | --- | --- |
| V1 Ordem/snapshot | Rascunho/Planejada/Cancelada, versão e snapshot; listagem, criar/editar, planejar, Visualizar/histórico; sem reserva | Versão publicada fixa, rótulos históricos, escala/conversão explícitas; duas edições concorrentes geram um sucesso/um409; zero movimento/reserva |
| V2 Reserva integrada | Iniciar/reservas/disponibilidade; UI escolhe lotes/locais paginados e exibe saldos; ajustar todas retiradas F01; ocorrência/aborto Admin | Corrida de ordens e F01 nunca ultrapassa disponível; abortar sem consumo libera uma vez; nenhuma liberação automática |
| V3 Confirmação/genealogia | Reais/perdas/saídas/destinos; transação única, novos lotes, grafo A→B→C, idempotência e replay; exceções O01/E01 | Falhas injetadas rollback total, timeout pós-commit replay único; perda total sem lote zero; sem débito ancestral duplicado |
| V4 Correção/reversão | R01 compensatória, anotação e ajuste presente vinculado; UI mostra pré-condições/impedimentos e histórico | Integral antes de qualquer uso, bloqueada depois de movimento/reserva descendente; sem apagar fatos ou restaurar ancestrais usados |
| V5 Validação integrada | Conciliação Admin, recuperação, migração/cópia isolada, documentação, testes e CI; revisão humana | Matriz §5 aprovada, dados antigos intactos, fluxos desktop/estreito/teclado/permissões; aceite humano separado do CI |

Cada vertical inclui domínio, persistência, API, UI, testes pertinentes e evidência. Não liberar V2 isoladamente para produção sem confirmação/ocorrência/recuperação disponíveis: reserva sem caminho seguro de resolução não é entrega operacional. Não executar as próximas verticais sem resolver falha estrutural da anterior. Criar novas Tarefas somente quando o recorte for autorizado; uma Tarefa P2.1 pode abranger V1–V5 se isso for o autorizado explicitamente.

## 4. Contrato HTTP/UI proposto

Base autenticada `/api/v1/ordens-producao`; GET/POST da coleção, GET/PUT `/{id}`, POST `/{id}/planejamento`, `/revisao-planejamento`, `/cancelamento`, `/inicio`, `/confirmacao`, `/confirmacao-ocorrencia` (Admin/E01), `/aborto-sem-consumo`, `/reversao`, `/ocorrencias` e GET `/historico`, `/comandos/{operacao}/{chave}`. Operações com efeitos usam versão/chave; criação recebe UUID da ordem gerado no cliente como identidade estável do retry, com resposta201/Location idempotente. `GET /api/v1/lotes/{id}/genealogia` e consulta Admin de reconciliação de produção têm contratos paginados e direção/limite de profundidade, a revisar junto às rotas F01 para evitar colisão.

Listas seguem `items/page/pageSize/totalItems/totalPages`, pageSize1–100, busca literal/ordenação estável com desempate ID. Filtros: estado, receita, responsável, intervalo operacional e propriedade opcional. GET por ID mantém registros inativos/históricos. 400/401/403/404/409 e replay seguem §6 do adendo; catálogo e lote existentes inativos diferem de inexistente. Retorno inclui revisão, ações permitidas e impedimentos; cliente nunca é autoridade de permissão/quantidade.

Exemplo conceitual de confirmação (IDs simbólicos, não payload pronto para execução):

```json
{
  "versaoEsperada": 3,
  "responsavelId": "usuario-ativo",
  "entradas": [{"linhaId":"a", "alocacoes":[{"id":"alocacao-1", "loteId":"A1", "localId":"L1", "quantidade":"100", "unidade":"kg"}]}],
  "saidas": [{"linhaId":"b", "quantidade":"98", "unidade":"kg", "novoCodigoLote":"B1", "localId":"L2"}],
  "perdas": [{"quantidade":"2", "unidade":"kg", "motivo":"Perda medida do processo"}],
  "observacao": "Exemplo sintético"
}
```

`Idempotency-Key` vai no header; DTO final inclui datas, campos de lote exigidos F01 e aceite individual de quantização quando necessário, todos documentados no OpenAPI. Prévia não reserva, não confirma e é revalidada no POST. Troca de lote/destino/reais altera payload; após timeout não permitir edição/reenvio com chave nova até consultar resultado. Mesmo payload usa mesma chave inclusive ao recarregar/reautenticar; guardar somente contexto de retry sem token/secret.

UI em Produção — transformações → Ordens (rota proposta `/producao/ordens`): lista→Visualizar→ação explícita. Planejado/real lado a lado; aviso de estoque reservado durante processo; bloqueios visíveis; seleção por busca/paginação servidor e preservação de ID/rótulo histórico fora da página; carregando/vazio/erro/retry e descarte de resposta antiga. Retorno conserva filtros/página e foco; dirty/cancelar/Enter não confirmam acidentalmente; desktop e largura390 sem perda de ação. Modal de Admin mostra motivo/evidência e impactos; R01 mostra qual lote/movimento/reserva impede. Disponibilidade nunca é inferida de simulação ou propriedade. Não acrescentar conceitos de persistência/locks à interface.

## 5. Matriz de validação futura

Todos os cenários de banco abaixo exigem **PostgreSQL real**, conexões/DbContexts independentes para corridas, barreiras determinísticas e asserts no estado final; InMemory/SQLite não comprovam locks/triggers. Executar em ambiente sintético novo ou cópia isolada autorizada, nunca piloto/fonte. A P2.0 somente os especifica.

| ID | Cenário | Resultado que o teste deve provar |
| --- | --- | --- |
| T01 | Planejar, revisar e cancelar | Snapshot da versão exata; nenhum saldo/reserva/evento; revisão obsoleta409 |
| T02 | Duas ordens reservam60 de físico100 simultaneamente | Um sucesso/um409; reservado60/disponível40, sem parcial |
| T03 | Início concorre com transferência/saída/ajuste/descarte F01 | Disponível nunca negativo; Admin também respeita reservas; livro=projeção |
| T04 | Reserva insuficiente na última de várias linhas | Rollback de todas posições/reservas/estado/histórico/comando |
| T05 | Dois confirms mesma ordem/chave/payload, autores distintos autorizados | Um conjunto de fatos; mesma resposta/IDs/status; autor original conservado |
| T06 | Mesma chave payload divergente; outra chave depois de Confirmada | 409 distinto por idempotência/estado; zero efeitos adicionais; 100 e100.000 equivalentes |
| T07 | Falha antes/depois do commit e no envio HTTP | Antes: nada persistido; depois: replay exato, mesmo com revisão/referência agora alterada; permissão perdida403 |
| T08 | Injetar falha após cada débito/lote/crédito/genealogia/liberação/comando | Ordem/reservas/livro/projeção/lotes/genealogia como antes; nenhuma saída órfã |
| T09 | Real menor/maior e troca/múltiplas alocações por linha | Conserva original=ativa+consumida+liberada; suplementar separada respeita reserva alheia; falta adicional409 conserva original; duplicidade rejeitada |
| T10 | Validade no dia limite e no dia seguinte; item/local/lote inativos; status em corrida | Relógio controlado, validade inclusiva; novo uso inelegível409; reserva preservada; E01 Admin registra fato apto/irregular, saídas Bloqueadas em Segregacao, sem novo uso ou saldo negativo |
| T11 | Aborto com/sem consumo; perda total e parcial | Sem consumo Admin libera uma vez; consumo impede aborto; total só Admin, zero lote/movimento vazio; baixa sem duplicar perda |
| T12 | Reversão íntegra, fonte Encerrada, fator alterado e falha intermediária | Fonte terminal409 antes de tocar saídas; compensar normalizado original sem nova conversão; atomicidade/sem DELETE/replay; perda total não recria perda real |
| T13 | Reversão após consumo/transferência/segregação/ajuste/reserva mesmo liberada | 409 integral; voltar quantidade ao lugar não remove impeditivo; sem restaurar ancestrais |
| T14 | A100→B98+perda2; B60→C57+perda3; recebimento/ajuste posterior em B | A baixa100 só uma vez, B final38; C57; grafo A→B→C; Entrada adicional vedada, ajuste Admin identificado como correção e não produção; reverter A→B bloqueado |
| T15 | Multissaída, `un` inteira, kg/L sem fator, resíduo por linha | Lotes por saída/destino; sem rateio nutricional implícito; unidade incompatível400; aceite individual Q01 |
| T16 | SQL direto inválido e mutações concorrentes não cooperativas | Constraints rejeitam reserva>físico, imutabilidade, quantidade negativa, evento sem execução, unidade/estado inválido; transação aborta |
| T17 | Reconciliação em corrida e corrupção simulada somente na fixture | Snapshot consistente; divergência reportada e mutação afetada impedida; nenhum reparo automático |
| T18 | Catálogo→F01→F02 concorrentes, timeout e deadlock provocado | Ordem de locks estável; timeout sanitizado409/retry mesma chave; sem lock Animal/Caixa nem retry em loop infinito |
| T19 | Migração nova e sobre baseline anterior em cópia isolada | SQL revisado, histórico correto, dados/IDs/contagens/hashes antigos preservados; reservas antigas=0; restore ensaiado |
| T20 | 401/403/404/409 e Admin removido antes de replay | ProblemDetails/code corretos; nenhuma escrita não autorizada; inexistente difere de inativo |

API: cada transição legal/ilegal, DTO inválido, limites/listas/rótulos históricos, autoria e `ExecucaoReferenciaId` de ajuste. Frontend: ações por papel, plano/real, lote fora da página, erro/retry, A→B→A, resposta antiga descartada, reentrada após timeout, cancelamento/foco/teclado, genealogia e conflito de saldo. Recuperação: reiniciar somente instância isolada após commit sem resposta, consultar comando e conciliar; reserva antiga não libera automaticamente. Medir política de retenção de comandos/registro de correlação sem segredo.

CI conforme `.github/workflows/ci.yml`: restore/build/test backend com PostgreSQL real e frontend `npm ci`, testes, lint e build; resultado no SHA exato da PR. Não enfraquecer agendamento/testes para contornar corridas. Baseline histórica tem608 testes backend/653 frontend; futuros totais dependem dos novos testes. Triagem de dependências conhecida continua separada; CI verde não encerra vulnerabilidade/aceite operacional.

Aceite humano: responsável confirma D05 e consequência sem WIP; demonstra fluxos reais de planejar→reservar→confirmar; confere físico/reservado/disponível, perda/interrupção/aborto/reversão/ajuste, cadeia A→B→C e D06 com contas reais; valida quantidades/conversões/destinos. Se executar a aplicação no futuro, antes conferir conexão e migrations/histórico, revisar/aplicar SQL pertinente **somente no ambiente autorizado**, e configurar/validar imagens conforme AGENTS: pasta exata/arquivos, miniatura e original autenticados200 image/png e renderização da UI. Fixture sem imagens não comprova gate do piloto. CI, revisão, aceite, merge e deploy são registros separados.

## 6. Saída da P2.0 e entrada da P2.1

P2.0 entrega documentos revisáveis, matriz de dependências/triagem e perguntas de aceite. A Tarefa #487 fica Em validação enquanto contrato/revisão/merge estiverem pendentes. A [instrução P2.1](2026-10-24-p2-1-implementacao-f02-487-prompt.md) só pode executar código após os gates explícitos. Sem essa autorização, não criar migration/tabelas/rotas nem iniciar uma vertical de implementação. Não concluir #430 ou P1 por equivalência.
