# GenSW

ERP agropecuário modular para a gestão de propriedades, produção, animais e genética.

## Visão

O primeiro domínio funcional será a produção e o melhoramento animal, começando por aves e codornas sem limitar a arquitetura a uma espécie. O objetivo de longo prazo é um ERP agropecuário amplo.

## Arquitetura

O backend usa uma separação simples entre `Domain`, `Application`, `Infrastructure` e `API`. O frontend é uma aplicação React independente. A visão detalhada está em [docs/architecture/overview.md](docs/architecture/overview.md).

## Tecnologias

- .NET 8, ASP.NET Core e Entity Framework Core
- PostgreSQL (por configuração externa)
- React, TypeScript, Vite e Tailwind CSS
- xUnit para testes backend

## Estrutura do repositório

```text
src/
  Backend/       API e projetos de domínio/aplicação/infraestrutura
  Frontend/      aplicação web
tests/           testes automatizados do backend
docs/            documentação arquitetural
```

## Como executar

Backend:

```powershell
dotnet restore GenSW.sln
dotnet run --project src/Backend/GenSW.API
```

Antes de iniciar a API, forneça `ConnectionStrings__GenSW` e `Authentication__Jwt__SigningKey` por variáveis de ambiente, User Secrets ou outro provedor externo de segredos. A chave JWT deve possuir pelo menos 256 bits e nunca deve ser adicionada aos arquivos `appsettings`. A aplicação falha cedo quando uma configuração obrigatória de autenticação ou persistência está ausente, evitando iniciar com endpoints indisponíveis.

Issuer, audience e duração do access token são configurações não sensíveis em `Authentication:Jwt`. As origens CORS permitidas vêm de `Cors:AllowedOrigins`; em Development, a origem preparada para o frontend é `https://localhost:5173`. O limite inicial de login vem de `RateLimiting:Login` e é de 10 tentativas por minuto por endereço remoto.

### Provisionamento inicial do administrador

O primeiro administrador é criado somente por uma execução administrativa explícita, nunca no startup da API e sem endpoint HTTP. Em uma base sem usuários, forneça externamente `ConnectionStrings__GenSW`, `InitialAdminBootstrap__Name`, `InitialAdminBootstrap__Username` e `InitialAdminBootstrap__Password`; então execute `dotnet run --project src/Backend/GenSW.AdminBootstrap`. A ferramenta recusa qualquer nova tentativa quando já existir um usuário e não exibe a senha.

O perfil local `https` publica a API em `https://localhost:7001` (e mantém HTTP apenas para redirecionamento). Prepare uma vez o certificado de desenvolvimento com `dotnet dev-certs https --trust`; depois execute `dotnet run --launch-profile https --project src/Backend/GenSW.API`. Isso é necessário para o navegador reenviar o refresh cookie marcado como `Secure`.

Frontend:

```powershell
cd src/Frontend/GenSW.Web
npm install
npm run dev
```

O frontend de desenvolvimento é servido em `https://localhost:5173` e exige a
exportação local do certificado HTTPS confiável do .NET, sem versionar a chave
privada. As instruções de `VITE_API_BASE_URL`, certificado e validação integrada
estão no [README do frontend](src/Frontend/GenSW.Web/README.md).

## Como testar

```powershell
dotnet test GenSW.sln

cd src/Frontend/GenSW.Web
npm test
npm run lint
npm run build
```

## Cruzamentos (NA-07)

O módulo autenticado de cruzamentos está disponível em `/api/v1/cruzamentos` e na interface em `/cruzamentos`. Ele registra macho, fêmea, status, período, objetivo e observação; os participantes devem ter respectivamente sexo Macho e Fêmea, não podem ser o mesmo animal, e a data final não pode preceder a inicial. Cruzamentos entre espécies são permitidos. O registro não cria nem associa descendentes: Filiação permanece a fonte de verdade do pedigree.

## Ciclos reprodutivos (NA-08)

O módulo autenticado de ciclos está em `/api/v1/ciclos-reprodutivos`, acessível a partir de cada Cruzamento. Há vários ciclos por Cruzamento, com fluxos ovíparo (postura, incubação/choco e eclosão) ou gestacional (início, previsão, parto e desfecho). Quantidades e pesos são dados brutos; duração, fertilidade e eclosão são calculadas na consulta, sem persistir percentuais. O módulo não cria animais, ovos individuais ou vínculos de filiação — essas confirmações continuam no domínio de Filiação.

## Proles (NA-09)

O módulo autenticado de proles está em `/api/v1/proles`, acessível na consulta de cada ciclo. Só aceita ciclos concluídos e limita os registros ao total apurado de eclodidos ou nascidos; o registro não modifica os números históricos do ciclo. Lotes podem ser desdobrados, deixando a origem rastreável, antes da conversão de uma unidade individual em Animal. A conversão é única por prole e encaminha os progenitores do Cruzamento apenas como sugestões para confirmação explícita no módulo de Filiação.

## Roadmap macro

Identity, People, Properties, AnimalProduction, Reproduction, Genetics, AgriculturalProduction, Inventory, Purchasing, Sales, Financial, Accounting, Fiscal, Reporting e BI evoluirão incrementalmente. O MVP inicial priorizará autenticação, pessoas, usuários, animais, espécie, raça, cruzamento e pedigree.

## Propriedades como unidades operacionais físicas

O módulo autenticado em `/propriedades` oferece cadastro, busca, edição, visualização e ativação/inativação. A edição de Animal permite associar, transferir ou encerrar seu vínculo operacional; Visualizar exibe o vínculo atual e o histórico em leitura. Animais sem Propriedade, inclusive os de referência, continuam válidos.

Cada Animal possui no máximo um vínculo atual. Transferências preservam os períodos anteriores; a inativação da Propriedade conserva os vínculos existentes e impede novas entradas. O módulo não modifica pedigree, reprodução ou caixa. Consulte [regras, API e publicação](docs/operations/propriedades.md) e [evidências de validação](docs/validation/2026-10-05-propriedades-404.md).

## Governança Git/Redmine

Cada demanda executada pelo Codex deve possuir uma **Tarefa** no Redmine, vinculada a uma **Evolução**. A Tarefa registra objetivo, escopo, critérios de aceite, validações, branch, commit, push e pendências. Commits só são criados após as validações aplicáveis passarem.

## Visualização dos cadastros

A ação **Visualizar** abre detalhes somente de leitura em Pessoas, Espécies, Raças, Variedades, Animais, Cruzamentos, Ciclos reprodutivos, Proles e nos registros financeiros. As rotas por identificador permitem acesso direto, inclusive a registros inativos autorizados. Os painéis de Animal e imagens de Variedade também oferecem consulta individual. Filtros e paginação das listagens são preservados no retorno. Em Proles, conversão e desdobramento ficam no fluxo **Editar**. Consulte o [inventário, verificações e limites do aceite](docs/validation/2026-10-04-visualizar-cadastros-397.md).

## Pesos, imagens e genealogia de Animal

A edição de Animal inclui pesagens individuais, galeria privada e seleção pesquisável de progenitores. Cada pesagem mantém a data observada e separa a idade alvo da idade calculada. A árvore exibe ascendentes e descendentes com expansão explícita; o pedigree legado permanece disponível. Variedade possui galeria própria de catálogo, sem herdar ou fornecer foto de um indivíduo.

As APIs autenticadas ficam em `/api/v1/animais/{id}/pesagens`, `/imagens`, `/progenitores-elegiveis` e `/arvore`; imagens de catálogo ficam em `/api/v1/variedades/{id}/imagens`. Conteúdo binário exige autenticação. Consulte [operação, migrações e backup](docs/operations/animal-evolution.md), [auditoria somente leitura](docs/operations/animal-genealogy-audit.sql) e [evidências da implementação](docs/validation/2026-10-02-animal-evolution-372.md).
