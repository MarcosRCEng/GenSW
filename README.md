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

O perfil local `https` publica a API em `https://localhost:7001` (e mantém HTTP apenas para redirecionamento). Prepare uma vez o certificado de desenvolvimento com `dotnet dev-certs https --trust`; depois execute `dotnet run --launch-profile https --project src/Backend/GenSW.API`. Isso é necessário para o navegador reenviar o refresh cookie marcado como `Secure`.

Frontend:

```powershell
cd src/Frontend/GenSW.Web
npm install
npm run dev
```

## Como testar

```powershell
dotnet test GenSW.sln

cd src/Frontend/GenSW.Web
npm run lint
npm run build
```

## Roadmap macro

Identity, People, Properties, AnimalProduction, Reproduction, Genetics, AgriculturalProduction, Inventory, Purchasing, Sales, Financial, Accounting, Fiscal, Reporting e BI evoluirão incrementalmente. O MVP inicial priorizará autenticação, pessoas, usuários, animais, espécie, raça, cruzamento e pedigree.

## Governança Git/Redmine

Cada demanda executada pelo Codex deve possuir uma **Tarefa** no Redmine, vinculada a uma **Evolução**. A Tarefa registra objetivo, escopo, critérios de aceite, validações, branch, commit, push e pendências. Commits só são criados após as validações aplicáveis passarem.
