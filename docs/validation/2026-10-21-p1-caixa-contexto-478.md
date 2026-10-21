# P1 — contexto mensal do Caixa (#478)

Execução da [Tarefa #478](https://devops-lab.tailaf9418.ts.net/issues/478), filha da [Evolução #467](https://devops-lab.tailaf9418.ts.net/issues/467), a partir da instrução preparada na #476. Tarefa própria confirmada **Em andamento** antes da investigação. Baseline após fetch: `322a008414a112c7d2f1ce066e73875357342bd7`. Branch: `codex/478-p1-caixa-contexto`, em worktree isolada; checkout principal, diretórios locais e serviços anteriores preservados.

## Piloto e origem da evidência

A #477 já está concluída: ferramentas operacionais e backup da #475 integrados pela [PR #26](https://github.com/MarcosRCEng/GenSW/pull/26). Não há integração operacional a duplicar. A referência e os limites estão no [recibo da #477](2026-10-21-homologation-backup-477.md).

O login humano, a renderização de imagens nessa sessão e o aceite dos fluxos/valores da cópia `PILOT-472-CLEAN` permanecem pendentes nas #472/#473. Não foi observada uma tarefa humana real nesta execução. Os vazios de Catálogo/Estoque/Propriedades e de parte da Reprodução são ausência de dados para aceite, sem caracterizar defeito. A P0 com fixtures fornece evidência técnica histórica, sem substituir esse aceite.

A investigação atual usa contratos, código e testes dirigidos. O caso do Caixa foi reproduzido na baseline com três regressões: as três falharam porque os indicadores anteriores continuavam visíveis e as ações continuavam disponíveis enquanto o novo mês estava pendente ou com erro. O histórico #394/#395 tratava de schema ausente; este problema é de contexto da interface, sem nova falha de schema.

## Backlog priorizado

Frequências abaixo são potenciais, inferidas do fluxo; não foram medidas em uso humano. Apenas o primeiro recorte é implementado nesta entrega.

| Prioridade / módulo | Evidência e impacto | Dependência e risco | Critério verificável |
| --- | --- | --- | --- |
| 1 — Caixa: resumo anterior sob outro mês | `FinancialPage.tsx` limpava a lista, mas conservava `summary` na troca de consulta. Regressões dirigidas reproduziram indicadores e ações antigos durante atraso/falha. Pode confundir uma conferência mensal e sua prévia; o servidor recalcula e protege sequência/versão, mas a versão isolada não identifica o mês da prévia. Potencial a cada troca/reconsulta. | Correção frontend sem alterar cálculo/persistência; risco principal é o diálogo de fechamento aberto durante recarga. Teste com campos preservados e nova conferência. | Sem indicadores/ações antigos durante carga/erro; nova tentativa recupera o mesmo contexto; diálogo seguro; respostas antigas descartadas. |
| 2 — Reprodução: listas sem acesso após 25 registros | Listas de Cruzamentos, Ciclos e Proles não enviam página e não oferecem navegação; API retorna página padrão 1/25 com metadados ignorados. Inspeção de código/contrato; cenário sintético de 26 registros ainda não executado nesta entrega. Potencial em consultas diárias acima do limite. | Implementação futura proporcional, preservando filtros/retorno e hierarquia. Sem dependência de dados fictícios no piloto; risco baixo de UI, com teste de corrida necessário. | Acessar o 26º registro; voltar do detalhe à página/filtros; distinguir carga/erro/vazio. |
| 3 — Cruzamentos: seletores limitados a 100 animais | Formulário e filtros da lista usam apenas `listAnimals({pageSize:100}).items`. Inspeção de código; cenário de animal elegível na segunda página ainda não executado nesta entrega. Pode impedir seleção conforme o volume. | Reutilizar os padrões existentes de paginação/busca, mantendo referências históricas e elegibilidade. Validar volume real; risco baixo a moderado de seleção/contexto. | Animal elegível além da primeira página selecionável; referência atual identificada; falha de busca não se apresenta como ausência. |
| 4 — Catálogo/formulação: rótulo de referência histórica | `ServerSelect` conserva o rótulo apenas após seleção local; valor pré-carregado fora da página recebe `Referência selecionada: UUID`. O teste existente de `FormulationPages` aceita esse fallback. Evidência por código/teste existente, sem reprodução humana. Pode dificultar a conferência de uma referência histórica/inativa. | Resolver o detalhe por ID pelo padrão do F01, preservando o ID e a página. Risco baixo a moderado de estados/erros; confirmar contrato do endpoint histórico. | Edição identifica a referência existente mesmo fora da busca ativa; falha de detalhe é explícita e não substitui nem apaga o vínculo. |

Aviso de bundle acima de 500 kB e a ocorrência HTTP/2 histórica da P0 continuam em monitoramento, sem recorrência/impacto atual demonstrado. F02/F03/F04/F05, compras/vendas, fiscal e publicação permanecem fora do recorte.

## Contrato da vertical

Antes: mudar o mês ou recarregar podia manter os totais, o estado aberto/fechado e a versão do resumo anterior; uma falha deixava a lista em carregamento indefinido.

Depois: indicadores, linhas e ações dependem da consulta atual. Enquanto ela carrega, ou se falhar, nenhum resumo anterior é apresentado como atual. A falha oferece nova tentativa e não equivale a mês vazio. Mês, filtros e retorno permanecem conservados. Respostas de uma consulta substituída não podem sobrescrever a atual.

O fechamento exige resumo atual disponível, papel Admin e as regras existentes. Recarregar com o diálogo aberto conserva os campos, retira a prévia antiga e bloqueia a confirmação até recuperar os dados e obter nova conferência. Nenhum retry de leitura confirma uma operação financeira automaticamente.

Não há mudança em cálculos, sequência de fechamento, idempotência, referências, histórico, permissões da API ou modelo de dados. Não há migration nova. Recuperação da alteração de interface consiste em retornar ao frontend anterior; não executar `Down` ou corrigir dados para reverter esta entrega.

## Validação e entrega

Ambiente **P1-478-SYNTHETIC-ONLY**: frontend `https://localhost:5177`, API `https://localhost:7005`, PostgreSQL 18.1 novo e exclusivo, somente loopback. Não foram copiadas bases reais. Contas Admin/comum, quatro lançamentos e valores são exclusivamente sintéticos. Configuração, credenciais, sessões, logs, cluster e volume privados permanecem fora do Git. Os recibos sanitizados estão em [checks.json](evidence/478/checks.json).

Antes de iniciar: SQL completo das 18 migrations existentes gerado/revisado e aplicado somente na base nova vazia; 49 tabelas, 103 índices, zero instruções destrutivas e histórico exato das 18 migrations. Destino, usuário, diretório efetivo, listeners e HTTPS foram conferidos separadamente. Nenhuma migration nova ou aplicação em fonte/piloto. Como só o frontend foi alterado, teste de atualização de cópia anterior e comparação de modelo novo não se aplicam; as suites existentes de PostgreSQL exercitam persistência, concorrência e migrations.

| Gate | Resultado final |
| --- | --- |
| Reprodução na baseline | 3 testes, 3 FAIL antes da correção; resumo e ações anteriores conservados na troca de consulta |
| `dotnet test GenSW.sln` | **608/608 PASS**, zero falhas/ignorados; Domain 165, Application 147, Infrastructure 114, API 182; PostgreSQL real |
| `npm test` | **569/569 PASS**, 52 arquivos, incluindo 11 regressões novas; repetido após o ajuste do retry dentro do diálogo |
| `npm run lint`; `npm run build`; `git diff --check` | **PASS**. Uma dependência de tipo no efeito e opções de tipagem dos testes foram corrigidas após os primeiros gates locais; resultados acima são os finais |
| UI mensal 1440 e 390 px | Atraso/falha controlados de GET autenticado: indicadores antigos ausentes, ações bloqueadas, erro distinto de vazio/carga, recuperação no mesmo mês, resposta antiga descartada e retorno do detalhe com mês/filtros preservados. Sem overflow da página |
| Fechamento UI/API 390 px | Recarga controlada com diálogo aberto, campos preservados, retry interno acessível e foco de teclado contido; correção concorrente real gerou **409**, sem retry de escrita; recarga e nova conferência permitiram **200** com a versão atual |
| Histórico e saldos sintéticos | Uma correção de descrição gerou versão/auditoria; um único snapshot de agosto foi criado. Saldo de agosto e resumo de setembro permaneceram iguais aos valores sintéticos esperados |
| Papéis/erros API reais | GET anônimo **401**, fechamento com comum **403**, lançamento inexistente **404**. Comum não vê Fechar mês; GET 401/403 e POST 403/409 também têm regressões de componente |
| Módulos adjacentes | Animal, Propriedades, Cruzamentos, Itens, Receitas e Estoque: sessão autenticada, título/destino corretos e sem alertas em listas vazias sintéticas. Não se atribui aceite a detalhes sem dados |
| Revisão independente | Sem achados bloqueantes; revisão de leitura, sem atribuir ao revisor a execução central dos gates |

Referências inativas/inexistentes em mutações, replay e idempotência continuam cobertos pelas suites existentes: não foram alterados contratos de edição/criação nem controles de seleção nesta vertical. Não houve mudança de cálculo, saldo ou persistência no código. A validação manual de imagens não se aplica ao recorte financeiro; Animal/imagens não foram alterados e esta base possui zero imagens/referências. Não se afirma `200 image/png` ou renderização nesta execução, nem se transfere o gate técnico histórico para o piloto atual.

As capturas contêm apenas o ambiente de teste: [consulta pendente em 390 px](assets/478/pending-390.png) e [erro recuperável dentro do fechamento](assets/478/closing-error-390.png). Chrome usou TLS padrão, sem bypass; uma tentativa de iniciar Edge falhou antes de acessar a aplicação e a validação prosseguiu em Chrome. Ajustes de locators do harness e da rota de Receitas não foram classificados como falhas do produto.

Limites: build conserva o aviso de bundle de 637,39 kB. `npm ci` reportou 24 alertas de dependências do lockfile existente; dependências não foram alteradas neste recorte. Emulação de largura não cobre aparelho físico, Safari ou leitor de tela. O ambiente com fixtures serve exclusivamente à revisão técnica e não é base operacional.

Commit, push, PR e CI do commit entregue são registrados na #478 e na PR para evitar uma referência circular ao próprio commit deste relatório. A entrega permanece **Em validação** até revisão e aceite humano; não autoriza merge nem concede aceite ao piloto. Próximo incremento recomendado: paginação da consulta de Ciclos reprodutivos, com retorno/filtros e estados de erro/vazio, após reproduzir o cenário de 26 registros exclusivamente em teste.
