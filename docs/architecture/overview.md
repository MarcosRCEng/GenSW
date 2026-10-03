# Visão arquitetural inicial

## Visão do sistema

O GenSW é um ERP agropecuário modular. Sua primeira vertical funcional é a produção e o melhoramento animal; a arquitetura deve atender aves, codornas e outras espécies sem criar dependências conceituais prematuras.

## Módulos previstos

Os módulos previstos são: Identity, People, Properties, AnimalProduction, Reproduction, Genetics, AgriculturalProduction, Inventory, Purchasing, Sales, Financial, Accounting, Fiscal, Reporting e BI.

Eles representam direcionamento de produto, não um esquema de dados pré-criado. Cada módulo evolui conforme necessidades concretas; sua presença no roadmap ou no menu não autoriza criar antecipadamente entidades, tabelas ou regras de negócio.

## Limites do MVP inicial

A evolução inicial abrange autenticação, pessoas, animais, taxonomia, cruzamentos e pedigree. As verticais existentes também oferecem ciclos reprodutivos, proles e fluxo de caixa. O acesso a essas funcionalidades deve preservar as rotas protegidas e os limites dos domínios existentes; a organização do menu não altera contratos, persistência ou regras de negócio.

## Organização da navegação autenticada

A página inicial organiza o ERP por finalidade de uso, em seções semânticas com cards de módulos. Esse agrupamento é de apresentação: as features verticais existentes continuam responsáveis por suas telas e serviços.

| Grupo | Disponível | Planejado |
| --- | --- | --- |
| Cadastros básicos | Pessoas (`/pessoas`); Taxonomia: Espécies (`/especies`), Raças (`/racas`) e Variedades (`/variedades`); Animais (`/animais`) | Produtos; Propriedades |
| Produção e operações | Reprodução: Cruzamentos (`/cruzamentos`), Ciclos reprodutivos (`/ciclos-reprodutivos`) e Proles (`/proles`) | Produção (agrícola); Produção animal; Genética; Estoque |
| Processos gerenciais | Financeiro: Fluxo de caixa (`/financeiro`) | Compras; Vendas; Fiscal; Contábil; Relatórios; BI |

Produção (agrícola), Produção animal e Genética permanecem módulos próprios no roadmap. Os recursos atuais de produção de ovos, filiação e pedigree continuam acessíveis por Animais; sua existência dentro dessa vertical não torna disponíveis os módulos futuros completos. A área Administração só deve ser exibida quando houver uma rota administrativa implementada. Identity permanece presente na autenticação e nas ações de sessão, incluindo Sair.

A configuração de navegação fica em `src/Frontend/GenSW.Web/src/features/auth/navigation` e discrimina os estados disponível (`available`) e planejado (`planned`). Um módulo disponível possui links para destinos reais; um módulo planejado possui descrição e indicação textual de estado, sem destino de navegação. A home consome essa configuração sem criar páginas, rotas ou endpoints para preencher o roadmap.

Para adicionar ou promover um item do menu:

1. Verificar a feature existente e sua rota em `AppRoutes.tsx`. Uma funcionalidade disponível deve possuir uma tela real dentro da proteção de sessão.
2. Atualizar a configuração com grupo, título, descrição, estado e, somente quando disponível, os destinos implementados. Reutilizar as rotas reais; não usar links vazios, `#` ou redirecionamentos genéricos como destinos de módulos planejados.
3. Preservar a hierarquia de títulos e os nomes acessíveis das seções de navegação. Links devem ter rótulos claros e foco visível por teclado; cards planejados são conteúdo estático e identificam seu estado por texto, sem depender apenas de cor.
4. Validar que cada link leva ao fluxo existente, que módulos planejados não oferecem navegação e que a apresentação funciona em telas estreitas. A promoção de um módulo segue sua própria Evolução e Tarefas, sem antecipar implementação de backend nesta configuração.

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
