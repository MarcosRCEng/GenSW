# P5.1 — plano incremental F05 condicionado

Data: 09/11/2026. P5.0 [#525](https://devops-lab.tailaf9418.ts.net/issues/525), filha da #430. **Plano proposto, sem autorização de implementação.** Ler [contrato](../specs/2026-11-09-f05-otimizacao-525-design.md), [decisões D05-01–12](../specs/2026-11-09-f05-otimizacao-525-decisions.md), [cenários S05-01–28](../specs/2026-11-09-f05-otimizacao-525-scenarios.md), [solver](../specs/2026-11-09-f05-otimizacao-525-solvers.md) e [prompt copiável](2026-11-09-p5-1-implementacao-f05-525-prompt.md).

## Gate anterior a código e dependências

Registrar aceite humano explícito da revisão do contrato e das decisões, incluindo alterações, **e autorização do recorte P5.1 N1/B1–B6**. Aceite pode vir junto da autorização, mas são atos distintos. Se for aprovado objetivo econômico/disponibilidade/MS/inteiros, revisar documentos/cenários antes de código. A P5.0 não cria Tarefa de implementação antecipada.

Na nova janela: ler AGENTS local; criar Tarefa própria filha da #430 com objetivo/escopo/restrições/aceite; mover para Em andamento e confirmar antes de investigar. Reconsultar aceite, #525, main/fetch, PR36/37, #493/#509. Não reutilizar #524/#525 como execução. Trabalhar em branch `codex/` baseada na main atualizada, preservando arquivos locais e o runtime operacional em uso. PR37 não é dependência do solver; se ainda aberta, preservar sua revisão separada. Testes OpenAPI em Development devem considerar o defeito conhecido e não absorver sua correção silenciosamente.

Entrega autorizável única N1, em seis incrementos. Cada incremento tem evidência própria; nenhum parcial é chamado F05 operacional. Não estimar prazo fechado antes de aceitar o recorte.

## B1 — domínio, modelo e adapter backend verificável

1. Criar tipos próprios sob `Optimization` em Domain/Application e um adapter em Infrastructure, seguindo a separação existente, sem refatorar catálogo/formulação/estoque/custos. Domain valida inputs e matriz de elegibilidade; não depende de EF/HTTP/biblioteca nativa. Interface de solver recebe modelo tipado, devolve status bruto/solução/diagnóstico; não decide política nutricional.
2. Resolver explicitamente baseline, perfis/versões, definições, limites e metas; validar N1, unidades/contexto/política e excluir somente linhas humanas com U=0. Reaproveitar normalização/comparabilidade existente por componentes pequenos, evitando duplicar equivalências ou mudar a semântica manual. Racionais preservam coeficientes/transformações exatas; modelos nativos têm matriz/escala/hash próprios.
3. Montar LP de desvio L1, sem pesos de nutrientes/custos, sem estoque, sem relaxamento automático. Implementar verificações analíticas e verificador de certificado; se o certificado nativo não puder ser validado, retornar inconclusivo. Não implementar motor exato de otimização artesanal.
4. Somente após autorização, provar adapter HiGHS/net8.0 e licenças/avisos/proveniência do pacote efetivo. Fixar versão; validar Windows e Linux, callbacks/status/ray/tempo. Se embalagem/ABI não satisfizerem os gates, registrar evidência e avaliar a alternativa documentada, sem mudar silenciosamente o contrato.
5. Executar solver em processo filho supervisionado local, usando pipes tipados, sem rede externa e sem gravar fórmulas em arquivo temporário/log. Um processo por execução, encerrado/descartado no fim; Windows sem janela visível. Limite nativo5s menos o tempo consumido em diagnóstico; watchdog10s do supervisor cobre travamento. Concurrency1 por instância e sem fila ilimitada; cancelamento HTTP não perde resultado já aceito. Não manter transação/lock de catálogo/físico aberto enquanto o nativo calcula.
6. Validar vetor bruto finito, dimensão/sinais/resíduos; quantizar por maiores restos e revalidar exatamente todas as restrições no modelo original. Preservar brute/quantizado, D/D2, folgas, contribuições e prova/status. Timeout sempre preserva sua categoria. Prévia e aceite de resíduos para derivação, sem mudar quantização física F01.

Gate B1: S05-01–24 e S05-28 pertinentes ao domínio/adapter, inclusive relógio controlado, crash/watchdog, escala extrema, perda de coeficiente e certificado inválido; comparação com oráculos analíticos, sem depender só do próprio solver para confirmar seu resultado. Casos de certificado geral podem ser inconclusivos, mas os inviáveis analíticos obrigatórios devem ser demonstrados.

## B2 — persistência e execução recuperável

Propor tabelas próprias, nomes finais conforme convenções do repositório: `ExecucoesOtimizacao`, `ResultadosOtimizacao` e `DerivacoesOtimizacao`. Identidades/autor/UTC/status/revisão/hash/chave em colunas; pedido, entradas, modelo e resultado em snapshots JSONB tipados versionados. `Resultados` é append-only, um terminal por execução; `Derivacoes` liga a ação humana à nova receita/versão e recibo, com snapshot da revisão inicial derivada. Contexto/metas/fontes são consultados pelo vínculo F05→RecipeVersion, sem presumir campos ausentes em `RecipeData` ou colocar JSON em `Observacao`. Não ampliar os tipos de `SnapshotsFormulacao` nem editar seus conteúdos/recibos históricos.

Idempotência de execução: escopo autor/operação/chave, como simulações atuais, com índice único; mesmo hash canônico retorna a identidade e o resultado original, hash diferente409. Canonicalizar números e ordenar conjuntos por ID sem apagar ordem que tenha significado; rejeitar IDs duplicados. Chave não contém segredo. Revalidar sessão atual inclusive no replay; respostas não expõem a outro autor uma chave de execução por colisão.

Fluxo proposto, deliberadamente sem broker/fila genérica:

1. Consultar chave/estado com sessão válida **antes** do gate de capacidade: replay não consome vaga. Para pedido novo, adquirir vaga limitada; numa transação curta consistente, reconsultar atomicamente a chave para resolver a corrida (replay libera a vaga), verificar revisões, resolver/copiar entradas e gravar execução `EmCalculo` com token de propriedade e deadline. Commit antes de chamar nativo. Replay de execução ainda em curso devolve202 e Location do mesmo recurso; não inicia outro cálculo. Sobrecarga de novo pedido retorna429/Retry-After sem execução nova, mantendo chave no cliente; reconferir a chave antes desse429 para não perder replay concorrente.
2. Supervisor calcula fora da transação, com snapshot congelado; escreve um único resultado terminal em transação curta, compare-and-set por token/estado. Revalida sessão para novas mutações de domínio; não recria pedido se o HTTP cair. Finalizar um resultado técnico já iniciado não publica nem cria receita.
3. Prazo durável de execução30s a partir do início (maior que watchdog e gravação normal). Recuperação no startup e varredura interna limitada marcam execução vencida como `ErroAlgoritmo/ExecucaoInterrompida`, sem rodar solver automaticamente; CAS impede finalização dupla. Em múltiplas instâncias respeitar token/deadline de proprietário vivo, sem marcar imediatamente toda execução de outra instância. Se o supervisor matou por tempo, resultado é `LimiteTempo`, não crash genérico. GET é somente leitura.
4. Desfechos sem chamada ao solver (dados incompletos/incompatíveis/prova analítica) também têm snapshot/resultado terminal. Erros sintáticos, 401/403/404 e conflitos de revisão antes da aceitação do comando seguem ProblemDetails; não alegar resultado nutricional desses pedidos não aceitos.

Migration futura aditiva apenas dessas estruturas/índices/FKs Restrict/checks/guardas de imutabilidade. Sem seed/backfill/tabela vazia de metas por espécie. Preservar as21 migrations anteriores, simuladores, custos, reservas, fatos e Caixa. Gerar/revisar SQL e aplicar somente em PostgreSQL efêmero novo e cópia **sintética isolada** de baseline; comparar antes de fixtures todas as tabelas/linhas/IDs/FKs/snapshots/recibos anteriores, provar replay e reaplicação idempotente. Down destrutivo não é plano operacional.

Gate B2: S05-25/26/28; duas chamadas concorrentes com mesma chave; conflito diferente; resposta perdida; crash após captura e antes/finalização; expirado/CAS; rollback e ausência de novo cálculo em GET/replay; preservação da baseline. Nenhuma migration em OPERACIONAL-REAL/compartilhado sem autorização específica posterior.

## B3 — API autenticada e contrato HTTP

Rotas novas propostas sob `/api/v1`, sem alterar rotas anteriores:

| Rota | Contrato proposto |
| --- | --- |
| `POST /otimizacoes-formulacao` | Idempotency-Key obrigatória; massa/baseline/candidatos/perfis/revisões/limites/metas/política/contexto/versãoModelo. 201+Location com recurso terminal no fluxo normal; replay201 do mesmo terminal;202+Location/Retry-After se mesma execução ainda em curso. Não usar500 para inviabilidade ou dados incompletos aceitos. |
| `GET /otimizacoes-formulacao` | Paginação25/máximo100, filtros de data/desfecho/contexto, ordenação permitida e ID de desempate; não expor fórmulas/documentos privados em logs. |
| `GET /otimizacoes-formulacao/{id}` | 200 recurso congelado transitório ou terminal; resultado/contribuições/folgas/fontes autorizadas. Sem recalcular/atualizar fonte por GET. |
| `POST /otimizacoes-formulacao/{id}/previas-rascunho` | Ação humana explícita, sem gravar receita; destino/saída/versão/revisão atuais, quantidades e resíduos, avisos/contexto; hash de prévia e resultado congelado. Bloqueia qualquer terminal diferente de SolucaoViavel. |
| `POST /otimizacoes-formulacao/{id}/rascunhos` | Idempotency-Key, hash de prévia, destino/revisões e aceite explícito de resíduos/avisos. 201 nova receita+versão rascunho ou nova versão de receita existente, vínculo/recibo persistidos atomicamente. Nenhuma publicação. |

Proposta de payload econômico/disponibilidade explícito fora N1 resulta no terminal `DadosIncompativeis/ModoForaRecorte`; nunca trocar para nutricional. Campo numérico em string invariante; estado/desfecho/códigos em enum validado. Requisição não contém a matriz calculada pelo cliente como autoridade. IDs de exemplo serão de fixtures, nunca de registros reais.

400 formato/precisão inválida;401 sessão;403 autorização;404 recurso inexistente;409 revisão/chave/prévia obsoleta;429 capacidade;503 indisponibilidade de persistência antes da aceitação, com orientação de consultar chave antes de tentar novamente quando a resposta for incerta. Falha do solver **depois** da aceitação é terminal `ErroAlgoritmo` recuperável por consulta. Manter códigos seguros, sem SQL/stack/path privado. Recuperação por chave autenticada em `GET /otimizacoes-formulacao/por-chave?chave=...` (registrar rota literal antes do Guid), sem payload privado na query; retorna recurso conhecido ou404, não relança cálculo.

Permissões: execução/consulta por sessão conforme formulação; rascunho conforme operações atuais, sessão/papéis conferidos no serviço. Nenhum papel novo. Criação atômica do rascunho usa o lock de catálogo existente `(413,421)` e validações/revisões atuais. Não adquirir locks físicos/Caixa para F05. Saída escolhida humana deve ser item elegível para a receita atual; não inferir ItemId de saída a partir de ingredientes. Copiar x>0 em quantidades absolutas, manter zeros apenas no resultado. Previa com referência agora inativa/alterada retorna conflito; usuário decide nova avaliação.

Gate B3: contratos HTTP nos sete desfechos, enums/precisão, autenticação/replay, recusa de modo fora do recorte e rascunho inválido, concorrência/transação da derivação, fontes autorizadas sem vazamento. Teste de pós-commit com resposta interrompida deve recuperar execução e rascunho sem duplicar.

## B4 — interface e fluxo humano

Adicionar ação “Otimização assistida” dentro da área de formulação existente, somente com a vertical N1 íntegra. Importar receita/versão escolhida ou baseline manual; M visível, candidatos alimentares kg e perfis explícitos, limites importados/alterados com confirmação, metas/contexto/fontes e política de estimativas por meta. Seletores paginados completos e referências fora da página resolvidas; nenhum primeiro resultado de busca escolhido automaticamente.

Antes de executar, mostrar problema resumido: menor desvio, massa BN, ingredientes/exclusões, metas duras, estimativas, fora de escopo economia/estoque. Painel candidato×meta com coeficiente/fonte/derivação e motivo de bloqueio, sem converter erro de leitura em célula vazia. Não oferecer preencher lacuna com zero.

Resultado: baseline×solução, D e massa redistribuída, contribuições expansíveis, proveniência, valores/folgas/restrições ativas, corte e versões, distinção bruto×quantizado/resíduos, status claro em texto. Mesmo SolucaoViavel mostra aviso de não certificação de dieta e ausência de disponibilidade/custo. Inviável mostra prova/contradição; incompleto lista fontes necessárias; incompatível identifica campo/contexto; timeout/erro/inconclusivo têm ações de nova tentativa **explícita**, sem retry que cria pedido novo silenciosamente.

Preservar chave/recurso em timeout HTTP e navegação; consultar por chave antes de repetir comando. Desabilitar clique duplicado ajuda UX, unicidade do servidor decide. Novo pedido alterado recebe chave nova e referência ao anterior. Não transformar polling em recálculo.

Rascunho: botão separado só para SolucaoViavel; prévia e confirmação explícita, escolha de destino/saída e aceite de resíduos; avisar que é rascunho. Após201, abrir editor existente com origem F05; edição subsequente não modifica snapshot nem receita publicada. Usuário pode desistir sem efeito de receita. Não criar ordem/reserva por esse fluxo.

Gate B4: navegador real autenticado em ambiente sintético, teclado/foco, desktop/390px, loading/vazio/erro/401/403/404/409/429, sete desfechos e ação humana. Preservar Visualizar separado de Editar, filtros/retorno e testes manuais de receitas/simulações/custos/estoque anteriores pertinentes.

## B5 — validação integrada e preservação

Matriz obrigatória S05-01–28 rastreada por camada e evidência. Os casos econômicos são de recusa de recorte em N1 e oráculos da proposta futura, **não teste de economia implementada**. Validar prova independente, estimativa excluída/aceita, qualificadores não pontuais, zero explícito, falta de MS na normalização, incompatibilidade de energia, IDs repetidos, escala extrema, arredondamento1/3 e pós-quantização, limite/erro e recuperação. Timeout é controlado por relógio/adapter, acompanhado de smoke real de watchdog; não depender de lentidão aleatória.

Rodar suites backend apropriadas e regressão completa, frontend testes/lint/build, PostgreSQL real efêmero para persistência/concorrência/migration, navegador real e CI no SHA entregue. Conferir fonte/modelo/versão e licenças no pacote de publicação Windows/Linux; nenhum benchmark fictício ou simples chamada nativa é aceite do contrato.

Antes de executar a aplicação de validação: comprovar conexão para banco/volume **isolados**, histórico exigido e SQL de todas as migrations pertinentes; aplicar apenas ali. Se cadastros com imagens forem atingidos no roteiro, conferir raiz exata/arquivos/permissões, miniatura e original autenticados200 image/png, anônimo401 e renderização real, conforme AGENTS. Não usar dados/imagens reais como fixtures, não reiniciar a operação real nem lançar registros para testar F05. Gates gerais #493 e npm #509 continuam separados, sem declarar risco aceito por CI verde.

## B6 — entrega e aceite funcional F05 próprio

Atualizar documentação operacional de F05 e evidências sanitizadas, matriz executado/não executado/falhou, limitações, versão/binários/licenças/SQL e plano de recuperação. Publicar branch `codex/`, commit/push e PR revisável, monitorar CI no SHA final. Não fazer merge automático.

Registrar tudo na Tarefa de implementação; mover para Em validação. Apresentar ao responsável o fluxo sintético, provas e criação explícita de rascunho para aceite funcional F05. Contrato aceito não substitui esse aceite; nenhuma Tarefa fica Concluído com revisão/merge/aceite pendente. Não alterar estado da #430/#493/#509 por inferência.

Se depois houver prioridade por economia/disponibilidade/MS/inteiros, abrir especificação/decisões próprias de extensão F05, com dados e escopo aprovados. Isso não autoriza F06 e não amplia N1 silenciosamente.
