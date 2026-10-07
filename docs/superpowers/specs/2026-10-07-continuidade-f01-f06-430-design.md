# Continuidade F01–F06 — estoque e produção física

Data: 07/10/2026, America/Sao_Paulo. Evolução [#430](https://devops-lab.tailaf9418.ts.net/issues/430), relacionada à [#413](https://devops-lab.tailaf9418.ts.net/issues/413); planejamento [#431](https://devops-lab.tailaf9418.ts.net/issues/431). **Contrato proposto para revisão. A autorização atual abrange investigação e documentação; implementação e migrations ainda dependem de aceite explícito.** Sem merge ou produção.

Base verificada por fetch: `origin/main` = `dda69509c01b4a27bc6b6b5c3f2add842c441a53`, merge do PR #20. Branch documental `codex/431-planejamento-f01-f06`, em worktree isolado. O checkout original e seus arquivos locais foram preservados. #413 permanece concluída no recorte MVP-1; #428 e #429 foram reconsultadas e permaneciam Em validação. Não foram criadas tarefas de implementação.

Este documento substitui **somente as propostas de continuidade física** do [desenho anterior](2026-10-05-producao-insumos-produtos-413-design.md), sem alterar o contrato integrado do MVP-1. Leia em conjunto o [plano executável](../plans/2026-10-07-continuidade-f01-f06-430-implementation.md), o [prompt condicionado de F01](../../../output/planning/prompt-implementacao-f01-430.md), o [contrato operacional vigente](../../operations/catalogo-formulacao.md) e o [aceite do MVP-1](../../validation/2026-10-06-catalogo-formulacao-413.md). Os caminhos de código abaixo são relativos à raiz, conferidos na base indicada; nomes de recursos futuros são propostas.

## 1. Diagnóstico da main integrada

| Evidência atual | Recurso reutilizável / lacuna |
| --- | --- |
| `src/Backend/GenSW.Domain/Catalog/Item.cs`, `CatalogRules.cs` | Identidade única, capacidades cumulativas, ativo, `Revisao`, `UnidadeFixada`; kg/L/un; strings invariantes até 14 inteiros e 6 decimais. Item **não possui política de validade**. `Canonical` divide g/mL por 1000 e pode gerar mais de seis casas. |
| `src/Backend/GenSW.Domain/Catalog/Item.cs`, `ConversaoItem` | Conversão documental imutável com fator/fonte/método/data/amostra/proveniência; sem vínculo físico. `ToKg` resolve massa, não é conversor genérico para estoque em L/un. Não identificar um lote pelo texto do laudo. |
| `src/Backend/GenSW.Domain/Formulation/`; `src/Backend/GenSW.Application/Formulation/FormulationService.cs` | Perfis e receitas publicados imutáveis, rascunhos/revisões, escala, expansão limitada, nutrição e simulações/comparações congeladas. Nenhum saldo, reserva ou execução. Reutilizar seleção explícita de versões e semântica, sem refazer P01–P05. |
| `src/Backend/GenSW.Infrastructure/Formulation/FormulationRepository.cs`, `BeginAsync` | Toda escrita atual do catálogo/formulação usa advisory lock `(413,421)`, com timeout 5 s. Um lock físico separado, sozinho, **não** sincronizaria inativação de item/perfil/receita. |
| `src/Backend/GenSW.Infrastructure/Persistence/GenSWDbContext.cs`; migration `20261006220939_AddCatalogAndFormulation` | Nove tabelas próprias de catálogo/formulação, JSONB tipado, referências restritivas, checks e triggers. Não há tabelas de estoque/ordem. Não pré-criar entidades de fases futuras. |
| `src/Backend/GenSW.Application/Financial/FinancialService.cs`; `src/Backend/GenSW.Infrastructure/Financial/FinancialRepository.cs`; `src/Backend/GenSW.API/Controllers/FinanceiroController.cs` | Padrões de Admin pontual, revisão, prévia, auditoria, idempotência persistida e transação. Reutilizar conceitos, com tabelas/lock físicos próprios e sem chamar Caixa. |
| `src/Backend/GenSW.Application/Properties/PropriedadeService.cs`; `src/Backend/GenSW.Infrastructure/Properties/PropriedadeRepository.cs` | Propriedade operacional, busca/paginação e histórico. Não representa autorização por estabelecimento nem depósito automático. Associação opcional de Local é contexto. |
| `src/Backend/GenSW.Domain/Animals/ProducaoOvo.cs`; `src/Backend/GenSW.Application/Animals/ProducaoOvos/ProducaoOvoService.cs` | Ovo tem AnimalId, data, peso em gramas, observação e timestamps; não tem quantidade ou revisão numérica. Registro editável, não disponibilidade. Semântica unidade/amostra continua pendente de F03. |
| `src/Frontend/GenSW.Web/src/routes/AppRoutes.tsx`; `src/Frontend/GenSW.Web/src/features/auth/navigation/modules.ts` | Itens, receitas e formulação já têm rotas protegidas. Estoque continua `planned`; ordens não têm rota. Promover apenas quando a vertical real existir. |
| `src/Frontend/GenSW.Web/src/shared/details/`; `src/Frontend/GenSW.Web/src/features/formulation/`; `src/Frontend/GenSW.Web/src/features/properties/` | Visualizar separado de Editar, retorno com filtros/página, seletores paginados e referência histórica retida. F01 precisa parsers de resposta próprios; a referência fora da página pode hoje aparecer como UUID. |
| `src/Frontend/GenSW.Web/src/shared/http/httpClient.ts`; `src/Frontend/GenSW.Web/src/features/financial/EntryEditor.tsx` | Cliente de sessão/refresh e interação prévia/confirmação/retry. Decimal e saldo ficam no servidor; não copiar a precisão monetária de duas casas. |
| `tests/GenSW.API.Tests/FormulationApiTests.cs`; `tests/GenSW.API.Tests/FormulationMigrationTests.cs`; `tests/GenSW.API.Tests/PostgreSqlPropriedadesTests.cs`; `tests/GenSW.Infrastructure.Tests/FinancialTests.cs` | Padrões de PostgreSQL descartável, barreiras, rollback e preservação. A assertiva histórica de ausência de estoque em `FormulationMigrationTests` deve continuar referida à migration do MVP-1, e não à latest futura. |
| `.github/workflows/ci.yml`; `README.md`; `docs/architecture/overview.md` | Backend restore/build Release/test em PostgreSQL efêmero e frontend npm ci/test/lint/build. Os 541/419 testes aprovados do MVP-1 são evidência histórica, não validação de F01. |

Leitura integral das referências obrigatórias concluída, incluindo [operação de imagens](../../operations/animal-evolution.md) e [visualização dos cadastros](../../validation/2026-10-04-visualizar-cadastros-397.md). Não se iniciou/reiniciou API, frontend ou banco. O schema compartilhado e os ambientes 5173/7001 e 5175/7005 **não foram revalidados nesta etapa documental**; seus estados anteriores não são afirmados como atuais. Reidentificação é gate anterior a qualquer uso futuro.

## 2. Recortes e decisões de revisão

| Decisão | Recomendação concreta | Gate |
| --- | --- | --- |
| D06-F01 — Papéis | Autenticado consulta, cadastra Local/Lote e registra recebimento operacional, transferência ordinária, saída justificada e consumo interno. Admin abre inventário, ajusta quantidades, altera situação/validade/referências do lote, inativa/reativa Local e executa tratamentos excepcionais. | Aceite de F01. Não cria papel novo nem ACL por Propriedade. |
| V01 — Validade não informada | Não configurar regra sanitária por classe por inferência. Sem data = não informada, aviso permanente; não equivale a vencido. Recebimento vencido ou declarado pendente entra Bloqueado; nenhum recebimento o libera automaticamente. | Aceite de F01. Se o negócio exigir validade obrigatória por item, revisar esse ponto antes de implementar, com política explícita própria. |
| Q01 — Resolução física | Saldo e movimentos `numeric(20,6)` na unidade canônica; quantização ToEven somente com prévia e aceite do resíduo. `un` sempre inteiro, nunca arredondado automaticamente. | Aceite de F01. Não altera escala/nutrição do MVP-1. |
| L01 — Coordenação | Escritas físicas obtêm lock atual de catálogo, depois lock próprio físico `(430,1)`, depois linhas em ordem estável. Catálogo mantém seu lock atual. Sem locks de Caixa/Genealogia. | Técnica, testada em F01; não depende de escolha rotineira do usuário. |
| D05-F02 — Execução | Reserva ao iniciar, consumo/saídas apenas na confirmação única. Sem WIP. Execução curta com revalidação; se fato físico já ocorreu sob divergência/bloqueio, registro excepcional de ocorrência por Admin, sem autorização ordinária de uso inválido. | Aceite específico de F02, posterior a F01. |
| R02 — Reversão | Corrigir lançamento que não representa transformação real, com evidência física; não desfazer uma moagem ou perda real por crédito fictício de grão. Ausência de uso posterior é necessária, mas insuficiente. | Aceite específico de F02. |
| O03 — Ovos | Entrada explícita posterior; confirmar unidade versus amostra e conciliar acervo manual antes de escolher IDs. | Aceite específico de F03, nenhuma importação agora. |
| C04 / S05 / H06 | Custos somente por prioridade expressa; solver só após contrato/dados; F06 escolhido por vertical real. | Desenhos/aceites próprios. |

Para iniciar F01 basta aceitar o contrato F01, incluindo D06-F01/V01/Q01, ou registrar suas alterações e autorizar a implementação. A revisão de F02–F06 pode continuar sem antecipar sua execução. Silêncio, CI ou aceite do MVP-1 não aprovam este contrato.

## 3. F01 — domínio e fronteiras

Criar features verticais `Inventory` em Domain/Application/Infrastructure/API e `inventory` no React. Application coordena casos de uso; Domain valida regras sem EF/HTTP; Infrastructure concentra persistência/transações. Exceções físicas específicas com códigos estáveis, sem reutilizar `AnimalEvolutionException` ou construir framework universal.

| Entidade F01 | Campos/regras propostos |
| --- | --- |
| `LocalEstoque` | Guid, código único normalizado (1–50), nome (1–200), descrição opcional (2000), PropriedadeId opcional, finalidade `Ordinario`/`Segregacao`, Ativo, Revisao, timestamps. Código único global; nome pode repetir. Criação ativa, finalidade declarada e revisão 1; status inicial inativo somente Admin. Histórico de nome/contexto/status. Troca de finalidade Admin exige saldo zero em todas as posições (e reservas zero após F02). |
| `LoteMaterial` | Guid, ItemId imutável, código interno único normalizado (1–50), código externo opcional, origem textual (1–1000), fonte (1–1000), responsável UsuárioId existente, data observada de origem opcional, fabricação/coleta opcionais, validade opcional e fonte/responsável quando informada, situação `Liberado`/`Bloqueado`/`Encerrado`, Ativo, Revisao, timestamps. Unidade canônica congelada do Item. |
| Referências do lote | PerfilNutricionalId opcional e ConversaoItemId opcional, ambos do mesmo Item, escolha explícita com justificativa de aplicabilidade (até 2000). Perfil deve estar Publicado no novo vínculo. Conversão é a versão documental imutável existente, não fator digitado no comando. Dados/fonte/contexto são copiados para o evento. Troca posterior exige Admin e histórico, sem reescrever fatos. |
| `PosicaoEstoque` | PK (LoteId, LocalId), Unidade, Quantidade canônica, Revisao. Começa em zero, não é campo editável. Linha zero persiste após esgotamento. **F01 não tem coluna/tabela de reserva**; F02 acrescenta sua projeção e constraints. |
| `EventoEstoque` | Guid, Sequencia bigint única crescente, Tipo, AutorId, ResponsavelId, instante UTC, DataOperacional, data observada opcional, motivo (1–2000), documento/origem textual opcional, EventoReferenciaId opcional, versão de algoritmo físico e snapshot tipado. |
| `MovimentoEstoque` | Guid, EventoId, ordinal único no evento, LoteId/LocalId, ItemId/Unidade congelados, sentido Entrada/Saida, quantidade positiva, quantidade/unidade declaradas, conversão/perfil exatos e snapshots, resíduo quando houver, saldo anterior/posterior. Não há custo. |
| `HistoricoEstoque` | Append-only: registro/tipo/operação, sequência, AutorId/UTC/motivo, antes/depois e correlação. Criação/edição/status e fixação física de unidade auditadas na transação. |
| `ComandoEstoque` | AutorId, Operacao, RecursoId (Guid vazio para criação/comando sem raiz), Chave, HashPayload, StatusHTTP, Location opcional, RespostaJson, EventoId/RegistroId, UTC. Índice único de escopo; resposta de sucesso durável. Sem JWT/cookies ou secrets. |

FKs Restrict para Item, usuário, Local, Lote e referências documentais; sem cascatas de fatos. Lote não é Prole, Animal, laudo textual ou versão de receita. Criar lote vazio é permitido e fixa a unidade do item (`FixUnit`) na mesma transação; no primeiro uso físico, a unidade deixa de ser editável mesmo sem perfil/receita. Situação inicial: Liberado quando ativo/não vencido e sem pendência declarada; Bloqueado quando vencido ou com pendência. Usuário comum pode declarar pendência, mas não liberar depois por edição. Admin pode cadastrar material de inventário em Item inativo/sem PodeEntrar, sempre Bloqueado, destinado a conferência/segregação. Atualização só de metadados não muda ItemId/unidade/código interno do lote. Corrigir identidade errada exige lote/item novo e ajustes presentes justificados, sem apagar o original.

Perfil ausente não impede estoque quantitativo; mostra ausência e não afirma composição. Perfil/conversão de lote não é automaticamente escolhido em simulações antigas ou futuras; a integração de seleção física da ordem fica em F02. Novo uso da conversão exige aplicar o contexto deste lote. Uma mudança na massa por unidade não reconverte posições existentes.

### 3.1 Permissões D06-F01

Todas as rotas exigem sessão; autorização Admin também no serviço, além do controller. Depois de esperar locks, reconsultar usuário ativo/papéis atuais via `IAuthenticationSessionService.GetCurrentUserAsync`, antes de replay ou comando: JWT com claim Admin anterior não prova papel atual. Retornar 401 se usuário não mais ativo/existente e 403 se papel necessário removido. Sem reformular login/refresh. Autor vem da sessão; responsável pode ser outro usuário ativo existente explicitamente informado no novo fato; responsável histórico inativo continua consultável. Não se cria usuário fictício nem se redefine senha. Cada comando excepcional exige motivo/evidência, com autor e responsável distintos visíveis.

| Operação | Autenticado | Admin | Condição |
| --- | --- | --- | --- |
| Consultar Local/Lote/saldo/livro/histórico | Sim | Sim | Inclui inativos, bloqueados, encerrados e zero por filtro/ID. |
| Criar/editar metadados de Local | Sim | Sim | Propriedade ativa no novo vínculo; vínculo histórico inativo permanece. Não altera finalidade nem status depois de criado sem Admin. |
| Criar Lote / corrigir descrição/origem | Sim | Sim | Item ativo; responsável/fonte; sem alterar identidade física ou situação pela edição. Criação em Item inativo/sem `PodeEntrar` somente Admin, para inventário declarado e Bloqueado. |
| Recebimento manual ordinário | Sim | Sim | Item ativo e `PodeEntrar`, Local ativo ordinário; lote compatível. Pode registrar vencido/Bloqueado em Local ativo de Segregação, com motivo, mantendo bloqueio. |
| Abertura de inventário | Não | Sim | Por lote/local ainda sem eventos: uma abertura por posição. Quantidade real e declaração de conferência; origem Inventario. Depois, corrigir por Ajuste. |
| Transferência/saída ordinárias | Sim | Sim | Elegibilidade e saldo; saída deve declarar finalidade, motivo e destino textual; não representa venda. |
| Consumo interno | Sim | Sim | Elegibilidade e Item.UsoInterno. Sem receita/execução de transformação disfarçada. |
| Ajuste de inventário, bloqueio/liberação, validade, referências, ativo e encerramento do Lote | Não | Sim | Versão/motivo/evidência; liberação não remove vencimento. Encerrar exige saldo zero em todos os locais. |
| Ativo/finalidade de Local | Não | Sim | Inativar com saldo é permitido; saldo permanece. Reativação não libera lotes. Trocar finalidade só com saldo/reservas zero. |
| Transferência para segregação / descarte / correção de inativos | Não | Sim | Caminhos excepcionais explícitos abaixo; nunca habilita consumo ordinário. |
| Reconciliação | Sim | Sim | Somente leitura; nenhum botão de reparar. |

Admin não ganha bypass para saldo negativo, fracionar contagem ou consumir ordinariamente vencido. Propriedade inativa impede **novo vínculo** de Local, não muda por inferência a elegibilidade de todos os materiais de locais existentes; status do Local é a autoridade operacional.

### 3.2 Situação, validade e tratamentos

Elegível para retirada/consumo ordinário = Item ativo + capacidade pertinente + Local ativo `Ordinario` + Lote ativo `Liberado` + não encerrado + validade não vencida. Validade é inclusiva: vencido quando Validade < DataOperacional atual. UTC de gravação e DataOperacional em America/Sao_Paulo calculados pelo TimeProvider; data observada pode ser passada e não modifica saldos retroativos. Capturar/revalidar o relógio ao fim das validações, imediatamente antes dos lançamentos. Testar passagem de meia-noite; timestamp não é garantia de que uma execução física ocorreu naquele segundo.

Sem validade aparece “não informada”; nenhum cálculo de validade pela fórmula, classe, espécie ou peso. Não há selo de segurança. Se fabricação/coleta/validade forem informadas no mesmo escopo, validade não precede a data de origem declarada; data observada de entrada/fabricação/coleta não é futura. Correção de validade exige fonte e responsável Admin; se a correção tornou vencido, elegibilidade cai imediatamente, sem apagar o saldo.

| Ação | Regra |
| --- | --- |
| Receber em lote Liberado | Incrementa posição, preserva identidade/origem/perfil/conversão; fonte/origem distinta ou características incompatíveis exigem novo lote. Nova entrada não reabre Encerrado. |
| Receber vencido ou pendente | Registrar fato somente em Segregação; lote Bloqueado (se vencido em lote antes Liberado, bloquear na transação). Liberação posterior só Admin; vencimento permanece impeditivo. |
| Bloquear | Liberado → Bloqueado, por Admin; história e quantidade intactas. Bloqueado não volta a Liberado por recebimento, reativação ou transferência. |
| Liberar | Bloqueado → Liberado por Admin com evidência; validade vencida retorna conflito, não existe override ordinário. Null pode ser liberado com aviso conforme V01. |
| Encerrar | Liberado/Bloqueado → Encerrado se todas as posições zero; terminal. Nova materialidade exige outro lote/código. |
| Inativar | Flag separada, com saldo preservado. Reativar exige Admin, sem alterar situação/validade. |
| Segregar | Admin transfere atomicamente a quantidade a Local ativo de Segregação e bloqueia o lote inteiro; permite retirar de Local/Item/Lote inativo ou vencido. O bloqueio é global ao lote, inclusive outras posições. |
| Retirar da segregação | Comando Admin próprio `retorno-segregacao`: origem ativa Segregacao, destino ativo Ordinario, lote Liberado/ativo/não vencido e Item ativo. Dispensa apenas finalidade Ordinario da origem; mantém saldo/versões e demais regras. Histórico permanece; não é bypass genérico da transferência ordinária. |
| Descartar | Admin registra saída justificada de qualquer situação (exceto Encerrado sem saldo), inclusive vencido/inativo, limitada ao saldo; destino Descarte, sem venda/consumo. |
| Ajustar | Admin declara quantidade contada **alvo**, motivo, evidência e data da contagem. Servidor calcula delta contra posição atual; delta zero recusado como movimento. Contagem do alvo nunca é delta informado pelo cliente. Pode reduzir/incrementar inativo/Bloqueado em correção; incremento excepcional conserva/requer bloqueio e não libera uso. |

Abertura de material já vencido/inativo exige Admin e segregação/bloqueio. Ajuste de posição em Local inativo é permitido para registrar a realidade presente, mantendo sua indisponibilidade. Não se usa ajuste para produzir intermediário: transformação exige F02. F01 não possui estorno genérico de evento; erro é ajuste presente referenciando o fato, após conferência real. Nenhuma linha é apagada.

## 4. Quantidades, livro e conversões F01

Invariantes no servidor e no banco:

1. `Saldo(lote,local) = Σ entradas − Σ saídas`; saldo ≥ 0, ≤ 99999999999999.999999 na unidade canônica. Transferência grava exatamente o mesmo valor no débito e crédito; total do lote permanece idêntico.
2. Unidade/Item de posição/linha conferem com lote; `un` inteiro em declarado, normalizado e saldo. Quantidades de movimentos > 0; alvo de ajuste pode ser zero. Repetição de posição/linha no mesmo comando recusada ou agregada de modo canônico explicitamente; em F01 um comando move um lote/posição (transferência tem duas posições), sem lote genérico em massa.
3. Projeção, movimentos, metadados afetados, revisão, auditoria e resposta idempotente confirmam juntos. Não atualizar saldo por GET, recálculo no cliente, job ou reparo automático.
4. Revisor espera versões dos registros e posições afetados. Posição inexistente = versão 0; sob lock, criar uma única linha. Versão cresce uma vez por posição mutada/comando. Item/local/lote possuem versões próprias, não um ETag financeiro emprestado.
5. Eventos/linhas/auditoria são append-only, protegidos contra UPDATE/DELETE por guards da migration; snapshot do comando/resultado também imutável. Constraints do evento validam cardinalidade e igualdade de transferência no commit (trigger deferred); não alegar que um CHECK de uma linha garante atomicidade entre duas.

### 4.1 Normalização e prévia

Entrada HTTP usa string invariante sem sinal, expoente ou vírgula, até 14 inteiros/6 casas. `kg↔g` e `L↔mL` exatos. Grandezas diferentes exigem ConversaoItemId selecionado/vinculado ao lote, par direto aplicável e justificativa de contexto. Sem caminhos transitivos, fator padrão, densidade de espécie ou fator digitado no movimento. Inverso usa a **mesma versão**, por divisão direta pelo fator original, sem pré-calcular um recíproco arredondado; converter as extremidades g/mL explicitamente. Exemplo sintético un→kg com fator3: 3kg /3 =1un exata, não 3×(1/3) arredondado. Implementar serviço físico próprio para origem → unidade canônica kg/L/un; `ToKg` existente não cobre sozinho esse contrato.

Prévia no servidor calcula valor canônico antes de quantizar, valor armazenável, resíduo `calculado − armazenável`, proveniência e avisos. Resultado calculado exposto como string com precisão decimal disponível; persistir original/fator/sentido/algoritmo e normalizado. Não alegar exatidão matemática de decimal para recíprocos periódicos.

- Se resultado tem até 6 casas, usar exatamente; se ultrapassa, mostrar ToEven a 6 casas e exigir `aceiteQuantizacao` com valor calculado/normalizado/resíduo e motivo. O servidor recalcula e compara o aceite; preview não reserva nem autentica para sempre. Alteração de payload/referência/versão invalida a prévia.
- Resultado quantizado zero, overflow ou fora de limite são recusados. Sem adicionar resíduo como estoque paralelo ou esconder numa “perda”. Transferência, segregação e retorno entre locais exigem quantidade apontada na unidade canônica, até seis casas e integral para un, sem conversão/quantização nova; conversão/perfil do lote aparecem apenas como referência histórica. As duas pernas recebem exatamente o mesmo valor.
- Para canônica `un`, resultado fracionário é recusado, mesmo com aceite ToEven. Operador corrige quantidade declarada/conversão ou aponta contagem inteira real; massa original pode ser evidência auxiliar. Nunca `ceil`/`floor` de ovos/frascos.
- Exemplo sintético: `0.0005 g` → `0.0000005 kg` → ToEven `0`, recusado por movimento nulo. `0.0015 g` → `0.0000015 kg` → `0.000002 kg`, resíduo `-0.0000005 kg`, só com aceite explícito. Estes são casos de teste, não precisão de balança recomendada.

`numeric(20,6)` pode arredondar coercivamente; a aplicação deve validar/quantizar **antes** do EF. Check de escala na coluna após coerção não prova rejeição de sete casas. Guards verificam documento declarado e aceite/resíduo conforme o contrato; testes SQL e de serviço distinguem essas garantias. Conversões estimadas/declaradas permanecem indicadas, sem promover medição fictícia.

### 4.2 Concorrência e idempotência

Protocolar todas as mutações físicas, inclusive cadastro/status/validade e F02 futura, na mesma ordem:

1. Autenticar/autorizar, validar forma e chave (1–100), abrir transação ReadCommitted, `SET LOCAL lock_timeout='5s'`.
2. Obter advisory catálogo `(413,421)` **antes** de físico `(430,1)`. Catálogo existente já usa o primeiro; física conserva ambos até commit. Não fazer caminho físico → catálogo; nenhum serviço aninhado abre transação própria. Isso sincroniza inativação/capacidades/unidade/perfis/receitas sem alterar o lock de Caixa/Genealogia. Não é alegação de alta vazão.
3. Revalidar usuário/papel atuais conforme D06-F01; buscar comando persistido por autor/operação/recurso/chave. Hash canônico normaliza strings decimais equivalentes, ordem de propriedades e IDs; revisões, motivo, referências e aceite fazem parte. Lista com semântica de conjunto ordena por identificador; rejeitar duplicatas. Mesmo hash retorna **status, Location e corpo originais**, antes de revalidar estado/versão/validade. Payload diferente retorna 409 `idempotencia_divergente`.
4. Para comando novo, obter locks de linhas em ordem definida por tipo (`Item`, `Local`, `Lote`, futura `Ordem`, `Posicao`) e Guid/chave composta crescente, revalidando estado/versões e disponibilidade depois da espera. Mutação de catálogo espera o advisory comum. Para novo vínculo de Local, conferir Propriedade ativa sob `FOR SHARE` da referência; status existente usa row lock conflitante. Propriedade não é lock de elegibilidade de retirada/consumo, pois só contextualiza Local. Validar responsável existente sob lock compartilhado quando sua elegibilidade atual for exigida; sem lock genealógico/Animal.
5. Calcular/quantizar, validar, atribuir sequência sob lock (gaps por rollback são aceitáveis), registrar fato/projeção/auditoria/fixação de unidade e resposta. Salvar/commit único. Índices/constraints reforçam exclusividade; exceção desfaz tudo.

Timeout retorna 409 `conflito_transitorio`, sem sucesso/idempotência persistidos. Falha depois de débito antes de crédito deve reverter ambos e comando. Erros de validação não são armazenados como sucesso. Se rede falhar depois do commit, mesma chave/payload entrega resposta original mesmo depois de ajuste, inativação ou reversão futura. Autorização atual continua obrigatória para replay; não reexecutar comando quando papel necessário foi removido.

UI mantém chave e payload para retry de rede; depois de conflito e alteração consciente de payload, cria outra chave e nova prévia. Idempotência por autor não evita dois autores registrarem o mesmo recebimento real com chaves novas: código de lote/documento e histórico ajudam revisão, mas não existe deduplicação comercial universal em F01. Abertura tem unicidade por posição; lote tem código global único. Não anunciar exatamente-uma-vez para origens livres.

Serialização é proporcional ao primeiro módulo físico. O lock de catálogo também bloqueia simulações enquanto uma escrita física ocorre: registrar limite e medir antes de trocar por locks granulares. Todos os escritores precisam respeitar protocolo; não misturar backend antigo que permita alterar unidade sem conhecer uso físico. Guards de unidade fixada defendem o banco; rollout e compatibilidade de writers são gate futuro, não deploy autorizado.

### 4.3 Consulta e reconciliação

Listas padrão 25/máximo 100, filtros explícitos, busca escapando `%`/`_`/barra, ordenação whitelist e ID de desempate. Listagem com count/linhas e cálculo de saldo/elegibilidade usa transação de leitura RepeatableRead; retorna `observadoEmUtc` e `sequenciaAte`. Próxima página pode ser snapshot posterior, indicado na UI; não se promete snapshot global através de múltiplas requisições. Movimentos permitem cursor/limite por sequência, para consulta estável até corte explícito.

Saldo retorna físico contabilizado e elegível; F01 não expõe reservado fictício. Somar somente por Item/unidade compatíveis; total de kg/L/un juntos não existe. Mostrar quantidade em inativos/Bloqueados/vencidos separada da utilizável. `GET /reconciliacao` compara projeção com livro no mesmo corte e devolve divergências por posição, quantidade do livro/projeção/diferença e corte. Paginação sobre divergências com totais no mesmo snapshot; leitura pode ser custosa, timeout 3 s e filtros de lote/local disponíveis. Timeout indica consulta não concluída. Divergência exige diagnóstico e revisão; não ajusta automaticamente o saldo nem gera estoque por recontagem de Animal/Caixa.

## 5. API F01 proposta

Prefixo `/api/v1/estoque`; todas autenticadas. Novas **mutações de domínio** (POST/PUT/PATCH e transições) exigem `Idempotency-Key` e versões esperadas; POST `/previas` é consulta, sem chave/gravação. Criação usa `versaoEsperada: 0`; movimentos enviam `versoesEsperadas` com lote, item, locais e posições. Histórico de cadastro incrementa Revisao da entidade; um movimento incrementa posições, sem mudar artificialmente revisão de metadados do lote. Resposta informa todas as revisões resultantes.

| Recurso | Métodos / comportamento |
| --- | --- |
| `/locais` | GET com search/ativo/propriedadeId/finalidade; POST criação. `/{id}` GET/PUT metadados; `/{id}/ativo` PATCH Admin; `/{id}/finalidade` POST Admin; `/{id}/historico` GET paginado. |
| `/lotes` | GET com search/itemId/situacao/ativo/validadeAte/semValidade/localId/comSaldo; POST cadastro explícito. `/{id}` GET/PUT metadados permitidos; `/{id}/ativo` PATCH Admin; `/{id}/bloqueio`, `/liberacao`, `/encerramento`, `/validade`, `/referencias` POST Admin; `/{id}/historico` GET. |
| `/saldos` | GET com itemId/loteId/localId/propriedadeId/situacao/ativo/validadeAte/semValidade/elegivel/comSaldo; GET `/saldos/{loteId}/{localId}` incluindo zero/versão ou posição inexistente versão 0. |
| `/movimentos` | GET com tipo/itemId/loteId/localId/autorId/dataDe/dataAte/seqDe/seqAte; `/{eventoId}` GET de evento/linhas/snapshot. A lista agrupa eventos, transferência aparece uma vez com duas linhas. |
| `/previas` | POST **somente leitura de domínio**, tipado por operação; sem gravar, reservar ou consumir chave. Exige o mesmo papel da operação (prévia Admin nega usuário comum). Retorna normalização/resíduo/projeção/versões/avisos, não saldo garantido. |
| `/aberturas`, `/entradas`, `/transferencias`, `/saidas-manuais`, `/consumos-internos`, `/ajustes` | POST, cada operação específica; não endpoint para PATCH de saldo. Abertura/Ajuste Admin. Saída/Consumo sem venda/caixa. |
| `/segregacoes`, `/retornos-segregacao`, `/descartes` | POST Admin; nunca opção oculta de saída ordinária. |
| `/responsaveis` | GET paginado search/ativo e `/{id}` GET incluindo histórico inativo. DTO mínimo `id,nome,ativo`, ordem nome/Id; usuário comum pode consultar. Novo fato exige responsável ativo, sem expor username/papéis/Pessoa/credenciais. |
| `/reconciliacao` | GET paginado, somente leitura; sem POST de reparo. |

Envelope de lista: `items,page,pageSize,totalItems,totalPages,observadoEmUtc,sequenciaAte`. Ordenações por recurso: Locais/Lotes `nome` (quando aplicável), `codigo`, `createdAtUtc`; Saldos `itemCodigo`,`loteCodigo`,`localCodigo`; Movimentos `sequencia` asc/desc. Datas YYYY-MM-DD; UTC ISO8601; quantidades strings invariantes; versões/paginação números inteiros limitados. Sequencia bigint, cortes, seqDe/seqAte e cursores usam **strings invariantes**, inclusive acima de 2^53; cliente não converte para Number. GET por ID inclui inativos e detalhes suficientes sem varrer todas as páginas.

ProblemDetails com `code`/IDs públicos/versões atuais seguras: 400 formato/conversão incompatível/fração/aceite faltante; 401 sessão; 403 Admin; 404 referência; 409 versão obsoleta, saldo insuficiente, inativo/situação/validade, código duplicado, encerramento com saldo, chave divergente ou timeout. Nunca retornar SQL, paths privados ou connection strings. POST cria cadastro/evento 201 com Location; edição/comando de estado 200. Replay preserva o status original; pode informar `Idempotency-Replayed: true` sem mudar o corpo.

Exemplo **sintético**, UUIDs exclusivos de fixture:

```json
{
  "loteId": "00000000-0000-4000-8000-000000000001",
  "origemLocalId": "00000000-0000-4000-8000-000000000002",
  "destinoLocalId": "00000000-0000-4000-8000-000000000003",
  "quantidade": "2.5",
  "unidade": "kg",
  "conversaoItemId": null,
  "responsavelId": "00000000-0000-4000-8000-000000000004",
  "motivo": "Transferência física conferida",
  "versoesEsperadas": {
    "item": 4, "lote": 2, "origemLocal": 1, "destinoLocal": 1,
    "origemPosicao": 3, "destinoPosicao": 0
  },
  "aceiteQuantizacao": null
}
```

Prévia/resposta: operação, IDs/corte, quantidade declarada, canônica calculada/armazenável/resíduo, fator/sentido/fonte, avisos, saldo anterior/posterior de cada posição e revisões. Entrada exige origem/fonte e `dataObservada`; ajuste exige `quantidadeContada` na canônica e declaração de inventário. Cadastro de lote separado precede os movimentos; nenhum saldo nasce pelo cadastro. Incluir campo de evidência textual, sem upload obrigatório. Campos e mensagens do comando expostos na OpenAPI.

## 6. Telas F01 e navegação

Promover **Estoque** na área Produção e operações para destinos reais Locais, Lotes, Saldos e Movimentos. Não criar Ordens nesta fase; não mudar Produção agrícola/animal/genética. Reutilizar sessão/refresh e shared/details; criar feature `inventory` com tipos/parsers HTTP e testes próprios.

| Tela/rota | Esboço verificável |
| --- | --- |
| `/estoque/saldos` (entrada principal) | Filtros Item/Local/Lote/Propriedade/situação/validade/zero; tabela ou cards com físico, utilizável, unidade, motivo de indisponibilidade e observadoEm. Ações de movimento em fluxo próprio. Sem resumo somando dimensões. |
| `/estoque/locais`, `/novo`, `/:id`, `/:id/editar` | Código/nome/contexto/finalidade/status, seleção paginada de Propriedade; detalhe só leitura, histórico/saldos relacionados e comandos Admin específicos. |
| `/estoque/lotes`, `/novo`, `/:id`, `/:id/editar` | Item/código/origem/datas/situação/validade/fonte/responsável; perfil/conversão selecionados por versão, sem “mais recente”. Detalhe apresenta metadados e posições/histórico paginados; Admin atua em comandos separados. |
| `/estoque/movimentos`, `/:id` | Filtros e sequência, transferência agrupada; detalhe imutável com linhas, origem/destino, autor/responsável, motivo, valor original/canônico/resíduo e referências históricas. |
| `/estoque/operacoes/:tipo` | Tipos permitidos: abertura, entrada, transferencia, saida-manual, consumo-interno, ajuste, segregacao, retorno-segregacao, descarte. Selecionar lote/local; apontar real/motivo; solicitar prévia; revisar; confirmar. Tipos Admin bloqueados também em acesso direto. |
| `/estoque/reconciliacao` | Corte e diferenças livro/projeção por filtro/página; mensagem “sem divergências” ou pendência de diagnóstico. Nenhuma ação de reparar. |

Prévia orientada ao operador: quantidade declarada → quantidade que será registrada; saldo observado → saldo após ação; em transferência, origem e destino. Se há resíduo, mostrar unidade/magnitude e aceite específico com motivo, sem jargão de EF/lock/SQL. Ajuste exibe contagem, saldo atual e delta calculado. Descarte/segregação dizem destino e situação resultante. Botão Confirmar só após prévia compatível; backend revalida de qualquer modo.

Novos usos selecionam elegíveis; dados históricos retêm referência selecionada mesmo fora da página ou inativa, com rótulo resolvido por GET individual (evitar UUID como única descrição). Seletores com busca/paginação servidor, sem baixar catálogo inteiro. Não existe listagem pública de usuários na main examinada: criar somente `/estoque/responsaveis` e GET individual mínimos conforme contrato, sem cadastro paralelo ou endpoints administrativos amplos. Testar responsável fora da página/inativo e negar novo fato com inativo.

Preservar filtros/página no retorno via `history.state`/query pertinente; URLs de detalhes funcionam diretamente/recarregadas. Loading, vazio, erro de campo, 401/reautenticação, 403, 404, 409 com atualização explícita e falha/retry têm texto/foco. Não descartar preenchimento após erro; replay com chave original em timeout. Teclado, labels, foco visível, tabela focável/rolagem interna e cards responsivos em desktop 1440×1000 e 390×844; sem overflow da página. UI não calcula saldo/conversão em `Number`.

## 7. F02 — contrato físico revalidado, ainda proposto

Depende de F01 entregue e aceita, ou autorização encadeada explícita com gates. Sem custos ou ovos. `OrdemProducao` fixa ReceitaVersaoId e snapshot planejado ao planejar; prévia de escala não reserva. Entradas físicas resolvem ItemId de cada intermediário e selecionam lotes/locais/conversões/perfis reais, sem debitar ancestrais. Sub-receita que ainda precisa produzir intermediário é ordem separada confirmada primeiro; nunca consumir saída que será criada na própria confirmação.

### 7.1 Estados/comandos

| Origem | Comando → destino | Regra |
| --- | --- | --- |
| Rascunho | editar → Rascunho; planejar → Planejada | Versão esperada; recipe publicada/ativa; snapshot e referências fixas. |
| Planejada | revisar → Rascunho; iniciar → Em execução | Revisão sem reserva; início escolhe alocações/responsável/data e reserva atômica. |
| Rascunho/Planejada | cancelar → Cancelada | Motivo; nenhuma reserva/consumo. |
| Em execução | ajustar reservas → Em execução | Versão/chave; aumentar só pelo saldo disponível; reduzir/reatribuir apenas parcela declarada não consumida/transformada e ainda presente na posição; snapshot planejado não muda. |
| Em execução | confirmar → Confirmada | Reais, perdas/divergências, lotes de saída e reservas liquidados em uma transação. |
| Em execução | abortar sem consumo → Abortada sem consumo | Admin declara nenhuma transformação/consumo físico; libera reservas, não lança perda fictícia. |
| Em execução | confirmar ocorrência → Confirmada (ocorrência) | Proposta Admin, §7.3; registrar fato já ocorrido, sem liberar uso inválido. |
| Confirmada | anotar → Confirmada | Append-only, sem quantidades/perfis/custos mutáveis. |
| Confirmada | reverter lançamento → Revertida | Admin, lançamento indevido + evidência física + lotes de saída sem uso, §7.4. |

Cancelada, Abortada sem consumo e Revertida são terminais. Não existe PATCH arbitrário de estado nem Em execução → Planejada/Cancelada. Planejado versus real lado a lado; autor autenticado distinto do responsável, datas reais coerentes, fim ≥ início e não futuro. Nenhum estado sozinho cria Animal/Filiação/ovo/caixa.

API futura `/api/v1/producao/ordens`: GET paginado/POST rascunho, `/{id}` GET/PUT rascunho e `/{id}/historico` GET. Transições POST `/{id}/{planejamento|revisao|inicio|reservas|confirmacao|cancelamento|aborto-sem-consumo|confirmacao-ocorrencia|reversao|anotacoes}`, com chave/versão e política de cada comando. Prévia não reserva; confirmação carrega reais por linha/lote/local, saídas/perdas e motivos; resposta fixa execução/eventos/lotes/snapshots. Estado atual vem do GET, separado da resposta histórica de replay. Nenhum endpoint F02 será criado em F01.

### 7.2 Reservas, atomicidade e rastreabilidade

F02 acrescenta `ReservasEstoque` (OrdemId, LoteId, LocalId, quantidade, estado/histórico) e QuantidadeReservada à posição: `0 ≤ reservado ≤ saldo`; disponível = saldo − reservado. Ordens Planejadas não reservam. Em execução mostra saldo **contabilizado**, parcela reservada e disponibilidade; não promete contagem instantânea durante transformação. Um mínimo modelo de eventos de reserva preserva que uma saída já foi reservada, mesmo que reserva posteriormente liberada.

O protocolo de locks de F01 protege reservas, saldos, ordem e elegibilidade. Reduzir/reatribuir reserva exige declaração/evidência da parcela ainda fisicamente não consumida na posição. Material já retirado/transformado permanece reservado até confirmação/ocorrência; liberar antes criaria disponibilidade fictícia. Ajustes/saídas/transferências futuras só debitam `saldo − reservas alheias`; ajuste de contagem abaixo de reserva retorna conflito, exige resolver ordens/ocorrências antes. Bloquear/inativar é permitido com reserva e sinaliza ordem afetada, sem liberar silenciosamente. Encerrar exige saldo/reserva zero. Confirmação revalida item/lote/local/receita/perfil selecionado e relógio. Histórico é consultável; nenhum status publicado serve de licença eterna.

Confirmar em transação única: replay primeiro; versão/estado; quantidades/conversões; conferir `consumoReal ≤ reservaDaOrdem + saldoNãoReservado`; debitar consumos, criar lotes de saída **novos**, creditar entradas, registrar perdas/medidas/diferenças, liberar todas as reservas da ordem, congelar snapshots planejado/real, nutrição calculável/lacunas, auditoria, Confirmada e resposta. Código de lote único; uma execução/confirmacao por OrdemId no banco. Mesmo item em entrada/saída pode ter lotes diferentes; lista de entrada não contém IDs de saída da própria transação. Uma ordem pode consumir vários lotes explicitamente.

Confirmação exige consumo real total positivo e cada consumo/saída vinculado a linha/ItemId do snapshot planejado; substituição de material exige revisão antes da retirada física, não alteração silenciosa na confirmação. Sem consumo/transformação, usar aborto sem consumo. Menor consumo libera reserva excedente na confirmação; maior consumo nunca toma reserva alheia. Perda total permite saída aproveitável zero, com destino das quantidades consumidas/motivo/Admin, sem divisor/rendimento artificial. Quantidades de dimensões diferentes continuam separadas; conversão ausente não vira perda. Balanço calculável e diferença apontada exigem justificativa; déficit documental é mostrado, sem igualdade inventada. Nutrição de saída não herda grão por moagem automaticamente; usar perfil/estimativa autorizada com contexto ou indeterminado.

Todas as entradas conectam-se conservadoramente a todas as saídas da execução; não inventar repartição molecular. API `/estoque/lotes/{id}/rastreabilidade` só em F02, expansão explícita padrão 2/máxima 10, página até 100, orçamento de 500 relações por pedido e continuação/corte. Saídas intactas e eventos de dependência consultados por sequência/vínculos, não timestamp empatado ou saldo final. Interfaces de detalhes devem mostrar origem/destino e ordens dependentes sem percurso ilimitado.

### 7.3 Bloqueio/vencimento durante execução: decisão material F02

D05 deixa material fisicamente transformado antes de ser contabilizado. A proposta antiga, ao recusar qualquer confirmação após bloqueio/vencimento, poderia manter esse fato sem registro e reservas eternas. Recomendação:

1. Confirmação ordinária continua estrita; bloqueio/inativação/vencimento durante reserva impede confirmação ordinária e pede revisão. Se nenhum material foi consumido, Admin aborta sem consumo.
2. Se houve fato físico, Admin usa **confirmação de ocorrência**, informa motivo, horários observados, evidência, materiais/destinos reais e referências que ficaram inelegíveis. Serve para contabilizar o ocorrido; não aprova consumir vencido nem omite desvio. Mesmas constraints de quantidade/reservas/atomicidade/idempotência.
3. Toda saída da ocorrência nasce Bloqueada em Segregação, ainda que matéria de entrada não estivesse vencida no momento observado. Sem saída aproveitável, registra perda real/perda total. Não forçar perda para esconder material que existe. Posterior liberação Admin exige evidência e validade admissível.
4. Se data observada/conversão/quantidade não puderem ser justificadas, não produzir confirmação fictícia; manter pendência explícita e resolver inventário/ocorrência com responsáveis. Processos que precisam contabilizar consumo antes do fim exigem recorte WIP F06, não remendo de D05.

Esse fluxo e seus poderes exigem aceite de F02. Não será implementado em F01 nem escolhido como autorização de exceções operacionais por silêncio.

### 7.4 Reversão e replay

Reversão integral é **correção de lançamento indevido** com declaração de que os insumos físicos permanecem nas posições originais/evidência de contagem e de que os lotes de saída registrados não representam produto efetivamente fabricado. Transformação real, inclusive perda total, não é desfeita por um crédito de insumos. Reprocessar material real exige outra ordem. Reversão não é `DELETE`.

Admin/motivo/chave/versão; somente uma por execução; todas as saídas ainda com quantidade original na posição original e **nenhuma saída, transferência, consumo ou reserva posterior, inclusive já liberada**, desde a confirmação. Entrada corretiva adicional ou alteração quantitativa também bloqueia até análise específica; não usar saldo recomposto como prova. Consulta mostra IDs dos fatos impeditivos. Perda total exige a mesma evidência de lançamento indevido, mesmo sem lotes gerados.

Lote de insumo Encerrado é terminal e impede reversão integral; não o reabrir implicitamente. Correção presente em novo lote requer evidência e referência, por procedimento próprio. Na correção aprovada: debitar lotes de saída originais, marcar encerrados quando zero, creditar insumos originais não encerrados na posição histórica sob mesmo lock; insumos recompostos ficam Bloqueados para conferência/liberação posterior, preservando validade e flag Ativo atuais. A situação muda explicitamente para Bloqueado com histórico. Não reativar item/local nem apagar reservas/eventos passados. Guardar compensações, auditoria, estado Revertida e resposta juntos. Se evidência só permite ajuste presente, usar ajuste Admin com contagem atual; nunca restaurar ancestrais de saída já utilizada.

Replay de confirmação após reversão retorna resposta original Confirmada; GET atual mostra Revertida e vínculo de compensação. Mesmo payload/chave retorna uma execução; outra chave em ordem Confirmada/Revertida retorna conflito, sem novo consumo. Replay da própria reversão devolve compensação original, sem repetir créditos. Chave divergente sempre conflito. UI separa resultado histórico do comando e estado atual consultado.

## 8. F03–F06 — roadmap com gates próprios

### F03 — Ponte explícita de ovos

Pode depender apenas de F01 aceita para recebimento simples; depende também de F02 quando a entrega incluir consumo em transformação. Não há dependência artificial de F04. Antes de importar: confirmar se cada `ProducaoOvoId` é uma unidade ou amostra; se amostra, manter entrada coletiva manual e projetar origem coletiva separada. Semântica não é inferida de uma linha de tabela.

Seleção humana de IDs/item ovo/lote/local; ponte única vitalícia por ProducaoOvoId, inclusive depois de compensação. Snapshot do AnimalId/postura/peso/observação/UpdatedAtUtc e hash do conteúdo observado, sem inventar Revisao. Prévia de seleção e confirmação devem comparar hash/UpdatedAtUtc observado; mudança concorrente detectável causa conflito/novo aceite, sem adquirir lock de genealogia. Capturar um snapshot consistente dos registros; edição posterior pode ocorrer e aparece na conciliação, sem recalcular saldo.

Conciliação com manual/coletivo: declarar “não recebido antes” ou escolher evento anterior e mapear origem **sem incrementar saldo**, com evidência e fato adicional imutável, sem editar a entrada. Item ovo deve coincidir; total de IDs associados, inclusive anteriores, não ultrapassa unidades comprovadas da entrada. Entrada apenas em kg não prova contagem. Se já recebido e não há prova de equivalência, bloquear importação duplicadora; decidir tratamento humano. Índice impede duplicidade da ponte, mas não prova que origens livres não duplicaram fisicamente o mesmo ovo. Peso padrão da Espécie nunca converte lote; massa registrada é evidência, fator do lote continua explícito. Não importar todas as posturas, usar ovos de Ciclos como estoque nem alterar contrato Animal. Gate: seleção/parcialidade/unicidade concorrente/edição/replay/compensação e leitura do acervo preservada.

### F04 — Custos de materiais

Depende de F02 aceita e prioridade explícita. Preço de referência (fonte/data/unidade) separado de custo apurado de materiais consumidos; não custo industrial completo. Registros F01/F02 antigos sem custo permanecem desconhecidos. Propor camada aditiva de evidência/avaliação, com data/fonte/método e vigência presente, sem editar eventos/snapshots anteriores nem declarar apurado retroativo sem comprovação.

Novo recebimento fixa custo unitário conhecido/desconhecido e proveniência. Mesmo material com custo diferente exige novo lote interno (código externo pode repetir), sem média silenciosa. Transferência preserva camada/custo; retorno/reversão compensam referências correspondentes. Confirmação congela valores e rateio. Total parcial mostra conhecido + lacunas; não zero automático, margem ou lucro sobre incompleto.

Rateio manual por saída/perda, percentuais exatos somando 100 (até seis casas), motivo/autor. Reconciliação em centavos BRL por maior resto e desempate estável por ID; custo unitário com escala própria declarada, dividido pela quantidade positiva de cada saída. Perda total destina 100% a perda, sem dividir por zero; saída zero não recebe custo unitário. Reverter custo nunca cria caixa. Gate: centavos reconciliados, casos de empate, faltantes, custo distinto, entradas antigas, perda total, transferências e snapshots preservados.

### F05 — Otimização assistida

Não é consequência automática de F04. Requer problema e dados/metas/aplicabilidade maduros; F04 só obrigatório se objetivo econômico, F01/F02 pertinentes se houver disponibilidade/reservas na restrição. Primeiro contrato: variáveis (massa BN/inclusão e decisões inteiras quando necessárias), unidades, objetivo, limites de inclusão, componentes/contextos, política de estimativas/lacunas, cortes de disponibilidade e precisão/tolerâncias/limites computacionais.

Resultado congela dados, metas, modelo/algoritmo/versão/seed quando pertinente e corte; não reserva estoque. Mostrar contribuições/restrições ativas/proveniência e distinguir viável, inviabilidade demonstrada, dados incompletos/incompatíveis e limite/erro do algoritmo. Criar variação/rascunho apenas por ação humana; preservar publicada. Ótimo não certifica dieta; não inventar metas por espécie ou equivalências energéticas. Nenhum solver/dependência foi escolhido/pesquisado neste planejamento; avaliar fontes técnicas primárias atuais somente quando contrato exigir escolha. Gate: soluções sintéticas conhecidas, inviáveis provadas, incompletas, estimativas excluídas, restrições integrais quando adotadas e timeout sem sucesso fictício.

### F06 — Alternativas independentes

| Recorte candidato | Dependência e impacto a desenhar antes de autorizar |
| --- | --- |
| Retenção/distribuição por componente e múltiplas saídas | MVP/formulação e demanda processual; perfis/hipóteses, balanço e snapshots de cada saída. F02 para execução física; F04 quando houver rateio econômico. Sem segurança de conserva presumida. |
| WIP / consumo parcial | F02; livro/reservas por etapa, perdas/apontamentos parciais, abortos/reversões por fatos e disponibilidade realmente consumida. Substitui D05 mediante contrato próprio. |
| Recebimento ligado a compra | F01; pedido/recebimento parcial/devolução/referências externas, custo se F04; evento idempotente e reconciliação separada de despesa/caixa. |
| Saída ligada a venda | F01 e reserva pertinente; pedido/expedição/devolução, rastreabilidade/elegibilidade/custo e impossibilidade de reverter saída já usada. Integração explícita/idempotente com Financeiro, sem caixa em duplicidade. |

Escolher uma vertical por demanda; não escolher todos os módulos pela presença de F06. Fiscal, rótulos e segurança alimentar exigem contexto jurisdicional/processo/mercado e fontes primárias vigentes, com responsável de validação definido quando solicitados. Não foram pesquisadas normas nem prometida conformidade neste recorte. Integrações usam eventos/referências idempotentes e outbox/inbox somente se demanda distribuída concreta justificar, sem reescrever história.

## 9. Migrações, preservação e recuperação

F01 futura cria apenas LocaisEstoque, LotesMateriais, PosicoesEstoque, EventosEstoque, MovimentosEstoque, HistoricoEstoque e ComandosEstoque, índices/FKs/guards próprios. Nome final segue convenções existentes. Snapshot e evidência são JSONB tipados, com colunas relacionais para identidade/posição/quantidade/chaves/corte. Não armazenar livro inteiro em JSON para esconder invariantes. Sem Reserva/Ordem/PonteOvo/Custo/Solver/Compra/Venda.

Migration aditiva: códigos únicos normalizados, posição única, escopo único de idempotência, evento/ordinal/seq únicos, abertura única por posição (índice de referência específico), FKs Restrict, saldo/quantidade/unidade/inteiros/estados válidos, guard imutável de fatos e unidade. Fixação de unidade de Item no uso físico reutiliza flag existente; registro em histórico catálogo/físico sem nova alteração retroativa de itens no Up. Trigger de uso físico impede inconsistência lote/item; constraints não substituem o protocolo entre escritores.

Nenhum seed de local/lote/saldo/nutriente/custo/validade/meta, backfill de caixa/animais/receitas ou ligação automática de laudos. Validar Up em base nova e cópia isolada da main anterior, comparando **antes de inserir fixtures** IDs/contagens/referências/valores (hash por linha de todas as tabelas anteriores). Adaptar teste histórico do MVP para parar em `AddCatalogAndFormulation`, mantendo comparação integral; novo teste F01 valida latest e ausência das fases adiadas. Gerar SQL revisável de todas as migrations pendentes da base de origem real, não apenas a última.

Shared DB não recebe migration por esta autorização. Antes de futura aplicação específica: identificar `__EFMigrationsHistory`, versão/porta/PID/banco/finalidade, backup/restore revisados, compatibilidade de writers, SQL de todas as pendências e plano de pausa de escritas. API não migra no startup. Build isolado evita DLLs em uso. Preservar volume privado já provisionado, fornecendo `Images__PrivateRoot` explicitamente; não registrar caminho privado nem provisionar volume vazio. Revalidar conteúdo de imagem autenticada após qualquer reinício autorizado.

Recuperação: antes de publicação, descartar apenas cópia de teste identificada se necessário. Depois de fatos reais, manter schema/livro e preferir forward fix ou restore coordenado de banco/volume com aceite; Down destrutivo perde história e não é rollback seguro. Não recalcular nutrição/custo/saldo retroativamente por trocar código. Reconciliação é evidência de leitura, não ferramenta automática de reparo.

## 10. Cenários verificáveis e aceite

| Cenário sintético | Resultado esperado / fase |
| --- | --- |
| A 10 kg → transferir 3 kg para B vazio | A=7/B=3/total=10, duas linhas um evento, revisões e replay; falha entre débito/crédito deixa A=10/B=0, sem evento/auditoria/comando parcial. F01 PostgreSQL. |
| Últimas 5 un, duas retiradas de 5 | Barreiras comprovam duas sessões em disputa; uma confirma, outra conflito, saldo zero, uma saída. F01. F02 repete com reservas/ordens distintas. |
| `1 g`, resíduo sub-micro, inverso contextual e `un` fracionário | Sem arredondamento oculto; aceite/resíduo ou recusa zero/fração/overflow; transferência não converte novamente. F01. |
| Bloquear/inativar/vencer enquanto comando espera | Sob locks/clock controlado, novo comando revalida e não consome ordinariamente; recebimento/segregação/descarte permitidos só pelo contrato. Catálogo e física disputam advisory comum. F01. |
| Grão 100 → moído 98 + perda 2; moído 60 → ração 60 | Ordens separadas; grão remanescente zero, moído 38, ração 60; segundo consumo não debita grão. Mesmo ItemId do moído; ancestrais consultáveis. F02. |
| Perda total 10 kg | Consome 10, saída zero, perda/motivo/Admin registrados, reservas liberadas, sem lote/custo unitário positivo inventado. F02; F04 custo destinado à perda. |
| Vencimento/bloqueio durante reserva | Confirmação ordinária recusada; sem consumo físico → aborto Admin; com fato → ocorrência conforme aceite, saída Bloqueada, sem reserva eterna silenciosa. F02. |
| Reversão antes/depois de uso | Antes: só lançamento indevido com evidência pode compensar. Depois de saída/transferência/reserva até já liberada: bloqueia mesmo com saldo recomposto. Transformação/perda real não recria insumo. F02. |
| Confirmação dupla, chaves iguais/diferentes; replay após reversão | Uma execução; chave igual mesmo payload resposta histórica; outra chave conflito; payload divergente conflito; reversão uma compensação. F02. |
| Ovo já recebido manualmente, edição posterior | Ponte explícita não soma duplicata; snapshot/hash/UpdatedAtUtc e diferença visível; compensação não permite segunda importação do ID. F03. |
| Custo ausente e rateio de R$0,01 em partes | Ausente é parcial; centavo distribuído uma vez por maior resto, soma preservada; nenhum caixa. F04. |

F01 aceita quando a vertical completa permite cadastrar/consultar Local/Lote, abrir inventário e receber/mover/consumir/ajustar com permissões, livro/saldo/reconciliação/precisão/versionamento/replay e UI reais; migrations/preservação, PostgreSQL concorrência/rollback, API/auth, navegador e CI comprovados. F02 exige reservas/estados/execução/rastreabilidade/reversão/ocorrência com os gates próprios; F03–F06 requerem seus contratos e critérios acima antes da tarefa executável. O [plano](../plans/2026-10-07-continuidade-f01-f06-430-implementation.md) especifica a matriz por camada.

Nesta entrega documental, validam-se referências/links, consistência do recorte e diff/segredos; não se alegam testes funcionais locais executados. Branch/commit/push/PR/checks e limitações reais serão registrados na #431; Em validação enquanto houver revisão humana/merge. A #430 permanece Proposta: planejamento técnico entregue não representa aceite das fases.
