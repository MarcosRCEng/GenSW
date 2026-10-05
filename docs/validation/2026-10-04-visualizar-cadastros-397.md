# Visualização de registros nos cadastros — #397

Data: 04/10/2026 (America/Sao_Paulo). [Evolução #397](https://devops-lab.tailaf9418.ts.net/issues/397), execução e entrega [#398](https://devops-lab.tailaf9418.ts.net/issues/398), inventário/API/revisão [#399](https://devops-lab.tailaf9418.ts.net/issues/399), básicos [#400](https://devops-lab.tailaf9418.ts.net/issues/400), animais/reprodução [#401](https://devops-lab.tailaf9418.ts.net/issues/401). As tarefas foram criadas e confirmadas **Em andamento** pela API antes das atividades correspondentes. Aceite e revisão humana continuam pendentes.

## Inventário e decisão

Todos os caminhos API abaixo são relativos a `/api/v1`. Os módulos apenas planejados no menu não receberam telas.

| Cadastro/lista | Ações anteriores | Consulta individual | Detalhe entregue |
| --- | --- | --- | --- |
| Pessoas `/pessoas` | Editar, Inativar/Reativar | GET `/pessoas/{id}` existente | `/pessoas/:id` novo |
| Espécies `/especies` | Editar, Inativar/Reativar | GET `/especies/{id}` existente | `/especies/:id` novo |
| Raças `/racas` | Editar, Inativar/Reativar | GET `/racas/{id}` existente, inclui espécie | `/racas/:id` novo |
| Variedades `/variedades` | Editar, Inativar/Reativar | GET `/variedades/{id}` existente, inclui espécie | `/variedades/:id` novo, com galeria |
| Animais `/animais` | Editar, Inativar/Reativar | GET `/animais/{id}` existente, inclui classificações | `/animais/:id` novo, com painéis em leitura |
| Cruzamentos `/cruzamentos` | Consultar, Editar, alterar status | GET `/cruzamentos/{id}` existente | `/:id` existente reutilizado |
| Ciclos `/ciclos-reprodutivos` | Consultar, Editar, Concluir | GET `/ciclos-reprodutivos/{id}` existente | `/:id` existente reutilizado |
| Proles `/proles` | Consultar; Editar/desdobrar/converter no detalhe | GET `/proles/{id}` existente | `/:id` reutilizado; operações explícitas em `/:id/editar` |
| Categorias financeiras `/financeiro` | Renomear, Inativar/Ativar | GET `/financeiro/categorias/{id}` novo | `/financeiro/categorias/:id` novo |
| Lançamentos `/financeiro` | Histórico, Corrigir, Cancelar, Ajustar | GET `/financeiro/lancamentos/{id}` existente | `/financeiro/lancamentos/:id` novo |

Identificações físicas, registros institucionais, pesagens, produção de ovos e imagens receberam Visualizar junto às ações atuais nos painéis. Usam `/animais/:animalId/{identificacoes|registros|pesagens|producoes-ovos|imagens}/:recordId`; imagens de variedades usam `/variedades/:varietyId/imagens/:recordId`. Identificações e pesagens reutilizam GET individual. Registros institucionais são localizados na coleção completa do proprietário; produção e metadados de imagens percorrem as páginas existentes até localizar o ID, incluindo imagens inativas. Assim, a URL continua válida para registros fora da primeira página. Essa consulta por coleção pode exigir várias requisições em históricos grandes; não foi criado novo endpoint onde a consulta existente atende funcionalmente.

Filiações e árvore permanecem relacionamentos consultáveis do Animal. Imagens ativas carregam conteúdo autenticado, com proporção preservada no detalhe. Para imagens inativas, a política existente bloqueia conteúdo binário; os metadados continuam visíveis com explicação.

## Comportamento e segurança

- Visualizar vem antes das ações existentes, disponível também para registros inativos/cancelados autorizados.
- Detalhes usam rótulos e listas de definição, sem montar o formulário de edição. Proles mantém conversão e desdobramento na rota de edição.
- Estados de carregamento, 403, 404, falha e nova tentativa são compartilhados; respostas obsoletas após troca de ID são ignoradas.
- Filtros, ordenação, mês e paginação das listas são preservados em `history.state` quando se retorna pelo botão ou pelo navegador; filtros por relacionamento já presentes na query string são conservados. Abertura direta usa o retorno padrão. Subregistros retornam ao detalhe do proprietário.
- Rotas continuam dentro da proteção de sessão. O backend mantém `[Authorize]`; os cadastros atuais não possuem separação adicional de papel entre leitura e edição. Regras Admin exclusivas do Financeiro permanecem intactas. Pessoa inativa continua sem acesso à edição.
- Novo GET de categoria usa `AsNoTracking`, inclui inativas e projeta apenas os campos públicos. Nenhum schema, migration ou papel foi alterado.
- A consulta faz somente GET de domínio. Renovação de autenticação pode usar POST `/auth/refresh`, sem mutar cadastros.

## Verificações executadas

Backend: **465 aprovados**, zero falhas/ignorados (Domain 112, Application 124, API 117, Infrastructure 112). Comando na raiz:

```powershell
dotnet test GenSW.sln --artifacts-path .gensw/view-build-397 --logger trx --results-directory .gensw/view-397/test-results --verbosity minimal
```

O teste novo de categoria cobre anônimo 401, usuário comum, ativa/inativa, inexistente 404 e projeção pública. Executa GETs com PostgreSQL `default_transaction_read_only=on`, verificando que a consulta funciona sem gravação. Fixtures são exclusivamente dos testes efêmeros, sem criar contas ou registros no banco em uso.

Frontend: **388 testes aprovados em 34 arquivos**, ESLint sem warnings e build TypeScript/Vite aprovados. Os testes novos cobrem links por ID, registros inativos, relacionamentos, consulta sem body/método mutante, erros/retry, histórico além da primeira página, retorno com filtros/página, proteção de todas as rotas novas e preservação das operações de Proles na edição. A regressão das identificações foi adaptada para fornecer MemoryRouter aos novos links. Uma primeira execução da suíte detectou essa necessidade; a execução final passou. Resultados finais e commit/push estão também no Redmine.

```powershell
cd src/Frontend/GenSW.Web
npm test -- --run
npm run lint
npm run build
```

Logs locais: `.gensw/view-397/frontend-tests.log`, `frontend-lint.log`, `frontend-build.log`; TRX em `.gensw/view-397/test-results`. `git diff --check` aprovado. O primeiro lançamento do backend pelo diretório Debug encontrou assemblies ocupados pela instância preexistente; o build/teste isolado acima resolveu o conflito.

## Navegador real

Validado no navegador integrado Chromium com sessão existente, desktop **1440×1000** e celular **390×844**. Não foram criados registros, contas, fotos ou dados de demonstração. A validação confirmou dados carregados nos detalhes e na imagem autenticada, além do documento HTML.

1. Pessoas: Visualizar abre o ID selecionado; recarregar a URL conserva os detalhes; nenhum formulário montado.
2. Espécies, Raças, Variedades e Cruzamentos: seleção pela lista abre o detalhe correto, com rótulos e relacionamentos, sem formulário de edição.
3. Animais: consulta completa, classificações, painéis e metadados; inativo com espécie histórica; retorno mantém filtro Inativos e página 2 de 2.
4. Imagem existente: URL aninhada direta/recarregada, conteúdo privado carregado com sucesso; layout proporcional no desktop e celular.
5. Financeiro: lançamento existente consultado pela lista, navegação à categoria relacionada, novo GET individual e recarregamento confirmados. Nenhuma evidência visual com valores financeiros ou dados pessoais foi salva.
6. Pessoa inexistente por UUID: mensagem de não encontrado e retorno à lista visíveis em celular. Erros 403/500 e retry foram exercitados por testes automatizados, sem alterar permissões ou interromper o serviço para simular falhas.
7. Teclado: Tab do título focado leva ao retorno, com indicador de foco visível. Largura do documento não excede o viewport móvel nas páginas verificadas.

Evidências locais, não versionadas, em `output/playwright/view-397/`: `animal-desktop.jpg`, `especie-desktop.jpg`, `categoria-desktop.jpg`, `imagem-detalhes-desktop.jpg`, `imagem-detalhes-mobile.jpg`, `animal-detalhes-mobile.jpg`, `animal-inativo-mobile.jpg`, `registro-inexistente-mobile.jpg`. Os screenshots relevantes foram inspecionados visualmente.

## Disponibilização e pendências

Aplicação em `https://localhost:5173`; API em `https://localhost:7001`. Frontend Vite existente reaproveitado, API executando o build isolado atualizado. O volume privado de imagens já provisionado em `%LOCALAPPDATA%\GenSW\private-images` foi configurado na instância, sem copiar ou alterar imagens. Segredos permanecem exclusivamente nos provedores locais; nenhum valor foi salvo nas evidências.

Branch `codex/397-visualizar-cadastros`, derivada do HEAD local `8eef174` de `codex/386-navegacao-modular`. Preserva os commits prévios de navegação e Financeiro ainda não integrados em `origin/main`; revisão deve considerar essa dependência. Nenhum merge foi executado. AGENTS.md e todos os diretórios locais preexistentes permanecem fora do commit. Hash final e confirmação do push são registrados nas tarefas, evitando referência circular neste documento.

A base local não possui Ciclos nem Proles: listagens vazias foram verificadas no navegador; detalhes positivos e operações de edição permanecem cobertos por testes automatizados. Não se afirma validação visual positiva desses dois cadastros. Emulação móvel não substitui aparelho físico ou leitor de tela. Revisão de código, aceite humano e merge permanecem pendentes; tarefas seguem **Em validação**, nunca Concluído nesta entrega.
