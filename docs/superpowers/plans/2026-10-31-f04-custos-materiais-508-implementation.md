# P4.1 — plano incremental condicionado de F04

31/10/2026 · P4.0 [#508](https://devops-lab.tailaf9418.ts.net/issues/508), Evolução [#430](https://devops-lab.tailaf9418.ts.net/issues/430). **Plano futuro, sem código/migration autorizados nesta entrega.** Base examinada `0f05ac810bfcbe4caf579b05a6decaf3b6e63baf`. [Contrato](../specs/2026-10-31-f04-custos-materiais-508-design.md), [decisões](../specs/2026-10-31-f04-custos-materiais-508-decisions.md), [matriz C01–C35](../specs/2026-10-31-f04-custos-materiais-508-scenarios.md) e [instrução](2026-10-31-p4-1-implementacao-f04-508-prompt.md).

## Gates anteriores a qualquer implementação

1. Reconsultar journals da #430/#508/#495/#493/#509 e PR/CI recentes, confirmar versão do contrato. Reaproveitar aceites humanos já registrados, sem nova votação dos mesmos pontos.
2. Registrar decisão expressa de prioridade D04-01 e de dependência D04-02. Recomendação: F02 aceita funcionalmente antes de código F04. #495 ainda não tem esse aceite nesta base; #493 continua Planejado, não executável por este plano. Alteração do gate exige decisão humana específica, sem inferência do merge F02/F03.
3. Registrar A: aceite explícito do contrato/decisões/cenários desta revisão. Registrar B, separadamente: autorização P4.1 e recorte (recomendado C1–C5 completos). A não implica B. Se faltar qualquer gate, entregar situação e parar antes de código/migration.
4. Quando autorizado, criar Tarefa própria P4.1 filha #430 e relacionada #508/#495/#493; objetivo/escopo/restrições/aceite, Em andamento confirmado por leitura. Usar API Redmine/header e chave exclusivamente de REDMINE_API_KEY; papel dedicado Codex, sem mudança global. Branch `codex/<tarefa>-f04-custos-materiais`, da main atualizada, preservando trabalho local e AGENTS.md fora do Git.

## C1 — domínio e contratos monetários

Criar vertical específica `MaterialCosts` em Domain/Application/Infrastructure/API e `material-costs` no React, seguindo padrões existentes antes de fixar nomes finais. Sem framework universal, reaproveitar parsers decimais e infraestrutura autenticada quando compatíveis.

Implementar referência documental separada de comprovado; evidência com componentes/lacunas; camada e fragmentos, eventos/parcelas, cobertura, apuração/destinos e compensação. Aplicar limites/BRL/unidades e algoritmos exatos do contrato; seleção de camadas por origem/ID; rateio por componente em centavos pelo maior resto, desempate Guid D ordinal; unitário positivo com arredondamento declarado. Não usar unitário arredondado como fonte do total.

Definir DTOs versionados F04, snapshot de estimativa e apuração com algoritmo/fontes/revisões/cortes e IDs de perdas novos, sem mudar registros antigos. Explicitar estados Completo/Parcial/Desconhecido e PendenteRateio; propriedades totais nullable. Operação sem dados F04 preserva físico e registra lacuna/pendência. Testes de domínio C01/C05–C16/C33/C34 antes da persistência. C1 isolado não é vertical disponível ao usuário.

## C2 — persistência e migration aditiva

Adicionar tabelas propostas do contrato, FKs Restrict para identidades físicas/usuários/referências, índices por item/lote/local/origem/corte, unicidade origem/camada, comando/recurso/operação/chave, compensação única e revisão de projeção. numeric(18,2) total, numeric(20,6) físico/unitário, numeric(9,6) percentual; JSONB tipado/versionado. Evitar defaults monetários que criem zero para ausente.

Guardas PostgreSQL append-only e consistência imediata/diferida: quantidades/centavos conservados; cobertura sem duplicação; origem e destino da camada compatíveis com movimento físico; cada saída positiva/ID de perda válido; soma percentual exata; desconhecido sem total completo; execução atomicamente ligada; compensação única e estado compatível. Guardar os casos SQL de inserção em ordem inversa que motivaram a correção F03 #503; não depender apenas da ordem do serviço.

Criar somente migration nova, modelo atual e snapshot EF atual correspondentes. Não editar as 20 migrations anteriores, Designers, eventos/snapshots/ledger/respostas ou fatos do Caixa. Sem backfill/seed de custo, camada, preço ou saldo legado. Sem pré-criar F05/F06. Se precisar atualizar função SQL atual para anexar guardas F04, fazê-lo pela migration nova, preservando/retestando contratos anteriores; nada de alteração de dados históricos.

Gerar SQL completo das pendências efetivas para base nova e cópia isolada anterior; revisar constraints/triggers, ausência de DML histórico e idempotência. Testar criação/upgrade/reexecução SQL, checksums de todos os fatos anteriores antes de fixtures e pg_dump/restore. Revisar testes históricos com ausência de tabelas futuras para que continuem testando sua migration de referência, sem enfraquecer preservação. Verificar drift EF. Down destrutivo não é recuperação operacional; após dados reais, forward fix ou restore coordenado autorizado.

## C3 — casos de uso transacionais e integração F01–F03

Integrar pelo serviço/unidade de trabalho, não por sequência HTTP/`MoveAsync` pública. Reutilizar GenSWDbContext e protocolos: catálogo `(413,421)` → inventário `(430,1)` → produção `(430,2)` quando pertinente → linhas ordenadas. Não introduzir lock de Caixa nem caminho F04 sem coordenação física. Sessão/papéis ativos revalidados após locks, antes de replay. Escrita ReadCommitted, leitura consistente RepeatableRead/read-only, revisão otimista e timeouts explícitos.

Recebimento: custo conhecido/desconhecido e evidência na mesma transação do crédito; proteger lote contra custo/proveniência incompatíveis. Fluxos antigos omitindo F04 produzem desconhecido. Material legado ganha reconhecimento presente de quantidade desconhecida somente quando necessário para novo movimento, sob lock e sem escrever passado. Transferência/retirada/descarte/ajustes operam parcelas e resíduos; reversão exata é vinculada e restrita.

F02: congelar custos dos consumos reais na confirmação, liberar/liquidar reservas e criar camadas de saída com cobertura herdada. Rateio completo C01–C16 e perda total; sem segundo débito de perda. IDs de perdas estáveis no novo envelope. Intermediário consome só a camada própria. Clientes antigos sem rateio preservam o fato físico e uma apuração pendente com conhecido não distribuído, saídas desconhecidas e lacunas explícitas. Não chamar conhecido não distribuído de valor em estoque nem somá-lo de novo aos destinos. Resolução posterior só por avaliação/apuração presente vinculada; sem mudar snapshot original, e sem atualizar automaticamente saída já movimentada/consumida.

F03: associação delta zero não abre camada; entrada sem prova monetária é desconhecida; compensação física retira parcelas exatas; invalidação documental não movimenta valores. Proteger recebimento em lote dedicado/produzido, inclusive SQL/ordens concorrentes.

Idempotência: preservar escopos/respostas F01–F03; comandos F04 independentes de autor por recurso/operação/chave, hash canônico com todos os dados relevantes e resposta durável no commit. Tratar falha antes/depois do commit, retry com mesma chave e payload distinto; não permitir dois endpoints custearem a mesma origem. Correção Admin e avaliação presente com guardas de uso posterior; reconciliação somente leitura por corte, divergência nunca reparada automaticamente.

## C4 — API e interface completas

API autenticada: rotas de referência/publicação/supersessão, lote/camadas, execuções/apuração, avaliação/compensação/histórico, estimativa explícita e reconciliação do contrato. Extensões versionadas de prévias/confirmações F01–F03 carregam custos/rateio sem segundo commit. Paginação/filtros/cortes, fonte autorizada e erros estáveis; schema de nullable/strings validado, não somente TypeScript por coerção. Authorization no serviço e controller; testes 401/403 inclusive papel revogado e replay.

React: lista/detalhe/referência/estimativa; recebimento com escolha conhecido/desconhecido e fonte/data; lote com camadas/cobertura; confirmação F02 com rateio por parcela/perda, prévia em centavos calculada no servidor e escolha explícita de pendência quando não resolvido; apuração/snapshots; avaliação/correção Admin e reconciliação. Separar estimado/referência/comprovado/parcelas conhecidas/lacunas, com total completo nulo quando pertinente. Saída zero sem unitário; nenhuma margem/lucro ou ação Caixa.

Seguir navegação/detalhes existentes: seletores paginados, histórico legível, referências fora da página retidas, voltar com filtros/página; teclado e 390px/desktop; estados carregando/vazio/erro e recuperação; invalidar prévia ao editar ou mudar revisão; manter chave em resposta perdida, bloquear duplo envio. Não habilitar menu/ação de entrega parcial incapaz de recuperar ocorrência/reversão. Documentar operação somente quando implementada.

## C5 — verificação integrada e entrega revisável

| Gate | Execução exigida quando P4.1 estiver autorizada |
| --- | --- |
| PostgreSQL real | C03–C30 pertinentes, incluindo RC e RR com barreiras para disputas/guardas; sem trocar por SQLite nem ignorar testes sem PG. Usar padrão `EphemeralPostgreSql`/factories existentes. |
| Aritmética | Fixtures C05–C16/C33/C34 e propriedades de soma/conservação, permutações de destinos e GUIDs/culturas, valores limite e cobertura; não repetir apenas a fórmula do código sob teste. |
| Migration/preservação | C29, upgrade da revisão anterior, 20 migrations históricas intactas, DML antigo ausente, hash de linhas/snapshots/replays preservado; SQL idempotente, pg_dump/restore e modelo sem drift. |
| API real | Permissões/validação/nullable, versões, idempotência/replay, timeout/rollback e integrações F01–F03, referência versus comprovado, Caixa sem escrita. |
| Navegador real | C02/C10–C14/C21–C22/C25–C26/C31–C35, desktop/390px/teclado/retry. Registrar evidência sanitizada no candidato; não declarar humano aceito por fixtures. |
| Regressão/CI | `dotnet restore GenSW.sln`; `dotnet build GenSW.sln --configuration Release --no-restore`; `dotnet test GenSW.sln --configuration Release --no-build`; no frontend `npm ci`, `npm test`, `npm run lint`, `npm run build`. PostgreSQL disponível conforme `.github/workflows/ci.yml`; registrar falhas iniciais/retestes sem reclassificá-los como run único verde. |
| Governança | Diff/escopo/segredos/referências, `git diff --check`, branch/commit/push/PR e CI com SHA exato e árvore da referência PR quando houver merge sintético; Em validação. Sem merge/deploy/Concluído por inferência. |

Antes de executar a aplicação para validação: identificar banco/ambiente escolhido e comprovar destino efetivo da API; conferir history e SQL de todas as migrations exigidas, aplicar somente no destino isolado autorizado. Compartilhado/produção exige autorização específica. Preservar serviços, dados e backup existentes. Configurar raiz privada exata das imagens, arquivos/chaves/permissões/volume, comprovar miniatura e original autenticados `200 image/png`, anônimo recusado e renderização real quando cadastros afetados forem usados; 404/placeholder impede declarar ambiente pronto. Não publicar caminhos/credenciais privadas. Nada disso é executado pela P4.0 documental.

Entregar domínio/persistência/API/UI completos no recorte autorizado, documentos operacionais atualizados e matriz com executado/não executado/pendências por cenário. Critério de entrega técnica não é aceite funcional humano. Anexar PR ao chat, registrar SHA/CI/evidências na Tarefa P4.1 e mover a Em validação; #430/#493 continuam abertas. [#509](https://devops-lab.tailaf9418.ts.net/issues/509) deve ter exposição/remediação e decisão residual tratadas antes do piloto, em trabalho separado; CI verde não aceita os 21 avisos.
