# Catálogo e formulação — MVP-1 da Evolução #413

O MVP oferece catálogo de materiais, perfis nutricionais documentais, receitas versionadas e simulações manuais. As telas autenticadas estão em `/itens`, `/producao/receitas` e `/producao/formulacao`. Estoque, lotes físicos, ordens, reservas, vínculo com ovos, custos, preços e otimização automática permanecem adiados.

## Uso e histórico

1. Cadastre uma identidade única por material. As capacidades de entrada, produção, uso interno e venda são cumulativas; a classe distingue alimento, outro material incorporado, embalagem e consumível. Categorias e itens podem ser inativados sem apagar histórico.
2. Escolha a unidade canônica (`kg`, `L` ou `un`). `g/kg` e `mL/L` são conversões exatas de unidade; volume ou contagem para massa exige conversão documental do próprio item, com fator, fonte, método, data, contexto, amostra e proveniência. Uma publicação de conversão, perfil ou uso em receita fixa a unidade canônica.
3. Registre um perfil em rascunho. Publicação exige identificação da fonte, método, preparação, amostra, aplicabilidade e observações. Datas, espécie e fase podem ser informadas; ausência de espécie não comprova adequação. Cada observação possui componente, base BN/MS, unidade, método, contexto e estado. `Conhecido` admite zero explícito e exige origem; `Desconhecido` e `NaoAplicavel` exigem motivo e não têm valor numérico. Estimativa exige hipótese. Faixa, mínimo, máximo e abaixo de detecção são registros documentais, sem cálculo escalar.
4. Crie o cabeçalho de receita e um rascunho de versão. Selecione itens, perfis e conversões explicitamente. Registre quantidades ou percentuais incorporados (soma exata de 100), saídas com uma principal, perdas, etapas e escala fixa/variável. Embalagens e consumíveis continuam em quantidades absolutas. A publicação mantém referências exatas; alterações posteriores exigem outro rascunho.
5. Use a prévia de escala para revisar as linhas antes da simulação. Fração de `un` e resultados que excedam seis casas de entrada são recusados, sem arredondamento silencioso. Mistura simples exige conservação da massa e saída única, sem perdas ou retenções.
6. Simule uma versão publicada, informando tamanho, contexto e metas com origem explícita. Uma variação manual fica no snapshot; ela não edita a receita publicada. O resultado pode gerar novo rascunho. Compare de 2 a 10 snapshots em BN ou MS.

Perfis e versões publicados são imutáveis: permitem inativação, preservando o conteúdo. Inativos continuam consultáveis por ID e bloqueiam novo uso. Snapshots e comparações são imutáveis e mantêm conteúdo histórico mesmo após renomear ou inativar referências. A UI separa Visualizar e Editar, preserva filtros/página no retorno e oferece busca/paginação no servidor nos seletores; uma referência histórica selecionada permanece visível fora da página atual.

## Semântica do cálculo

O motor `gensw-formulation/1.0` usa `decimal` no backend. Quantidades, concentrações e fatores atravessam os contratos novos como strings invariantes, sem cálculos de negócio por ponto flutuante no navegador. Entradas aceitam até 14 casas inteiras e seis decimais; resultados derivados preservam a precisão decimal disponível. As definições semânticas dos componentes têm versão 1 e não fornecem valores nutricionais de catálogo.

Massa BN é a soma de todos os materiais incorporados, inclusive os de composição desconhecida. Embalagem e consumível não entram no denominador. Concentração BN é a soma das contribuições dividida por massa BN; concentração MS usa massa seca total conhecida e positiva. Umidade e MS são complementares dentro da tolerância documental de 0,001 g/kg. Sem massa/conversão ou MS aplicável, os resultados correspondentes ficam indeterminados. BN calculável é preservada quando falta MS.

PB, gordura e extrato etéreo, FB/FDN/FDA, modalidades energéticas e métodos/contextos são distintos. Energia apenas converte a unidade da mesma modalidade (`1 kcal = 0,004184 MJ`), com espécie/fase/contexto compatíveis; não converte EB em ED/EM/EL. Origem estimada de nutriente, conversão ou MS usada no cálculo propaga a indicação de estimativa e a política das metas. Um perfil genérico selecionado para uma espécie gera aviso de aplicabilidade não comprovada.

Dado ausente não vira zero. O motor registra contribuição conhecida, cobertura por massa, motivos por linha, total indeterminado e meta indeterminada. Metas completas recebem `AtendidaNosDadosDisponiveis` ou `NaoAtendida`; isso não certifica dieta ou processo seguro. Limites de inclusão são verificados e aparecem em problemas do snapshot. Uma prova convexa local pode identificar mínimo acima do máximo dos ingredientes escolhidos ou do limite sob inclusões declaradas; não há solver nem afirmação de inviabilidade global.

Sub-receita exige versão publicada e saída explícita. A expansão preserva versões e caminhos e não conta simultaneamente intermediário e ancestrais. Um perfil escolhido explicitamente para o intermediário usa sua composição. Ciclos pela mesma `ReceitaId`, inclusive por outra versão, são rejeitados. Limites: profundidade de 10 e 500 linhas expandidas.

Processamento não presume conservação de nutrientes a partir do rendimento. Usa perfil explícito da saída principal, ou estimativa documental por retenção de componentes somente em saída única. MS exige retenção própria ou perfil; perda de água não é inferida automaticamente. Sem dados da saída, a nutrição é indeterminada. Saídas múltiplas permitem perfil explícito da principal, com aviso sobre a distribuição não modelada. Rendimento e balanço são documentais; balanço não zero exige revisão do usuário.

## Contrato HTTP

Todas as rotas abaixo usam `/api/v1` e autenticação existente. Erros usam ProblemDetails: 400 validação, 401 sem sessão, 404 inexistente, 409 revisão obsoleta, referência inativa, duplicidade ou idempotência divergente. A política de autorização atual exige sessão, seguindo os catálogos existentes; não foi introduzido ACL específico.

| Recurso | Operações |
| --- | --- |
| `/itens` | GET paginado, POST; `/{id}` GET/PUT; `/{id}/ativo` PATCH; `/{id}/historico` GET |
| `/categorias-itens` | GET/POST; `/{id}` PUT de nome/status |
| `/itens/{id}/conversoes` | GET paginado, POST de versão documental imutável |
| `/componentes-nutricionais` | GET de definições sem valores nutricionais |
| `/itens/{id}/perfis-nutricionais` | GET paginado, POST de rascunho |
| `/perfis-nutricionais/{id}` | GET/PUT; `/publicacao` e `/inativacao` POST |
| `/receitas` | GET paginado, POST; `/{id}` GET/PUT; `/{id}/ativo` PATCH; `/{id}/historico` GET |
| `/receitas/{id}/versoes` | GET paginado, POST; `/receitas/versoes` GET paginado para seleção |
| `/receitas/versoes/{id}` | GET/PUT; `/publicacao` e `/inativacao` POST; `/escalonamento?tamanho=200&unidade=kg` GET |
| `/simulacoes-formulacao` | GET paginado, POST; `/{id}` GET de snapshot |
| `/comparacoes-formulacao` | GET paginado, POST; `/{id}` GET de comparação congelada |

Listas retornam `items`, `page`, `pageSize`, `totalItems`, `totalPages`. `pageSize` é 1–100; ordenação permitida: `nome`, `codigo`, `createdAtUtc` e direção `asc`/`desc`, com desempate estável por ID. Filtros incluem `search`, `ativo`, `classe`, `categoriaId`, `capacidade` e `estado` conforme o recurso. A busca trata `%` e `_` literalmente.

Edições/status/publicações exigem `versaoEsperada`. Os POST de simulação e comparação exigem `Idempotency-Key` (até 100 caracteres), isolada por autor e tipo. Mesmo payload, inclusive grafias decimais equivalentes, retorna o mesmo snapshot; chave reutilizada com mudança retorna 409. Replay não revalida referências que foram inativadas depois do snapshot. A UI conserva a chave ao tentar novamente após falha de rede.

## Persistência, migração e recuperação

Migration aditiva `20261006220939_AddCatalogAndFormulation`: nove tabelas próprias (`CategoriasItens`, `Itens`, `ConversoesItens`, `PerfisNutricionais`, `Receitas`, `ReceitasVersoes`, `ReceitasReferencias`, `HistoricoFormulacao`, `SnapshotsFormulacao`). Conteúdos versionados são JSONB tipados e validados no domínio; referências publicadas possuem FKs restritivas. Índices únicos protegem códigos normalizados, numeração e idempotência. Checks e triggers protegem estados, precisão, unidade fixa e imutabilidade. As mutações do módulo usam transação com advisory lock `(413,421)` e timeout de 5 s; isso serializa o MVP entre instâncias, sem bloquear mutações de Animal ou Caixa. É uma decisão proporcional ao MVP, com concorrência de escrita limitada.

O [SQL idempotente para revisão](sql/2026-10-06-catalogo-formulacao-413.sql) foi gerado a partir da migration de Propriedades até esta migration. Não inclui migrations anteriores pendentes. Nenhuma migration foi aplicada à base compartilhada nesta entrega; a homologação compartilhada estava em `AddFinancialCash`. A cópia isolada recebeu também a migration prévia de Propriedades, já presente em `main`.

Antes de uma futura aplicação aprovada: conferir `__EFMigrationsHistory`, backup PostgreSQL e manifesto do volume privado de imagens, testar restore em ambiente isolado, revisar o SQL correspondente a todas as migrations pendentes e validar contagens/hashes das tabelas anteriores. Nunca apontar testes descartáveis para a base compartilhada. A API não migra automaticamente no startup.

Configuração externa: `ConnectionStrings__GenSW`, `Authentication__Jwt__SigningKey`, issuer/audience e origens CORS. Para preservar fotos, configurar explicitamente `Images__PrivateRoot` para o volume já provisionado, conforme [operação de imagens](animal-evolution.md); não há valor padrão. Não registrar segredo ou caminho privado em evidências públicas.

Reversão preferencial antes de merge/publicação: conservar a base compartilhada e descartar exclusivamente a cópia de validação. Após uma futura aplicação com dados, preferir correção aditiva ou restore coordenado do backup; `Down` remove os dados das nove tabelas novas e não é recuperação sem perda. Nenhum rollback destrutivo foi executado nesta entrega. Fixtures sintéticas estão apenas em testes/bases descartáveis, sem seed nutricional operacional.

## Validação reproduzível

Instale PostgreSQL com `initdb`, `pg_ctl`, `createdb`, `pg_dump` e `pg_restore`; indique o binário em `GENSW_TEST_POSTGRES_BIN`. Execute os gates definidos no CI:

```powershell
dotnet restore GenSW.sln
dotnet build GenSW.sln --configuration Release --no-restore
dotnet test GenSW.sln --configuration Release --no-build
cd src/Frontend/GenSW.Web
npm ci
npm test
npm run lint
npm run build
```

Quando uma API local estiver usando as DLLs, use `--artifacts-path` externo ao build em uso, ou reinicie somente a instância isolada. Veja [evidências e roteiro de revisão](../validation/2026-10-06-catalogo-formulacao-413.md).
