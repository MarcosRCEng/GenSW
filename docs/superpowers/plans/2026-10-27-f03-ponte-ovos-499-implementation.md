# P3.1 — plano incremental condicionado da ponte de ovos

27/10/2026. [#499](https://devops-lab.tailaf9418.ts.net/issues/499), filha da #430. Revisão documental `P3.0-499-r2`, com publicação Git autorizada. **Plano documental, sem autorização de código ou migration.** Depende do aceite explícito do [contrato F03](../specs/2026-10-27-f03-ponte-ovos-499-design.md) e das [decisões](../specs/2026-10-27-f03-ponte-ovos-499-decisions.md), e da autorização explícita do recorte. Este documento não registra esses atos.

## 1. Recorte recomendado e preparação

B1–B5 completos para ponte de lançamentos individualmente comprovados em `un`, entrada simples F01, associação manual sem saldo, conciliação e compensação conservadora. Usar F02 já integrada para regressão/consumo posterior, sem criar nova transformação automática ou identidade individual de consumo. Coletivos/amostras, entrada baseada só em kg, fator por peso padrão, compensação parcial, remapeamento liberando ponte e F04–F06 ficam fora do recorte.

Antes de código: criar Tarefa própria P3.1 filha da #430, registrar os dois atos humanos, o recorte exato e referências à #499/#495/#493; mover Em andamento e confirmar por API. Não reutilizar #499 para um novo prompt executável P3.1. Resposta que apenas delibera o contrato pode ser registrada na #499, sem Tarefa redundante para esse ato; se uma Tarefa P3.1 própria já existir, reconsultá-la e continuá-la. Reconsultar journals para aproveitar aprovações já existentes e diferenças posteriores. Fetch/reidentificar main/CI; comparar o novo SHA com a base da P3.0 e adaptar o plano se contratos tiverem mudado. Preservar checkout principal e arquivos locais; preferir checkout isolado adequado e branch `codex/<tarefa>-f03-ponte-ovos` somente quando iniciada a implementação autorizada.

Não abrir migration ou mexer em dados para resolver decisão de negócio pendente. Não tratar zero nas consultas P3.0 como autorização de importação. Reidentificar o destino de qualquer teste antes de usá-lo.

## 2. Verticais e critérios de parada

| Etapa | Domínio/persistência | API/UI e efeito revisável | Saída mínima |
| --- | --- | --- | --- |
| B1 — identidade, prova e transação | Recursos F03 próprios; ponte vitalícia, snapshots, recorte/prova/slots, fatos/comandos imutáveis; migration aditiva e guardas | Contratos e consultas autenticadas paginadas; estados/impedimentos explícitos | PostgreSQL prova unicidade/capacidade/imutabilidade; sem habilitar mutação operacional incompleta |
| B2 — prévia e associação | Captura consistente, hash/UpdatedAtUtc, expectativa de cortes/revisões, associação à entrada original sem movimento físico | Seleção parcial entre páginas, prévia verificável, fluxo Admin de prova e associação; delta zero visível | Histórico manual intacto; corridas da última capacidade; adulteração/erro/retry/409; não habilitar entrada nova ainda |
| B3 — entrada e recebimento manual | Entrada F01 + ponte em commit único; lote dedicado estruturado, guardas de recebimento; participação de F01 na conciliação B03 | Prévia/confirmação ordinária por autenticado com prova e liberação vigentes; recebimento manual gerido exige origem comprovada | Não duplicação da origem identificada; corrida F01/F03 e invalidação de prévia; fluxo completo pronto para correção/recuperação |
| B4 — edição, compensação e conciliação | Fatos de divergência/anotação/invalidação, compensação exata única por grupo, proteção de reservas/usos F02 | Detalhe histórico versus estado atual; prévia Admin e impedimentos explicados; conciliação READ ONLY | Sem reentrada/slot liberado; erro após uso tratado por anotação/ajuste presente explícito, sem reparo automático |
| B5 — validação e entrega | Migration nova/upgrade/restore/preservação, guardas SQL concorrentes, regressão integrada | Browser desktop/estreito, comum/Admin, recuperação e instruções operacionais | Suites/lint/build apropriados; CI no SHA entregue se publicação autorizada; evidências e Tarefa Em validação |

Não disponibilizar B3 em operação antes de B4 e gates dirigidos. B1/B2 isoladas podem ser autorizadas como investigação técnica/infraestrutura inativa; não declarar F03 operacional nem oferecer botão de entrada incompleto. Se alternativa humana mudar cardinalidade, massa ou compensação, revisar contrato e vertical antes de implementá-la.

## 3. Desenho técnico proposto

### 3.1 Recursos e índices

Seguir as verticais atuais `Domain/Application/Infrastructure/API` e features React, sem framework genérico e sem acoplar o cadastro Animal ao serviço Inventory. Nomes abaixo são propostas, não tabelas existentes:

| Recurso | Conteúdo e proteção |
| --- | --- |
| `AncoraConciliacaoOvos` | UNIQUE ItemId explicitamente inscrito, revisão/corte e estado derivado dos fatos. Escrita de crédito relevante bloqueia/atualiza âncora e invalida conferência no mesmo commit, inclusive escritor SQL sem advisory em outro lote/local |
| `RecorteConciliacaoOvos` / fatos de revisão | Itens explicitamente geridos, fronteira física/documental, fontes consultadas/lacunas, decisão por seleção, aprovador/responsável, revisão e corte; histórico imutável. Inscrição de Item é ação Admin explícita, sem classificação por texto/backfill |
| `ProvaEntradaOvos` | MovimentoEstoqueId e EventoId originais manuais, ItemId, contagem N comprovada, declaração original un, evidência/referência, autor/responsável/data. UNIQUE MovimentoEstoqueId; N positivo inteiro e ≤ contagem original; exclui crédito raiz/lote de entrada nova F03 também por guarda SQL |
| `GrupoPonteOvos` | Operação, recorte/prova, destino, número de IDs, comando e snapshots físicos. Fato imutável; estado derivado dos tratamentos, sem edição destrutiva |
| `PonteProducaoOvo` | UNIQUE ProducaoOvoId incondicional/vitalício; FK Restrict à origem e ao grupo; snapshot/hash/UpdatedAtUtc/algoritmo; origem física precisa e quantidade 1 un. Nunca apagar para corrigir |
| `AlocacaoProvaOvos` | PonteId único; MovimentoEstoqueId/ProvaId e ordinal inteiro. UNIQUE MovimentoEstoqueId/ordinal; ordinal entre 1 e N. Referências compostas/guardas impedem trocar movimento/item; ordinal continua ocupado após invalidação |
| `OrigemLoteOvos` | UNIQUE LoteId e grupo de entrada. Lote F01 vazio/dedicado no início; um recebimento inicial correspondente ao grupo; proibição de recebimentos adicionais, sem confundir com SaidaProducao F02 |
| `TratamentoPonteOvos` | Anotação, invalidação documental ou compensação ligada ao grupo, autor/responsável/evidência/comando. Compensação única por grupo; restrição adicional para invalidação terminal única. Fatos append-only |
| `ComandoPonteOvos` | UNIQUE Operacao/Chave global, HashPayload, grupo/referências, status HTTP/Location/JSON original/autor/data. Permissão atual antes do replay; sucesso atômico |

FKs Restrict, checks de unidade/inteiros/tamanhos/algoritmo, append-only e índices de consulta por grupo/item/origem/data. Não usar índice filtrado por estado para a ponte. Guardas diferidas comprovam completude da seleção, coerência snapshot/movimento/item/lote, quantidade de entrada = número de pontes, associação sem movimento novo e estado de compensação. Reavaliar formatos dos JSONB atuais antes de escolher colunas/records. Evidência pode ser referência documental textual, sem exigir novo sistema de upload.

A unicidade de slots no próprio movimento evita duas provas concorrentes com capacidades independentes. Guardas verificam prova/contagem no banco e proíbem alteração/exclusão da prova ou alocação depois de aceita. Se for escolhida projeção alternativa de capacidade em vez de slots, documentar equivalência e provar atualização atômica e reconciliação em RC/RR; nunca usar só `SUM` em trigger sem serialização, que admite write skew.

Capacidade N e ordinais devem comportar os 14 dígitos inteiros aceitos por F01: usar bigint ou decimal integral apropriado, com serialização string, sem Int32 ou Number JavaScript. Criar somente slots efetivamente usados, sem materializar N linhas de uma prova grande. Revisões/projeções mutáveis da âncora devem corresponder a fatos imutáveis e ser reconciliáveis.

### 3.2 Migration aditiva

Gerar ID/nome somente na implementação, após conferir a última migration real. Baseline P3.0 termina em `20261025184750_AddProductionOrdersAndExecution`, após F01 `20261007225228_AddManualInventory`. Acrescentar tabelas/índices/guards necessários, sem alterar migrations históricas e sem backfill de pontes, prova, inscrição de itens, contagem, estoque ou fatos de Animal.

A entrada nova pode reutilizar o tipo físico Entrada F01 com vínculo estruturado F03. Associação documental nunca gera EventoEstoque vazio ou quantidade zero: as guardas atuais exigem cardinalidade de movimentos físicos. Para compensação, definir extensão explícita das guardas/tipo físico, por exemplo CompensacaoEntradaOvos, através da nova migration. Conferir todas as funções F01/F02 que validam tipos, cardinalidade, snapshot, reservado ≤ físico, movimento→livro e livro→projeção. Não assumir que EventoReferenciaId ou Ajuste atual implementa inversão única. Preservar a compensação F02 e os snapshots/referências originais, inclusive nulos.

Entregar SQL incremental e idempotente revisados, hash do arquivo entregue e lista de pendências. Provar base nova, cópia isolada com dados anteriores, repetição do SQL, pg_dump/restore e ausência de mudanças nos módulos antigos. Definir Down como recusa de destruição de fatos; recuperação por forward fix/restore coordenado autorizado. Não executar Down em fatos reais nem fazer downgrade de runtime que desconheça os novos eventos sem plano de compatibilidade.

### 3.3 Coordenação e algoritmo de confirmação

Reutilizar o mesmo DbContext/transação F01. Sequência: adquirir catálogo `(413,421)` → inventário `(430,1)` e, em compensação que depende de F02, produção `(430,2)`; reautorizar usuário atual; recuperar replay; bloquear/reler Item→âncora F03 dos itens geridos→Locais→Lote→Posições e os recursos de prova/linhas de ovos em ordem fixa; comparar expectativas; gerar fatos; salvar e commit único. Todas as operações que compartilham recursos devem respeitar essa ordem, incluindo F01 gerido. Releitura após lock não pode usar valores EF rastreados anteriormente. Nenhum lock de genealogia, Caixa ou escopo de mutação Animal.

Bloqueio compartilhado das linhas `ProducoesOvos` conflitante com UPDATE garante snapshot até commit. Fato zootécnico editado primeiro força nova prévia; edição posterior preserva ponte e gera divergência legível. Não fortalecer o PUT atual por mudanças de request/response ou impedir edição por estar em estoque. Se ajuste interno se provar necessário, limitá-lo à serialização, documentá-lo e testar compatibilidade.

F01 dos itens geridos deve validar a decisão de origem/conciliação no mesmo lock e commit do recebimento. Campos/referências F03 podem ser aditivos e opcionais para itens fora do recorte; dentro do recorte a ausência bloqueia recebimento sem prova. Isso é mudança material prevista em B03 e deve constar do aceite. Recebimento de origem comprovadamente distinta atualiza corte e invalida prévia/conferência afetada; recebimento ambíguo não gera saldo. Lote de origem F03 rejeita recebimento adicional também por guarda SQL. O fluxo manual coletivo conserva F01 para origem distinta comprovada, sem gerar ponte fictícia.

Âncora por Item serializa créditos relevantes em todos os seus lotes/locais, por atualização de revisão no banco, não só advisory/corte LastSequence. Guardas/trigger devem participar do protocolo mesmo com SQL não cooperativo; verificar ordem de locks, falha de serialização em RR, rollback e retry sanitizado. Confirmar F03 também atualiza a revisão pelo seu próprio crédito, gravando no resultado o novo corte; não confundir esse efeito esperado com crédito concorrente anterior à validação. Testar ambas as ordens com outro lote/local e impedir entrada nova até nova conferência quando a revisão mudou.

Conciliação pendente pode ser registrada por fato documental sem saldo, para encaminhar conferência humana. Não gravar primeiro uma entrada F01 desprotegida para só depois ligar a ponte. SQL privilegiado que contorne a aplicação não prova equivalência física; guardas devem preservar invariantes estruturais e acusar recebimentos sem protocolo no recorte gerido. Não prometer que o índice detecta dois ovos físicos iguais em origens livres.

### 3.4 API e UI

Contratos próprios F03 com seleção explícita, operação, destinos/prova, versões/cortes, evidências e expectativas por origem. Datas e decimais vêm do servidor; sequências bigint como string; não calcular saldo com Number. Limites e paginação seguem padrões F01 (até 100 por página); definir limite explícito de seleção do comando e rejeitar excesso, sem truncamento. Não atribuir agora um tamanho de lote de ovos ao negócio.

Rotas autenticadas aditivas da ponte, DTOs validados, parsers frontend e ProblemDetails. Proposta de detalhes: candidatos com estado da ponte e impedimentos, prévia, confirmação, leitura por grupo/ID e comandos, provas/recortes Admin e tratamentos/consulta de conciliação. Garantir 401/403 também em acesso direto, expectativa de versão e token de prévia verificável. Nenhum endpoint existente é apresentado como novo já disponível.

UI segue Visualizar separado de Editar, TableRegion, seleção entre páginas com contador/revisão visível, labels por ID fora da primeira página, retorno com filtros/página, teclado e layout de 390px. Não pré-selecionar “não recebido”, coletar motivo/responsável e não oferecer botão “importar todos”. Associar mostra delta zero; entrada mostra contagem e saldo servidor. Confirmar exige clique consciente após prévia; Enter e mudanças de campos não disparam confirmação. 409 mostra IDs/razões autorizados e oferece recarga/nova conferência, sem excluir linha ou fazer retry com chave nova.

Preservar chave/payload em falha ambígua, mostrar resultado histórico do comando e consultar estado atual separadamente. Tela de tratamento explica que compensar não libera reentrada, e que invalidar associação não remove estoque. Reutilizar navegação dos módulos existentes, promovendo a rota operacional só com a vertical íntegra. A tela zootécnica pode oferecer navegação para a ponte, preservando campos, métricas, independência dos ciclos e imagens.

## 4. Matriz de aceite e validação futura

Todos os testes abaixo estão **planejados, não executados na P3.0**. Dados e IDs dos exemplos são sintéticos. Usar PostgreSQL real com conexões independentes e barreiras observáveis; Task.WhenAll isolado não prova disputa. Não trocar por SQLite/in-memory para validar locks, constraints ou migration.

| ID | Cenário | Resultado exigido |
| --- | --- | --- |
| F03-01 | Unidade individual comprovada; amostra/coletivo/desconhecido; IDs de Ciclos | Só indivíduo provado entra; demais impedidos, sem gerar quantidade/IDs/ponte |
| F03-02 | Selecionar 2 de 3 origens, uma inelegível; páginas múltiplas; repetição de ID | Seleção parcial consciente e nova prévia; confirmação não omite impedidos; duplicado rejeitado |
| F03-03 | Duas origens comprovadas → 2 un em lote dedicado vazio | Uma Entrada, duas pontes, livro/projeção +2; massa não presumida |
| F03-04 | Entrada manual sintética 5 un; associar 2 e depois 3 | Saldo permanece 5 se não houve uso; capacidade restante 3→0; sexta associação falha; nenhum EventoEstoque novo de associação |
| F03-05 | Movimento manual já consumido/transferido; mesma prova válida | Associação histórica delta zero; não restaurar saldo nem identificar qual ID foi consumido |
| F03-06 | Entrada só em kg, declaração convertida de kg, item diferente, prova insuficiente, Ajuste/transferência | Bloquear associação e entrada duplicadora; oferecer pendência documental, nunca conversão/reparo automático |
| F03-07 | Dois comandos/atores/chaves disputam o mesmo ProducaoOvoId | Exatamente uma ponte e um efeito físico; replay correto ou 409 vitalício |
| F03-08 | IDs distintos disputam último slot manual; tentar provar entrada raiz F03 como manual | Só um ocupa capacidade; SQL não cooperativo RC/RR também não excede N; prova duplicada por movimento e prova do crédito raiz F03 recusadas |
| F03-09 | Edição gravada antes/durante/depois da confirmação | Antes invalida; durante serializa por linha; depois mantém snapshot/saldo e aparece divergência; timestamp igual com conteúdo diferente também detectado |
| F03-10 | Entrada F03 versus recebimento manual do mesmo item/recorte, em outro lote/local, inclusive SQL sem advisory RC/RR | Âncora/revisão/corte e protocolo de origem; nenhuma dupla entrada sem prova; prévia obsoleta exige conferência; teste nas duas ordens e sequência, não só corrida |
| F03-11 | Inativação/capacidade/unidade/validade/local/conversão e virada do dia enquanto espera | Revalidação F01; sem liberação implícita; destino F02 produzido rejeitado; novo recebimento em lote F03 rejeitado app/SQL |
| F03-12 | Mesma chave/hash; payload divergente; autor diferente; resposta perdida pós-commit | Mesmos bytes/status/Location/IDs e autor original; divergente 409; retry não cria fato; papel removido nega replay |
| F03-13 | Falha injetada após cada fase antes do commit | Nenhum fato/projeção/slot/comando parcial; mesma chave pode tentar novamente; cadastros vazios prévios preservados |
| F03-14 | Compensar grupo de entrada intacto com prova de erro | Débito exato único de 2 un do exemplo, lote zero/terminal; pontes preservadas; mesma chave replay; nova chave não compensa duas vezes nem reentra |
| F03-15 | Reserva histórica liberada, transferência devolvida, consumo F02 ou revisão do lote alterada | Compensação integral bloqueada antes de qualquer efeito; anotação/ajuste presente vinculado, nunca restauração ancestral |
| F03-16 | Invalidar associação manual; editar zootécnico; tentar reutilizar ID/slot | Delta zero e histórico preservado; ID/slot continuam ocupados; sem remapeamento automático |
| F03-17 | Conversão explícita do lote em F02; ausência/troca posterior; peso padrão da espécie | F02 exige fator aplicável e preserva executado; peso do snapshot/espécie não converte estoque; edição não recalcula |
| F03-18 | SQL tenta editar/apagar fatos, ponte incompleta, slot fora de intervalo, troca de item, compensação divergente | FKs/checks/guardas recusam em commit; nenhum saldo negativo; reservado ≤ físico e livro = projeção |
| F03-19 | Conciliação durante escrita e corrupção injetada só em fixture | Corte consistente; acusa divergência documental/física/reservas sem writes de reparo; comando afetado bloqueado |
| F03-20 | API 401/comum/Admin, claim Admin revogada, responsável inativo; prova adulterada | Autorização atual em serviço/API antes de replay; entrada/associação/tratamento conforme D03; sem privilégio por responsável |
| F03-21 | Migration base nova, upgrade da anterior/cópia isolada, SQL idempotente, dump/restore | Sem backfill; dados anteriores íntegros; índices/guards instalados; modelo sem mudanças pendentes; Down destrutivo recusado |
| F03-22 | Browser desktop e 390px; seleção/prévia/associação/entrada/erro/retry/compensação | Efeitos reais no banco confirmados, labels/retorno/teclado legíveis, sem overflow da página ou confirmação implícita |
| F03-23 | Regressão Animal/ovos/Ciclos/F01/F02 e suites integrais | POST/GET/PUT e métricas zootécnicas preservados; nenhuma ponte por ciclo/seed; suites/lint/build e CI no SHA correto |

Padrões de testes existentes: `InventoryApiTests` (último saldo, atomicidade, idempotência, permissão atual), `InventoryAdditionalGateTests` (unicidade/capacidade), `ProductionAtomicityTests` (falha em fases/SQL concorrente), `ProductionRecoveryTests` (resposta perdida/timeout/reconciliação), `ProductionAdditionalGateTests` (compensação/referências), `ProductionGenealogyTests` (conversão/consumo), migrations Inventory/Production. Adaptar aos riscos F03, sem testes que apenas espelhem o número de arquivos ou a implementação.

## 5. Execução futura e entrega

Antes de iniciar API/frontend: identificar revisão e conexão tipada efetiva, banco/ambiente e cadeia processo/listener/socket/pg_stat_activity quando aplicável; usar base nova ou cópia isolada inequívoca, sem dados sintéticos em fonte/piloto. Conferir migrations exigidas e histórico selecionado, revisar SQL de todas as pendências e aplicar somente no destino isolado autorizado antes do teste funcional. Banco compartilhado/produção exige autorização específica. Não reaproveitar porta ou alias histórico como prova do destino.

Para cadastros afetados com imagem, configurar `Images__PrivateRoot` na pasta exata contendo os arquivos diretamente, permissões e volume privado existentes; conferir chaves de original/miniatura contra arquivos, sem divulgar caminhos. Depois de iniciar a API nessa configuração, provar respostas autenticadas `200 image/png` para original e miniatura, controle anônimo conforme arquitetura e renderização na UI. Volume existente/configurado sem essas provas não satisfaz o gate. Se 404/substituta, corrigir antes de declarar aplicação pronta. Fixture sem imagens não aceita esse gate do piloto; registrar limites e cumprir o gate pertinente à execução solicitada.

Backend: `dotnet test GenSW.sln --configuration Release` com PostgreSQL real configurado externamente e resultados de falhas/ignorados inspecionados. Frontend: `npm ci`, `npm test`, `npm run lint`, `npm run build`. Usar runtime/versões do repositório/CI efetivos. Não repetir gates amplos já aprovados sem nova alteração ou preocupação, mas executar matriz própria e regressão apropriada após implementação. Reavaliar exposição/remediação dos avisos npm antes do piloto na #493 ou em tarefa própria se bloqueantes; não converter o registro 14/4 histórico em aceite residual nem fazer atualização ampla nesta P3.0.

Revisão de contrato/código/SQL/UI/segredos, links, exemplos sintéticos e `git diff --check`. Evidência sanitizada de migration, preservação, concorrência, livro/projeção/reservas, API/browser e recuperação. Branch/commit/push/PR apenas quando efetivamente autorizados/criados; PR criada deve ser anexada ao chat. CI deve corresponder ao SHA entregue; CI da baseline não vale por implementação. Merge/deploy/publicação/migration compartilhada conservam autorização própria.

Ao terminar tecnicamente: anexar evidências e registrar base/branch/commit/push/PR/CI reais, validações, limitações, pendências/donos e o aceite funcional ainda necessário. Mover Tarefa para Em validação e confirmar leitura. Manter #430/#493 abertas; não chamar F03 aceita por código verde. O aceite funcional humano e a validação geral final não são antecipados pelo plano.
