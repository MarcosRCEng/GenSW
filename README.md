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

## Execução local rápida

O launcher para Windows é o caminho recomendado para homologação visual/manual.
Na raiz do repositório, execute:

```text
Primeira vez:
setup-gensw.bat

Uso diário:
start-gensw.bat

Encerrar:
stop-gensw.bat
```

O setup valida os pré-requisitos sem instalar software do sistema, prepara o
certificado HTTPS, mantém connection string e chave JWT em .NET User Secrets,
executa `dotnet restore`, `dotnet tool restore`, `npm ci` e aplica as migrations.
Ele também oferece, de forma explícita, o bootstrap opcional do primeiro
administrador. A senha é lida sem eco e existe apenas no processo filho.

O start valida as três portas fixas antes de criar processos, confirma o
PostgreSQL existente, atualiza migrations, aguarda API e Web responderem e só
então abre o navegador em `https://localhost:7441/login`. O stop valida o PID,
horário de início e linha de comando registrados em `.gensw/run` antes de
encerrar somente as árvores iniciadas pelo launcher. PostgreSQL e outros
projetos nunca são encerrados.

Para validações automatizadas e execução sem navegador:

```powershell
.\setup-gensw.bat -CheckOnly
.\start-gensw.bat -CheckOnly
.\start-gensw.bat -NoBrowser
```

Portas locais fixas do GenSW:

- Web HTTPS: `7441`;
- API HTTP: `7442`;
- API HTTPS: `7443`;
- PostgreSQL existente: `5432`.

Se uma porta da aplicação estiver ocupada, o launcher falha informando a porta,
o PID e o processo; ele nunca seleciona outra porta automaticamente.

## Como executar

### Execução manual para desenvolvedores

Backend:

```powershell
dotnet restore GenSW.sln
dotnet run --project src/Backend/GenSW.API
```

Antes de iniciar a API, configure `ConnectionStrings:GenSW` e
`Authentication:Jwt:SigningKey` em User Secrets do projeto `GenSW.API` (o
`setup-gensw.bat` faz isso), ou use outro provedor externo seguro. A chave JWT
deve possuir pelo menos 256 bits e nunca deve ser adicionada aos arquivos
`appsettings`. A aplicação falha cedo quando uma configuração obrigatória de
autenticação ou persistência está ausente.

Issuer, audience e duração do access token são configurações não sensíveis em `Authentication:Jwt`. As origens CORS permitidas vêm de `Cors:AllowedOrigins`; em Development, a única origem preparada para o frontend é `https://localhost:7441`. O limite inicial de login vem de `RateLimiting:Login` e é de 10 tentativas por minuto por endereço remoto.

### Provisionamento inicial do administrador

O primeiro administrador é criado somente por uma execução administrativa explícita, nunca no startup da API e sem endpoint HTTP. Em uma base sem usuários, forneça externamente `ConnectionStrings__GenSW`, `InitialAdminBootstrap__Name`, `InitialAdminBootstrap__Username` e `InitialAdminBootstrap__Password`; então execute `dotnet run --project src/Backend/GenSW.AdminBootstrap`. A ferramenta recusa qualquer nova tentativa quando já existir um usuário e não exibe a senha.

O perfil local `https` publica a API em `https://localhost:7443` e mantém
`http://localhost:7442` apenas para redirecionamento. Prepare uma vez o
certificado de desenvolvimento com `dotnet dev-certs https --trust`; depois
execute `dotnet run --launch-profile https --project src/Backend/GenSW.API`.
Isso é necessário para o navegador reenviar o refresh cookie marcado como
`Secure`.

Frontend:

```powershell
cd src/Frontend/GenSW.Web
npm ci
npm run dev
```

O frontend de desenvolvimento é servido em `https://localhost:7441` e exige a
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

## Roadmap macro

Identity, People, Properties, AnimalProduction, Reproduction, Genetics, AgriculturalProduction, Inventory, Purchasing, Sales, Financial, Accounting, Fiscal, Reporting e BI evoluirão incrementalmente. O MVP inicial priorizará autenticação, pessoas, usuários, animais, espécie, raça, cruzamento e pedigree.

## Governança Git/Redmine

Cada demanda executada pelo Codex deve possuir uma **Tarefa** no Redmine, vinculada a uma **Evolução**. A Tarefa registra objetivo, escopo, critérios de aceite, validações, branch, commit, push e pendências. Commits só são criados após as validações aplicáveis passarem.
