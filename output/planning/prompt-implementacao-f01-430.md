# Executar F01 — estoque manual e lotes, após aceite do contrato

Preparado em 07/10/2026 sob a [Tarefa #431](https://devops-lab.tailaf9418.ts.net/issues/431), vinculada à [Evolução #430](https://devops-lab.tailaf9418.ts.net/issues/430), relacionada à #413. Este arquivo é uma instrução futura: **sua preparação/abertura não autoriza implementação**. A mensagem humana deve aceitar o contrato de F01 e autorizar sua execução, ou informar ajustes. Se o aceite e a autorização já forem explícitos, não pedir novamente a mesma aprovação.

Trabalhe em `C:\Users\Marcos\Documents\ChatGPT\GenSW`. Leia integralmente o `AGENTS.md` local, sem versioná-lo. Conclua a vertical funcional F01 aprovada, com backend, React navegável, testes PostgreSQL reais, operação e PR revisável. Não faça merge, deploy ou migration em banco compartilhado por efeito deste prompt. F02–F06 não estão incluídas nesta implementação.

## Contratos que devem ser lidos integralmente

1. `docs/superpowers/specs/2026-10-07-continuidade-f01-f06-430-design.md`, com F01 §§2–6 e preservação §9; F02–F06 como dependências futuras.
2. `docs/superpowers/plans/2026-10-07-continuidade-f01-f06-430-implementation.md`, passos F01.0–F01.6 e matriz T01–T16.
3. `docs/operations/catalogo-formulacao.md` e `docs/validation/2026-10-06-catalogo-formulacao-413.md` — contrato integrado e aceite anterior, sem autorização das fases físicas.
4. Design/plano anteriores de 2026-10-05 #413 — histórico completo; propostas físicas agora revalidadas pelo contrato de 07/10. Não executar P01–P05 novamente.
5. README, `docs/architecture/overview.md`, navegação/rotas/CI e instruções locais aplicáveis.
6. `docs/operations/animal-evolution.md` e `docs/validation/2026-10-04-visualizar-cadastros-397.md` antes de iniciar/reiniciar APIs ou validar imagens.

A base verificada na preparação era `origin/main` `dda69509c01b4a27bc6b6b5c3f2add842c441a53` (MVP-1 integrado pelo PR #20). Faça fetch e reconsulte main; não force reset a esse hash nem diagnostique pela branch original `codex/415-planejamento-producao`, anterior à implementação. O contrato novo tem branch documental própria `codex/431-planejamento-f01-f06`; sua existência/PR não significa aceite humano. Leia a versão efetivamente aprovada e registre mudanças posteriores.

## Gate inicial e rastreabilidade

- Confirme que a mensagem humana aceita F01, incluindo D06-F01 (papéis), V01 (validade não informada) e Q01 (quantização/resíduo), e autoriza implementação. Se solicitar apenas executar/ler este arquivo sem aceite material, entregue/ajuste o pacote revisável e mantenha código/migrations pendentes. Silêncio não é aprovação.
- Antes de investigação técnica deste novo prompt, valide Redmine `https://devops-lab.tailaf9418.ts.net`, projeto `gensw`/23, via `X-Redmine-API-Key` obtido exclusivamente de `REDMINE_API_KEY`; chamada autenticada de leitura antes de pedir login. Não imprimir/gravar chave.
- Consulte #430/413/431 e demandas abertas. Crie Tarefa própria de implementação sob #430, objetivo/escopo/restrições/aceite, percorra estados até Em andamento e confirme por leitura. #431 é planejamento, #413 está concluída no MVP-1; não reutilizar/reabrir. Não criar tarefas F02–F06 sem execução autorizada nem alterar papéis/workflow globais.
- Inspecione status/worktrees/processos. Branch `codex/<nova-tarefa>-f01-estoque` da main atual; reuse worktree adequado ou crie isolamento gerenciado. Preserve checkout que serve homologação e todos os untracked, em especial AGENTS, `.gensw/`, `.playwright-cli/`, `.test-output/`, `.verify-output/`, `output/`. Não criar nova janela/chat automaticamente.

## Escopo integral de F01

Implemente exatamente o contrato aprovado e os passos F01.0–F01.6:

1. Locais explícitos com código/status/histórico, finalidade Ordinario/Segregacao e Propriedade opcional; sem depósito/ACL por inferência.
2. Lotes físicos de Item existente, código interno único, origem/fonte/responsável/datas/validade/situação/ativo e perfil/conversão explicitamente aplicáveis. Cadastro vazio não gera saldo; fixa unidade do Item. Laudo textual não vira lote retroativo.
3. Estoque inicial vazio. Abertura Admin por inventário declarado única por posição sem eventos; recebimentos operacionais autenticados; transferências atômicas, saída justificada/consumo interno; ajuste Admin por quantidade contada alvo; segregação/descarte/status/validade Admin nos comandos explícitos.
4. Livro imutável/projeção transacional por lote/local, posição zero conservada, sequência crescente, quantidade não negativa e contagem inteira. Unidade canônica congelada, backend autoridade do saldo.
5. Vencido/Bloqueado/inativo conservam saldo/história e impedem uso ordinário. Recebimento vencido/pendente em segregação bloqueado; tratamentos Admin conforme contrato. Sem override de consumo vencido, nenhuma validade inventada.
6. Decimal/string invariantes; até 14+6, conversões exatas e par contextual direto/inverso por divisão pelo fator original selecionado, sem cadeia/fator padrão. Prévia canônica/resíduo e aceite de ToEven quando necessário; recusar zero resultante, overflow ou un fracionária. Transferência/segregação/retorno recebem canônica e usam o mesmo valor sem reconversão/drift; sequência/corte bigint via strings.
7. Advisory catálogo `(413,421)` antes de físico `(430,1)`, depois rows em ordem estável, timeout5s; catálogo e física realmente sincronizados. Não adquirir lock de Caixa/Genealogia/Animal nem abrir transações aninhadas por serviços alheios.
8. Chave/revisões obrigatórias em novas mutações, hash canônico tipado, replay durável antes de revalidar estado/versão, resposta/status/Location originais; payload divergente conflito. Rollback de livro/projeção/metadados/auditoria/idempotência em falha. Reconsultar usuário ativo/papéis atuais via GetCurrentUserAsync depois dos locks e antes do replay; JWT anterior não prova papel atual.
9. API autenticada completa, ProblemDetails, GETs individuais/listas/históricos, prévias read-only sem chave/gravação e com papel da operação, reconciliação sem reparo; paginação/filtros/escaping/corte consistente. Criar GET paginado/individual `/estoque/responsaveis`, id/nome/ativo mínimos e consulta histórica; novo fato exige responsável ativo.
10. React `inventory`, Locais/Lotes/Saldos/Movimentos/reconciliação e comandos, Visualizar separado de Editar, filtros/página no retorno, seleção histórica por ID/label, prévia/retry, sessão/papel/conflito e desktop/celular/teclado. Promover Estoque somente com destinos reais.

Exclusões: ordens, reservas, execução/ponte de ovos, preços/custos/rateio, solver, WIP, compra/venda/fiscal ou geração de Caixa. Não pré-criar suas entidades/tabelas/rotas/colunas vazias. Não alterar contratos Animal/Filiação/reprodução/ovos/Propriedades/Caixa ou snapshots publicados. Ajuste não simula transformação; execução exige F02 em autorização própria.

## Validação e preservação obrigatórias

- Testes Domain/Application/API e React relevantes; PostgreSQL real para constraints/concorrência/rollback. Comprove disputa com barreiras e sessões efetivamente bloqueadas, inclusive catálogo × física; não só Task.WhenAll.
- T01–T16 completos: último saldo, transferência 10→7/3, falha injetada após débito antes de crédito, idempotência igual/divergente/replay após alteração, revisão, papel, validade/virada de dia, quantização/resíduo/inteiros, guards do ledger e reconciliação read-only.
- Migration aditiva em base nova e cópia isolada da main anterior: IDs/contagens/refs/valores de todas as tabelas antigas comparados antes de fixtures, novas físicas vazias. Adaptar teste histórico FormulationMigrationTests ao alvo AddCatalogAndFormulation mantendo preservação, e criar teste Inventory próprio. Gerar SQL revisável de todas as migrations pertinentes/pendentes.
- `dotnet restore GenSW.sln`, build Release e testes da solução; frontend `npm ci`, `npm test`, `npm run lint`, `npm run build`; artefatos separados se DLLs em uso. Logs privados locais; testes não executados não são PASS.
- HTTPS/auth/refresh e navegador real, fluxos completos, erros, desktop1440×1000 e390×844, teclado/console/requisições. Preservar catálogo/conversões/perfis/receitas/snapshots, Animal/pedigree/imagens, Cruzamentos/Ciclos/Proles/ovos, Propriedades e Caixa.
- Antes de start/stop: reidentificar portas/PIDs/versão/banco/finalidade. 5175/7005/`gensw_auth_tests` e5173/7001 são referências históricas, não processos atuais garantidos. Não substituir banco do usuário por fixture, criar usuários fictícios ou redefinir senhas.
- Volume privado existente e `Images__PrivateRoot` explícito, sem padrão; não copiar/apagar fotos/provisionar volume vazio ou registrar caminho privado. Revalidar imagem autenticada se reiniciar API autorizada.
- Não aplicar em shared DB: precisa autorização específica, schema atual, backup/restore revisados e SQL de todas as pendências. Recovery com schema/livro conservado/forward fix ou restore aprovado; não Down destrutivo. Não migrar no startup.

## Entrega e encerramento técnico

Documente uso/API/permissões/precisão/locks/idempotência/migrations/recuperação em `docs/operations/estoque.md`; evidências reais em `docs/validation`, com ambiente inequívoco, limitações e validações. Atualize README/arquitetura apenas para o entregue. `git diff --check`, escopo/segredos e checks pertinentes; commit/push e PR revisável, anexe ao chat e acompanhe CI Backend/Frontend.

Registre no Redmine base/branch/commit/push/PR/checks/resultados/limitações/pendências; mover Tarefa para Em validação e conferir por leitura. Entregue F01 funcional completa para revisão, sem merge/produção nem Concluído com aceite/manual/revisão pendente. Não avançar F02 por consequência automática. Se requisito material for incompatível com o contrato, faça recomendação concreta e solicite decisão agrupada, continuando trabalho independente; não invente autorização.
