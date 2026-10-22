# P1.2 — paginação da Reprodução (#481)

Execução da [Tarefa #481](https://devops-lab.tailaf9418.ts.net/issues/481), filha da [Evolução #467](https://devops-lab.tailaf9418.ts.net/issues/467), relacionada a [#359](https://devops-lab.tailaf9418.ts.net/issues/359) e [#478](https://devops-lab.tailaf9418.ts.net/issues/478). A API autenticada foi validada e a Tarefa confirmada **Em andamento** antes da investigação. A #480 preparou a instrução e não foi reutilizada.

Baseline após fetch: `79a0030089e5d85b5be04f141e63bcf6d17a86a1`, `origin/main`. Branch `codex/481-p1-reproducao-paginacao`, em worktree isolada anexada ao chat. O checkout principal e seus diretórios locais, os serviços anteriores, a cópia limpa do piloto e o backup agendado foram preservados. A PR #23 de pesquisa permanece fora desta entrega.

## Antes e depois

Antes da correção, cada lista foi montada com uma fixture exclusivamente de teste contendo **26 registros**, com resposta da API simulada em páginas de 25 e metadados `totalItems=26`, `totalPages=2`. Os três testes foram executados contra a implementação da baseline e falharam: Cruzamentos e Proles não tinham o botão `Anterior`; Ciclos não exibia `Página 1 de 2`. Os 25 primeiros registros eram apresentados e o 26º não era alcançável. Essa é reprodução técnica sintética, não observação de uso humano ou de 26 registros reais no piloto.

Depois, cada consulta envia `page` e `pageSize=25`; `Anterior`/`Próxima` e os totais usam a resposta do servidor. Não há download ilimitado nem total calculado no cliente. Filtros de status/macho/fêmea em Cruzamentos e fluxo/status em Ciclos reiniciam a página. Mudar ou retirar o contexto pai também reinicia a lista, inclusive quando o router reutiliza a rota.

Detalhes, edição e criação conservam página, filtros e origem. A cadeia Cruzamento → Ciclo → Prole conserva os retornos dos três níveis; `listNavigation` só reutiliza um pai cujo ID corresponde à consulta atual. Acesso direto ao contexto pai oferece um retorno determinístico sem depender de history state anterior.

Cada resposta pertence a uma consulta identificada; mudanças invalidam imediatamente as linhas e os totais anteriores. Cleanup descarta respostas substituídas e a geração de consulta cobre A→B→A enquanto B ainda está pendente. Erro tem retry na mesma página/filtros e não se apresenta como vazio. Uma página que deixou de existir é consultada novamente na última página válida informada pelo servidor; `totalPages=0` exibe zero registros e controles desabilitados, sem inventar uma página no total.

No Chrome real, desabilitar o botão de paginação durante a carga perdeu o foco para `body`, apesar de o botão permanecer no DOM. A implementação final transfere o foco para o resumo permanente **antes** dessa transição. O resumo é um grupo nomeado e vivo; não há refocalização assíncrona que roube o foco de um filtro ou link escolhido depois. `TableRegion` mantém a rolagem horizontal acessível por teclado em largura estreita.

## Ambiente e dados

Ambiente **P1-481-SYNTHETIC-ONLY**: frontend `https://localhost:5179`, API `https://localhost:7007`, PostgreSQL **18.1** novo e exclusivo, somente loopback. Banco e conexão foram verificados contra o alias, usuário, listener e diretório efetivo do cluster descartável. Contas Admin/comum, espécie, dois animais e 26 Cruzamentos, 26 Ciclos e 26 Proles são fixtures marcadas `P1.481 TEST ONLY`. Nenhum dado de fonte/piloto foi copiado.

Antes de iniciar a aplicação, as **18 migrations existentes** foram conferidas contra a versão e o banco inicialmente vazio. O SQL completo foi gerado e revisado: 49 tabelas, 103 índices, sem DROP/TRUNCATE/DELETE; o UPDATE de backfill de Pessoas foi revisado e incidiu sobre uma base nova vazia. Depois da aplicação, `__EFMigrationsHistory` correspondeu exatamente às 18 migrations requeridas. Não há alteração de backend ou migration nesta entrega, nem aplicação em banco compartilhado/produção.

O volume privado foi configurado para a pasta exata, mas esta base possui **zero imagens e zero referências a imagens**. Nenhuma tela de imagem foi validada. Não se reivindica `200 image/png`, renderização de imagens ou cumprimento desse gate no piloto. Configuração, credenciais, chaves, cookies, cluster e logs privados ficam fora do Git.

## Validações

Os resultados sanitizados estão em [checks.json](evidence/481/checks.json).

| Gate | Resultado |
| --- | --- |
| Regressão na baseline | 3 testes executados antes da correção; 3 falhas esperadas, fixture de 26 registros |
| Frontend completo | **627/627 testes, 57 arquivos**, incluindo retornos reais do router e serviços autenticados; lint/build aprovados |
| Backend completo | Resultado final registrado no recibo; ver observação de desempenho abaixo |
| Diff e revisão | `git diff --check` aprovado; duas revisões independentes, sem achados pendentes após ajustes de total vazio, concorrência, foco e nome acessível |
| UI 1440 e 390 px | Nas três listas, 1→2→1 apresenta 25→1→25 linhas, 26 registros/2 páginas, limites corretos, detalhe/edição/cancelamento de criação e retornos preservados |
| Hierarquia 1440 e 390 px | Cruzamento 26 → Ciclo 26 → Prole 26 → pais → listas, conservando páginas 2, filtros e IDs dos pais |
| Falha/retry e vazio | GET atrasado/500 controlados nas três listas: zero linhas antigas, erro distinto de vazio, retry recupera a página 2 e o contexto; vazio real da API nas três listas |
| Respostas fora de ordem | Filtros rápidos de Cruzamentos/Ciclos com resposta anterior atrasada no browser; resultado vazio atual preservado. Troca de contexto de Proles e A→B→A também cobertos por componente |
| Mutação real, base descartável | Iniciar último Cruzamento e concluir último Ciclo sob filtro: total 26→25, página 2→1 e os 25 registros anteriores alcançáveis; zero mutações na fonte/piloto |
| Sessão e metadados | APIs sem sessão retornaram **401/401/401**, UI redirecionou ao login; conta comum autenticada acessa as listas, com metadados reais e 26 IDs distintos entre as páginas |
| Teclado/layout | Enter na paginação, foco no grupo nomeado, rolagem da tabela com seta quando necessária e nenhuma largura da página excedendo a viewport |

O `dotnet test GenSW.sln` padrão foi executado duas vezes: **607/608** em ambas; somente o benchmark de genealogia de 10 mil animais excedeu 500 ms, ora candidatos (558,1 ms), ora árvore (703,3 ms), sob concorrência de testes. O mesmo teste isolado passou: árvore p95 **214,2 ms**, candidatos p95 **200,5 ms**, SQL máximo **9**. O gate completo foi repetido com agendamento serial (`-m:1`, `xUnit.MaxParallelThreads=1`, `xUnit.ParallelizeTestCollections=false`), sem alterar limites, testes ou implementação do backend. O recibo identifica os resultados finais dessa rodada; a CI do HEAD entregue é registrada na #481 e na PR.

Os primeiros gates frontend encontraram dois mocks de rotas que ainda esperavam URLs sem query; foram ajustados para exigir `page=1&pageSize=25`. No harness de navegador, a navegação direta sobre a mesma entrada mantinha corretamente o history state, a rolagem por seta exigia aguardar o frame e o catálogo de animais precisava terminar de carregar antes de conferir o select restaurado. Essas esperas foram corrigidas. O cliente HTTP Node do harness não confiou no certificado local durante uma tentativa de `route.fetch`; as verificações finais usam requisições do próprio Chrome com TLS padrão, sem bypass.

Capturas de revisão: [Cruzamentos desktop](assets/481/cruzamentos-page2-1440.png), [Cruzamentos estreito](assets/481/cruzamentos-page2-390.png), [26º Ciclo estreito](assets/481/ciclos-reprodutivos-page2-390.png), [26ª Prole estreita](assets/481/proles-page2-390.png) e [erro recuperável estreito](assets/481/proles-error-390.png). Contêm apenas fixtures sintéticas.

## Limites e entrega

Build conserva o aviso de bundle acima de 500 kB (644,72 kB). Emulação de largura não substitui aparelho físico, Safari ou leitor de tela. Não há mudança de status permitido, quantidade, conversão de Prole, cálculo reprodutivo, Filiação/pedigree, referências históricas ou ovos/estoque. Reverter esta entrega consiste em retornar o frontend anterior; não há migration ou correção de dados a reverter.

Seletores limitados aos 100 primeiros animais e rótulo UUID histórico permanecem backlog separado. O aceite funcional do Caixa #478 e o login/aceite do piloto #472/#473 permanecem com seus responsáveis e evidências próprias. Integração de código ou CI não concede esses aceites.

Commit, push, PR e CI do HEAD entregue são registrados na #481 e na PR, evitando referência circular ao commit deste relatório. A entrega permanece **Em validação** até revisão, merge e aceite humano. Nenhum merge, publicação, deploy, F02–F06 ou migration compartilhada foi executado.
