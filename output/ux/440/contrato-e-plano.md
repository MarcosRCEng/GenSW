# Proposta de navegação e apresentação — decisão #440

Consulta: 11/10/2026, America/Sao_Paulo. Base: `022450bbd651fa3d0571d9eb00fe532292544bb6`. Pesquisa da [Tarefa #440](https://devops-lab.tailaf9418.ts.net/issues/440), filha da [Evolução #386](https://devops-lab.tailaf9418.ts.net/issues/386). Este documento propõe comportamento; não altera a aplicação. Ver [diagnóstico](diagnostico.md), [benchmark](benchmark.md) e [protótipos](index.html).

## Fronteira da evolução

A #386 já define áreas e módulos; a proposta mantém esse mapa e estende sua apresentação às páginas autenticadas, cabeçalhos e controles de retorno. Não redesenha o domínio nem refaz F01. A padronização de tokens e estados é transversal, mas permanece nesta continuidade de navegação. Uma futura mudança de identidade visual completa, recursos de personalização ou nova funcionalidade de domínio deve ter Evolução própria relacionada à #386. Não foi criada demanda de implementação antes da escolha.

## Mapa de informação

Fonte de verdade a reutilizar: `features/auth/navigation/modules.ts`; destino e autorização conferidos em `routes/AppRoutes.tsx`. A identificação do módulo ativo considera a rota específica antes de prefixos genéricos: `/producao/perfis/:id` pertence a Insumos e produtos, embora seu prefixo seja `/producao`.

| Área | Módulo | Destinos existentes no menu proposto |
| --- | --- | --- |
| Global | Início | `/` |
| Cadastros básicos | Pessoas | `/pessoas` |
| Cadastros básicos | Taxonomia | `/especies`, `/racas`, `/variedades` |
| Cadastros básicos | Animais | `/animais`; subregistros permanecem no Animal |
| Cadastros básicos | Insumos e produtos | `/itens`; Categorias `/itens/categorias` como navegação local |
| Cadastros básicos | Propriedades | `/propriedades` |
| Produção e operações | Reprodução | `/cruzamentos`, `/ciclos-reprodutivos`, `/proles` |
| Produção e operações | Produção — transformações | `/producao/receitas`, `/producao/formulacao` |
| Produção e operações | Estoque | `/estoque/saldos`, `/estoque/locais`, `/estoque/lotes`, `/estoque/movimentos`; Reconciliação `/estoque/reconciliacao` no contexto do módulo |
| Processos gerenciais | Financeiro | `/financeiro`; categorias, fechamento e histórico continuam nos controles existentes |
| Sessão | Identificação e Sair | Identificação mínima da sessão; Sair é botão que usa o logout existente, sem nova página de conta |

Produção agrícola, Produção animal, Genética, Compras, Vendas, Fiscal, Contábil, Relatórios e BI continuam texto **Planejado · indisponível**, concentrado na home para não poluir a navegação operacional. Não recebem links. Administração não aparece: não há destino administrativo implementado em AppRoutes. Não criar menu de Usuários só porque a API oferece administração.

## Duas alternativas

| Aspecto | A — camadas | B — sidebar única |
| --- | --- | --- |
| Estrutura | Trilho global 56 px + lista de módulos 208 px; painel secundário transitório de **314 px no protótipo desktop**, dentro do orçamento aproximado de 320 px | Sidebar de 232 px no protótipo, com áreas e grupos visíveis; barra local de páginas do módulo |
| Navegação em repouso | 264 px; painel cobre o conteúdo durante escolha, fecha ao navegar | 232 px; não há segundo menu permanente |
| Área em desktop 1440 px | Exemplo de largura útil: 1176 px antes de padding; painel de 314 px aberto deixa 862 px visíveis (856 px no orçamento de 320 px) | Exemplo de largura útil: 1208 px antes de padding |
| Descoberta | Descrição do módulo e separação explícita entre páginas e ações | Todos os grupos visíveis; ações permanecem junto ao título/registro |
| Intermediário | Retirar a lista fixa quando prejudicar a largura útil; abrir camadas num drawer | Sidebar vira drawer; barra local quebra linha ou oferece disclosure com rótulo atual |
| Celular ~390 px | Um drawer, com módulo e painel apresentados em sequência; nunca três colunas lado a lado | Um drawer com grupos; conteúdo ocupa toda a largura disponível |
| Custo de adoção | Mais estados aberto/fechado, foco e mudança de nível; mais distância das convenções atuais | Menos estados; aproxima a configuração já existente das páginas atuais |
| Risco | Excesso de níveis para poucos módulos; overlays podem bloquear tabela e exigir ida/volta | Grupos longos se muitos módulos futuros forem promovidos; manter hierarquia pequena |

As larguras de 56/208/232/314 px vêm do CSS atual do protótipo; as contas para 1440 px são exemplos de área útil, não medição de produtividade. Os protótipos acionam o drawer quando o **contêiner da aplicação tem até 820 px**; o orçamento futuro de aproximadamente 1100 px será avaliado pela largura necessária ao conteúdo na implementação, depois da escolha. Tema claro, verde e neutros, com tipografia do sistema, são iguais nas duas alternativas; favoritos, tema escuro, abertura externa e métricas de negócio não fazem parte do pedido.

**Financeiro nas duas alternativas:** a demonstração usa drawer modal para consulta/edição de lançamento. Essa é uma proposta de apresentação e foco para os painéis que hoje são inline, e precisa integrar o aceite humano antes de implementação. O fechamento permanece uma ação por callback que mantém o caixa e seu mês/filtros/página, sem criar rota de painel ou enviar mutação. O caixa continua tratando dinheiro realizado; a proposta não cria contas a pagar/receber nem outros recursos financeiros futuros.

**Recomendação: B**, com o contrato abaixo. A quantidade atual de destinos cabe numa navegação única; listas de estoque, históricos e formulários ganham área útil e a transição móvel tem menos estados. O benchmark sustenta filtros próximos às listas, separação entre páginas e ações e foco explícito; não prova que B será mais rápida para usuários reais. A mantém valor se a prioridade humana for descobrir módulos através de descrições e agrupamentos em camadas.

## Contrato comum de navegação

### Localização, links e ações

Um shell autenticado oferece Início, módulo ativo, acesso global e sessão, mantendo a proteção existente. Não há shell público no login. Links levam a páginas reais; botões abrem/fecham painéis, expandem grupos, aplicam filtros ou executam comandos. Um link pode receber aparência de botão. Nunca usar `navigate(-1)` como retorno genérico nem criar `role=menu` para navegação comum.

Grupos expansíveis usam botão com `aria-expanded`/`aria-controls`; o destino atual usa `aria-current="page"`. O estado ativo vem da rota, não da última opção clicada. Detalhes e edição conservam o módulo ativo. No catálogo, perfis pertencem ao Item; versões pertencem à Receita. Abas de conteúdo representam seções do registro; links locais representam páginas; nenhuma convenção exige aba nova do navegador.

### Cabeçalho e hierarquia de ações

1. Breadcrumb curto e sem redundância: Início → módulo/lista → registro → subregistro, quando houver hierarquia real. Breadcrumb é orientação; o retorno pode apontar à origem filtrada e por isso não é idêntico ao breadcrumb estrutural.
2. Título único `h1`, contexto essencial (código, estado, versão ou mês) e descrição curta quando útil. Título recebe foco programático em navegação de página; não recebe foco a cada alteração de filtro.
3. Retorno contextual secundário antes do título ou na faixa superior. No máximo um controle principal de retorno à mesma origem. Início permanece na navegação global.
4. Uma ação principal por tarefa: Criar na lista, Editar no detalhe quando permitido, Salvar no formulário, Gerar prévia no comando, Confirmar recebimento depois da prévia. Ações secundárias ficam próximas; ações destrutivas/restritas conservam rótulos, papéis e confirmação existentes.
5. Em formulários longos, barra Salvar/Cancelar no fim; barra fixa só se não encobrir foco ou conteúdo, com padding equivalente. No móvel as ações quebram linha e mantêm alvos de 44 px. Não duplicar submissões em dois formulários.

### Retornos, cancelamento e pós-salvamento

| Situação | Controle | Destino e fallback | Contexto |
| --- | --- | --- | --- |
| Lista → detalhe | Link “Voltar para Animais/Pessoas/…” | Origem reconhecida; sem origem, lista canônica do recurso | Busca, atividade, relações, página, tamanho, ordenação |
| Detalhe → edição | Link “Editar”; “Cancelar edição” no formulário | Detalhe do registro de entrada; edição aberta da lista volta à lista de entrada; URL direta usa detalhe do ID | Origem da lista atravessa o detalhe e a edição |
| Criação | “Cancelar”; “Salvar” como ação | Cancelar para lista de origem; salvar mantém o destino operacional atual da feature, com contexto, mensagem de sucesso e acesso ao registro | Não impor mudança global de destino ao salvar sem registrar diferença por feature |
| Subregistro de Animal/Variedade | Link “Voltar para Animal/Variedade” | Pai validado pela própria rota; URL direta retorna ao mesmo pai | Origem da lista fica encadeada; seção/aba e paginação do painel quando aplicável |
| Versão de Receita / perfil nutricional | Link “Voltar para Receita/Item” | Pai obtido do recurso; falha em obter pai mantém retorno à lista canônica e estado de erro | Origem da lista e aba de versões/perfis |
| Financeiro, detalhe com rota | Link “Voltar para Fluxo de caixa” | `/financeiro` com origem válida; URL direta usa mês/defaults atuais | Mês, filtros e página; resumo continua independente dos filtros |
| Financeiro, painel de editor/histórico/ajuste | Botão “Fechar painel” em consulta; “Cancelar edição” em edição | Fecha estado local e revela o caixa existente; não navega | Mês/filtros/página intactos; foco volta ao acionador |
| Estoque, lote vindo de Saldos | Link “Voltar para Saldos” | Uma das cinco listas permitidas em `inventoryOrigin`; sem origem, Lotes | Filtro de item/local/lote, página e ordenação |
| Comando físico | Link de retorno antes da prévia; botão “Gerar prévia”; “Confirmar …” | Cancelar antes de confirmar preserva origem; evento confirma somente por ação explícita; retorno à origem após consulta | Revisões, prévia, payload/chave/retry seguem contrato F01; navegação não gera comando |
| Início | Link “Início” no shell | `/` determinístico | Não aplicar filtros de outro módulo |

Salvar em edição iniciada no detalhe deve voltar ao detalhe com mensagem de sucesso; em edição iniciada pela lista, pode conservar o destino de lista existente. Esta regra e a proteção de mudanças não salvas são mudanças intencionais propostas para aceite, a conferir por feature antes da adoção. O protótipo demonstrativo de Animal usa o primeiro caso. Nenhuma diferença de destino legítima é corrigida apenas por aparência.

### Contexto e links diretos

Reutilizar `listReturnState`, `useRememberListState` e `useRestoredListState`, com validação de caminhos permitidos. Estender a passagem do estado nas lacunas documentadas, sem aceitar destinos externos ou prefixos parecidos. Manter filtros de relacionamento existentes na query string. Não mover nomes, termos pesquisados, registros ou valores financeiros para URLs, localStorage ou logs da aplicação real.

O protótipo usa parâmetros no hash **somente sintéticos** para permitir comparar, copiar links e recarregar o roteiro; isso não decide a persistência real. Na aplicação: voltar pela própria entrada de histórico e recarregar a mesma entrada recupera o estado quando o navegador o conservar; abrir em aba nova/copiar URL não transfere `history.state`, portanto usa defaults e relações da URL. Não prometer persistência após limpeza de sessão/histórico. Sem estado válido, nunca adivinhar um histórico de outro usuário. Retorno ao pai por URL direta funciona sem depender da lista.

Um campo editado deve acionar proteção de saída: navegação interna abre confirmação “Descartar alterações?” com “Continuar editando” inicialmente focado e ação explícita “Descartar e sair”. Escape permanece na edição. Reload/fechar aba pode usar a confirmação nativa `beforeunload` apenas quando há alterações; não prometer rascunho persistido. Sessão expirada/conflito conserva o preenchimento em memória enquanto a tela permitir, comunica a situação e não repete mutação automaticamente. Reautenticação segura e recuperação de rascunho exigem conferir a infraestrutura de auth; não armazenar dados sensíveis para contornar isso.

### Responsividade e acessibilidade

Desktop mantém o conteúdo de trabalho prioritário. O protótipo atual usa container query: sidebar fixa acima de 820 px de largura do contêiner da aplicação e drawer até esse valor; não confundir essa medida com a largura externa do navegador. Para a aplicação real, aproximadamente 1100 px é um orçamento inicial a conferir pela largura útil dos fluxos, sem breakpoint definitivo antes da escolha. Drawer modal aplica foco contido, conteúdo de fundo inerte, Escape para fechar e devolução do foco. O painel de descoberta A e o drawer financeiro de ambas as alternativas demonstram essa semântica modal. A adoção do drawer financeiro requer aceite explícito da proposta de apresentação dos atuais painéis inline, conservando callback/contexto. Se a decisão humana mantiver o Financeiro inline, tratá-lo como região nomeada sem trap ou `aria-modal` e registrar esse ajuste antes do código.

No móvel, título e ação quebram linha; formulários usam uma coluna; resumo vira grid compacto. Tabelas preservam colunas operacionais e usam região horizontal **contida**, rotulada, focalizável e com aviso de rolagem. Não aplicar overflow oculto no body para mascarar erro de layout. Se houver apresentação alternativa em cartões, ela deverá expor os mesmos campos e relações, sem duplicar a árvore acessível. Usar cabeçalhos/caption nativos, alinhamento numérico, wrap de nomes e unidade ao lado da quantidade.

Critérios: contraste WCAG 2.2 AA para texto e controles conforme sua função; foco visível e não encoberto, ordem de Tab coerente, nome acessível nos ícones, estados por texto além de cor, alvos mínimos WCAG e preferência de 44 px, anúncio de resultados/erros sem deslocamentos repetidos de foco. APG fornece padrão de comportamento, não certificação. Avaliar 390, 768, 1024 e 1440 px, zoom e teclado; leitor de tela/aparelho físico ficam no aceite manual quando não executados.

## Convenções visuais propostas

A paleta abaixo corresponde à demonstração atual, comum a A/B. Valores de tipografia, espaçamento e largura são convenções iniciais a conferir na adoção; não representam CSS já implementado na aplicação real. Verificar os pares de contraste renderizados e os estados antes de centralizar os tokens.

| Token / convenção | Valor inicial / motivo |
| --- | --- |
| Fonte | Protótipo: Inter quando disponível, fallback `Segoe UI`, Arial e sans-serif; sem download externo. Conferir compatibilidade com os fallbacks já existentes na aplicação |
| Texto | Corpo 14–16 px; título 28–32 px desktop, 24 px celular; entrelinha ~1,45; texto auxiliar nunca ilegível para aumentar densidade |
| Escala | 4 / 8 / 12 / 16 / 24 / 32 px; padding de trabalho 24 px desktop, 16 px celular |
| Superfícies | Fundo `#eef2ef`, painel branco, borda `#dae3dc`, sombras discretas; reduzir o excesso de cartões dentro de cartões |
| Ação principal | Verde `#176348`, texto branco, conforme o protótipo atual |
| Texto principal / secundário | `#24362f` / `#53645a`, conforme os tokens efetivos do protótipo; verificar os pares reais |
| Foco | Contorno azul `#287cd0`, 3 px com afastamento, conforme o protótipo; visível em nav e controles sem depender de hover |
| Estado | Verde sucesso, âmbar aviso, vermelho erro, cinza histórico/inativo, sempre com texto e/ou ícone rotulado |
| Bordas / raio | Controles 8 px, seções 12 px; borda 1 px; manter alinhamento entre módulos |
| Largura | Listas usam largura disponível; formulários podem limitar ~960 px, detalhe ~1120 px, sem estreitar tabelas extensas arbitrariamente |
| Densidade | Uma densidade operacional padrão; linhas ~48–56 px, ações 44 px; sem adicionar seletor de densidade nesta etapa |
| Ícones | Família coerente, SVG existentes ou pictogramas simples sem dependência nova; texto nos destinos críticos, ícone decorativo com `aria-hidden` |

Os tokens serão centralizados somente após aprovação e verificações de compatibilidade; não substituir CSS de domínio por regras globais agressivas. Nenhuma cor sozinha significa permissão, validade ou aptidão.

## Estados e feedback

| Estado | Apresentação e ação |
| --- | --- |
| Carregamento | Região `role=status`, rótulo específico; manter shell e contexto. Desabilitar somente envio em curso |
| Vazio sem registros | Explicação e Criar se autorizado; vazio por filtro oferece Limpar filtros |
| Falha de consulta | `role=alert`, Tentar novamente; contexto preservado e nenhum resultado parcial apresentado como completo |
| 404 | Registro não encontrado + retorno determinístico; não substituir por formulário vazio |
| Sessão expirada | Mensagem de sessão e fluxo de login existente; não tratar como vazio ou falha de dados |
| Papel insuficiente | Mensagem específica; nenhum formulário/comando protegido disponível; ocultar menu não substitui autorização de rota/API |
| Conflito | Conservar campos, informar motivo/versão e ação explícita de recarregar/conferir; sem retry automático de mutação |
| Confirmação | Resumo do efeito real, dados relevantes, motivo/aceites atuais e botão com verbo explícito. Foco inicia na opção segura |
| Sucesso | Mensagem anunciada, link para registro/evento e retorno coerente; não perder filtros nem anunciar que prévia confirma fato |

## Componentes a reutilizar e lacunas

Reutilizar configuração tipada de módulos, guard de sessão, `DetailsPage`/`DetailField`/`DetailStatus`, helpers de lista, `inventoryOrigin`, carregamento de detalhes, paginadores/seletores de servidor e componentes de controle de cada feature. Preservar as máquinas de estado de Caixa e comandos F01. O [diagnóstico](diagnostico.md) contém arquivos/linhas e as diferenças por fluxo.

Lacunas propostas: shell compartilhado, resolução tipada de módulo por rota, cabeçalho/ações/retorno com destinos explícitos, estilos comuns de links/botões/feedback, proteção de alterações não salvas e passagem completa do estado onde ausente. Adaptar os frames atuais, retirando duplicações somente depois de seus destinos estarem cobertos. Não migrar tudo para uma biblioteca nova.

## Plano posterior executável — depende de escolha e autorização

| Etapa | Ação e dependências | Saída / critério de validação |
| --- | --- | --- |
| 0 — Registrar decisão | Receber A/B, aceitar contrato de retornos/estado e direção visual; reconsultar main e processos; criar Tarefa própria filha #386 Em andamento antes do código | Decisões registradas; branch `codex/<tarefa>-padronizacao-navegacao`; nenhum checkout ativo substituído incidentalmente |
| 1 — Fundação | Introduzir shell dentro da proteção atual, módulo ativo, navegação e cabeçalho; manter páginas utilizáveis | Mesmas URLs, acesso direto/login/logout; destino ativo correto inclusive perfis; planejados sem links |
| 2 — Contrato e lacunas | Estender contexto em links/formulários; retorno de origem/pai; cancelamento e dirty guard | Regressões relevantes de Pessoas, Animal, Propriedade, subregistro, perfil e versão; caminho externo/sem estado recusado; cancelar/salvar com filtros |
| 3 — Adoção por módulos | Cadastros → catálogo/produção → Financeiro → Estoque; revisar handlers de painel e comandos, preservar papéis | Sem navegação incidental que envie mutação; caixa fecha painel conservando mês; F01 mantém prévia/chave/revisão/aceite; nenhuma alteração F02–F06 |
| 4 — Responsivo e foco | Revisar tabelas, formulários, drawers, títulos/feedback e foco; retirar controles redundantes completos | Desktop 1440, intermediários 768/1024, celular 390; sem overflow global; Escape/Tab/retorno foco e contraste; estados essenciais |
| 5 — Gates | Testes frontend direcionados e suíte aplicável, lint/build, diff/segredos; backend somente se afetado | Comandos e resultados reais registrados; não reutilizar números de F01 como prova; corrigir falhas relevantes |
| 6 — Revisão | Preparar instância isolada sem modificar banco compartilhado; documentar URLs/roteiro/limites; commit/push/PR e checks | Entrega pronta para validação manual; Tarefa Em validação com branch/hash/PR/evidências; sem merge/deploy/conclusão com pendência humana |

Criar Tarefa própria antes de cada etapa executável do plano, vinculada à #386 e com dependências explícitas; registrar objetivo, escopo, restrições e critérios de aceite e confirmar Em andamento antes de executar a respectiva etapa. Não criar antecipadamente tarefas de etapas ainda não autorizadas. Sem estimativa fechada de horas: o diagnóstico dá ordem e critérios, não mede produtividade.

Riscos principais: conflitos com frames locais, perda de estado em cadeias pai/filho, sobreposição de prefixos de rota, foco em overlays, guard de formulário descartando campos, CSS compartilhado alterando prévias e permissões. Mitigações: adoção incremental, tipos explícitos, helpers allowlist existentes, testes de comportamento relevante e validação de browser em dados sintéticos isolados. Nenhuma mudança de backend, dado ou migration é prevista.

## Decisões materiais para a revisão humana

1. **Estrutura:** A em camadas ou B sidebar única (recomendada).
2. **Retornos e cabeçalho:** aceitar o contrato com destino explícito, separação Fechar painel/Voltar/Cancelar, continuidade de origem e proteção de alterações; aceitar pós-salvamento contextual conforme a origem e a apresentação dos painéis financeiros em drawer modal, mantendo callback/contexto.
3. **Densidade e direção visual:** tema claro com o verde e os neutros demonstrados, densidade operacional única, tabelas completas com rolagem interna e filtros junto à lista.

Escolhas rotineiras de tamanho, componentes e testes serão resolvidas pela implementação dentro destes limites. Se a escolha for feita com ajustes, registrar os ajustes antes de alterar a aplicação. A recomendação não é autorização.
