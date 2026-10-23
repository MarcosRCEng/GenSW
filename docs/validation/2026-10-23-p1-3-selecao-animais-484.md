# P1.3 — seleção de animais em Cruzamentos (#484)

Execução integral da [Tarefa #484](https://devops-lab.tailaf9418.ts.net/issues/484), criada e confirmada **Em andamento** antes da investigação, filha da [Evolução #467](https://devops-lab.tailaf9418.ts.net/issues/467) e relacionada a [#359](https://devops-lab.tailaf9418.ts.net/issues/359) e [#481](https://devops-lab.tailaf9418.ts.net/issues/481). A preparação #483 não foi reutilizada. Autenticação Redmine por variável de ambiente, sem credenciais no Git. AGENTS.md permanece local.

Baseline após fetch: `3a35ef149951ad92a201a6c7acc05359daf2ddab`, `origin/main`, conferida em 23/10/2026. [PR #28](https://github.com/MarcosRCEng/GenSW/pull/28) integrado e [CI pós-merge](https://github.com/MarcosRCEng/GenSW/actions/runs/40031783608) verde. Branch `codex/484-p1-selecao-animais`, worktree isolado anexado ao chat. Os demais checkouts, serviços, dados, piloto e backup foram preservados.

## Reprodução anterior e resultado

Na baseline, lista e formulário carregavam apenas `listAnimals({pageSize:100}).items`. Dois testes de regressão foram executados **antes** de alterar essas telas: ambos falharam porque não existia busca que alcançasse os animais fora desse bloco. A fixture de componente possui 204 animais alternados, 102 de cada sexo: o **101º macho ocupa a posição geral 201** e a **101ª fêmea a posição 202**. A primeira página geral não contém o alvo, embora a API simulada já o encontre por busca. A fixture não representa uso humano do piloto.

A API real da base descartável confirmou, separadamente para ambos os sexos, ausência do alvo tanto na primeira página geral de 100 quanto na primeira página de 100 de seu sexo, e presença na página seguinte e na busca por código. Depois da correção, os testes encontram e selecionam os alvos nos filtros e criam o cruzamento com os IDs originais. Os testes do seletor também chegam ao 101º de cada sexo pela página 11 de dez opções.

## Implementação e contrato

`BreedingAnimalSelector` é compartilhado pelos filtros e pelo formulário. Cada consulta envia sexo, busca por código/nome, página e `pageSize=10`, com `codigoInterno asc`; o servidor desempata por ID. Não há aumento do limite nem carregamento de todas as páginas. A busca real usa ILIKE literal, ignora caixa, remove espaços nas extremidades e escapa curingas. Nenhum endpoint ou migration foi alterado.

A opção selecionada permanece separada dos candidatos. Quando não está na página atual, o detalhe por ID fornece código/nome, com indicação de inativo ou sexo diferente do filtro. Falha na referência conserva o ID, mostra o erro e permite nova tentativa; nunca apaga nem substitui o vínculo. Buscar, paginar ou receber uma resposta não altera a seleção. Candidatos, referência e carregamento do cruzamento têm descarte de respostas substituídas, inclusive A→B→A, rota reutilizada e contexto/sexo alterados.

O backend existente exige animais existentes, IDs distintos e Macho/Fêmea correspondentes. **Não exige ativo, escopo, espécie, raça ou idade**: o seletor não introduz esses filtros. Animal inexistente retorna 404; mesmo ID, sexo trocado e indeterminado retornam 409. Animais/Cruzamentos usam `[Authorize]`, sem exigência de papel Admin: anônimo retorna 401; conta comum autenticada tem acesso. O 403 foi injetado apenas como falha controlada do frontend, sem apresentá-lo como proibição de negócio existente. Filiação permanece fonte de verdade do pedigree.

Carregando, nenhum resultado e erro são distintos, e retry mantém busca/página. Mudar busca ou página de candidatos não reinicia a página da lista de Cruzamentos; alterar o filtro selecionado reinicia somente essa lista. Página/filtros retornam após detalhe, edição, criação e navegação hierárquica da P1.2. O formulário marca alterações apenas nos dados persistidos, preserva o aviso de descarte e ignora saves/carregamentos de uma rota abandonada. Erro ao carregar o cruzamento oferece retry e não abre um formulário vazio para edição.

Os controles são nativos, com rótulos e paginação nomeados. Antes de desabilitar a paginação durante a carga, o foco passa ao resumo persistente; respostas assíncronas não roubam foco posterior. A revisão encontrou e corrigiu submit implícito: **Enter no campo de busca não salva o formulário**, preservando composição de texto e teclado do select. O layout foi conferido em 1440 e 390 px, sem largura de documento acima da viewport; a tabela conserva sua rolagem acessível existente.

## Ambiente, migrations e imagens

Ambiente **P1-484-SYNTHETIC-ONLY**, PostgreSQL **18.1** novo, exclusivo e somente loopback. API `https://localhost:7008`, frontend `https://localhost:5180`, HTTPS validado sem bypass de certificado. Conexão, banco, usuário e diretório efetivo do cluster foram conferidos antes de iniciar a aplicação. Nenhum dado ou conexão do piloto foi utilizado.

O banco começou sem tabelas públicas. As 18 migrations exigidas foram comparadas com o histórico vazio; SQL gerado/revisado, 49 tabelas e 103 índices, sem DROP/TRUNCATE/DELETE. O único UPDATE é o backfill de Pessoas, revisado e aplicado sobre base nova vazia. Depois, `__EFMigrationsHistory` correspondeu exatamente às 18 migrations, antes de iniciar API/frontend. Nenhuma migration compartilhada ou de produção foi aplicada.

Fixture inicial: 102 machos ativos, 102 fêmeas ativas, dois históricos inativos, um indeterminado; contas Admin/comum e 26 registros de cada nível Cruzamento/Ciclo/Prole. As criações e edições do navegador ocorreram somente nessa base. O ciclo final está concluído; para alcançar as 26 linhas, o teste hierárquico usa todos os status e preserva o filtro de fluxo.

`Images__PrivateRoot` aponta para a pasta privada exata, com ACL e escrita verificadas, porém a fixture possui **zero arquivos e zero referências de imagens**. Não foram comprovados miniatura/original autenticados `200 image/png`, renderização de imagens ou gate de imagens do piloto. Runtime, cookies, chaves, cluster e logs privados permanecem fora do Git e dos relatórios.

## Evidências e validação

Resultados sanitizados: [checks.json](evidence/484/checks.json).

| Verificação | Resultado |
| --- | --- |
| Baseline antes da correção | 2 testes, 2 falhas esperadas; fixture de 204 animais |
| Frontend final | **653/653**, 60 arquivos, zero falhas |
| Backend | **608/608**, zero falhas/ignorados, uma execução com agendamento padrão |
| Suites backend | Domain 165; Application 147; Infrastructure 114; API 182 |
| Qualidade | `npm run lint`, `npm run build`, `git diff --check` aprovados |
| UI desktop/estreita | Ambos os sexos além de 100; filtro aplicado; criação 201/edição 200 com IDs corretos; retorno de rótulos/filtros; teclado/foco; Cancelar/continuar/descartar |
| Referência histórica | Inativos fora da página; detalhe 500 controlado conserva ID; retry resolve; busca/vazio não muda vínculo; save 200 conserva IDs |
| Falhas de candidatos | 503 controlado na página 2; retry envia mesma busca/página; 403 controlado visível; vazio distinto; ambos em 1440/390 px |
| Concorrência | A→B→A com respostas antigas liberadas depois da atual no Chrome; ID histórico e candidatos atuais preservados. Troca de rota/sexo/contexto, falha e save antigo também cobertos por componente/router |
| Hierarquia | Cruzamento → Ciclo → Prole e retorno completo, conservando página 2 nas três listas, filtros e IDs dos pais, nas duas larguras |
| Sessão/permissões | APIs anônimas 401; navegador anônimo redireciona ao login; conta comum busca/seleciona ambos os sexos; negativas 404/409/400 conforme contrato |
| Revisão | Duas revisões independentes; achado Enter corrigido e testado; nenhum achado adicional pendente |

A rodada frontend anterior passou 652/652; após acrescentar o teste de Enter, a rodada completa final passou 653/653. Backend executado com `dotnet test GenSW.sln` e apenas parâmetros de logger/resultados, sem mudar paralelismo, testes ou limites. Os problemas locais de tempo/conexão registrados na #481 não ocorreram nessa execução e não foram alvo de correção.

O harness de navegador foi ajustado ao detectar que opções nativas do select são anexadas ao DOM, mas não visíveis com a lista fechada; também deixou de filtrar apenas ciclos em andamento ao conferir a página do ciclo final concluído. Uma espera de nome de conta foi substituída pela conferência autenticada de papel, pois o cabeçalho estreito oculta o nome. Essas tentativas não são contadas como validações aprovadas; as verificações finais corrigidas passaram.

Capturas exclusivamente sintéticas: [seleção desktop](assets/484/selected-1440.png), [seleção estreita](assets/484/selected-390.png), [erro e histórico estreito](assets/484/error-390.png), [lista restaurada desktop](assets/484/restored-list-1440.png) e [lista restaurada estreita](assets/484/restored-list-390.png).

## Entrega e pendências

Implementação: commit `ba26cbd328e49d65e17915d2bf8a3d6258b9e271`, enviado à branch própria, com [CI Backend/Frontend aprovada](https://github.com/MarcosRCEng/GenSW/actions/runs/40034342987). [PR #29](https://github.com/MarcosRCEng/GenSW/pull/29) revisável e anexado ao chat; este relatório e as evidências são acrescentados na mesma branch. O commit final, push e resultado **Backend/Frontend do HEAD final** são registrados na #484 e na PR, evitando autorreferência do relatório. [Checks da PR](https://github.com/MarcosRCEng/GenSW/pull/29/checks). A conclusão técnica só é declarada após ambos verdes nesse HEAD.

Build mantém aviso de bundle maior que 500 kB (**649,13 kB**); `npm ci` informou 14 vulnerabilidades nas dependências fixadas (2 baixas, 5 moderadas, 5 altas, 2 críticas), sem alteração do lockfile ou correção fora deste recorte. Emulação de viewport não substitui aparelho físico, Safari ou leitor de tela. Reversão consiste em retornar o frontend anterior; não há migration nova ou correção de dados de produção.

Tarefa destinada a **Em validação**, com revisão e aceite funcional humano pendentes. Não houve merge, deploy ou publicação. Gate de imagens, login/aceite do piloto #472/#473, Caixa #478 e aceites #481/#482 continuam separados. UUID do Catálogo/formulação, F02–F06, cálculos reprodutivos, conversão de Prole, estoque, custos e solver permanecem fora do recorte.
