# Visão arquitetural inicial

## Visão do sistema

O GenSW é um ERP agropecuário modular. Sua primeira vertical funcional é a produção e o melhoramento animal; a arquitetura deve atender aves, codornas e outras espécies sem criar dependências conceituais prematuras.

## Módulos previstos

Os módulos previstos são: Identity, People, Properties, AnimalProduction, Reproduction, Genetics, AgriculturalProduction, Inventory, Purchasing, Sales, Financial, Accounting, Fiscal, Reporting e BI.

Eles representam direcionamento de produto, não um esquema de dados pré-criado. Nesta etapa não existem entidades, tabelas ou regras de negócio para esses módulos.

## Limites do MVP inicial

O MVP futuro abrangerá autenticação, pessoa, usuário, animal, espécie, raça, cruzamento e pedigree. Animais poderão possuir referências recursivas de pai e mãe; cruzamentos planejados e realizados serão entidades distintas. Esses recursos não são implementados pela fundação técnica.

## Camadas e dependências

```text
GenSW.Domain          <- regras e modelos de domínio, sem infraestrutura
GenSW.Application     -> GenSW.Domain; casos de uso, contratos e DTOs
GenSW.Infrastructure  -> GenSW.Application + GenSW.Domain; EF Core, PostgreSQL e integrações
GenSW.API             -> GenSW.Application + GenSW.Infrastructure; HTTP, DI e configuração
GenSW.Web             -> API HTTP; interface React independente
```

`Domain` não depende das demais camadas. `Application` não depende de ASP.NET Core, Entity Framework ou PostgreSQL. A `API` é o ponto de composição da aplicação.

## Persistência e configuração

O provedor padrão é PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`. A connection string deve ser fornecida por `ConnectionStrings__GenSW`, User Secrets ou configuração de ambiente não versionada. O `appsettings.json` não contém credenciais.

## Estratégia modular

Novos módulos começam pequenos, com uma Evolução no Redmine e Tarefas rastreáveis. Criar uma nova entidade, tabela, endpoint ou tela exige uma necessidade concreta do módulo. Abstrações compartilhadas só são introduzidas quando houver uso real em mais de um ponto.

## Integração GitHub e Redmine

Para cada prompt técnico: identificar ou criar a Evolução, criar uma Tarefa filha, registrá-la em andamento, executar validações e atualizar a Tarefa com evidências. O fechamento depende do status real das validações e da necessidade de inspeção humana; quando houver essa necessidade, usar `Em validação` em vez de `Concluído`.
