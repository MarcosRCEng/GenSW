# Financeiro: caixa realizado e fechamento

Evolução Redmine #376; execução #383; entregas FIN-01 #378 a FIN-05 #382. Design aprovado pelo usuário em #377: `docs/superpowers/specs/2026-10-02-financeiro-caixa-design.md`, preservado no commit `9fcf789`. Esta entrega requer revisão e homologação humana; não envolve merge ou produção.

## Operação

No início do sistema, abra **Financeiro · Fluxo de caixa**. Um Admin informa o primeiro dia do mês inicial e o saldo existente, inclusive zero ou negativo. Não se gera receita de abertura nem se reconstrói movimento anterior ao início. A configuração pode ser corrigida com sua versão antes do primeiro lançamento/fechamento; depois fica imutável.

Registre apenas dinheiro efetivamente recebido ou pago. Tipo determina entrada/saída e o valor informado é positivo. Data efetiva é preservada, não pode preceder o início nem superar a data operacional em `America/Sao_Paulo`. Pagamento em cartão representa o desembolso/recebimento ocorrido, sem controlar parcelas ou obrigações futuras.

Categorias possuem natureza imutável. As sete categorias iniciais são criadas pela migration, sem gerar saldo ou movimentos. Nomes podem ser alterados e categorias podem ser inativadas. `VENDA_ANIMAIS` é o código estável da categoria de venda; vincular Animal é opcional e só é oferecido nessa receita. Pessoa e Animal são pesquisados por nome/código; nenhum UUID precisa ser digitado. Registrar venda não muda Animal, status, filiação ou histórico produtivo.

Os indicadores consideram todos os lançamentos efetivos do mês. Pesquisa, período, categoria, natureza, situação e paginação restringem apenas a lista. Cancelados continuam consultáveis e deixam de compor saldos. O resultado é movimento de caixa, não lucro contábil.

Em mês aberto, **Corrigir** e **Cancelar** exigem motivo e versão esperada. A auditoria preserva antes/depois, referências, autor e UTC; não há exclusão física nem reversão de cancelamento. Referências inativadas depois do registro permanecem consultáveis e podem ser mantidas em correções de outros campos. Novos vínculos precisam estar ativos.

## Fechamento e ajustes

Somente Admin fecha mês civil já encerrado. O fechamento precisa seguir a sequência desde o início do controle, incluindo meses sem movimentos. A tela mostra prévia, observação obrigatória, confirmação explícita e saldo conferido opcional. A diferença de conferência é informativa; não gera ajuste automático.

O servidor recalcula os totais sob transação, guarda um snapshot único e torna o mês imutável. Não existe reabertura. Criação, correção, cancelamento e mudança de data envolvendo mês fechado retornam conflito.

**Ajustar** um registro fechado permite:

- Reversão compensatória no mês atual, com valor original e tipo oposto.
- Reversão e substituição correta, atômicas, com data operacional atual.
- Anotação de descrição/referências, sem modificar o original ou movimentar dinheiro.

Motivo e referência ao original são preservados. A prévia mostra os lançamentos e o impacto líquido. Cada original admite uma reversão, garantida por índice parcial único. A reversão conserva a categoria original como exceção explícita à compatibilidade de natureza. Ajustes não recebem edição/cancelamento direto; sua correção usa outro ajuste referenciando o anterior. Anotações ficam no histórico. Snapshots e totais dos meses fechados não são recalculados por ajustes posteriores.

## Contrato HTTP

Todas as rotas abaixo ficam sob `/api/v1/financeiro` e exigem JWT. Configuração e fechamento exigem `Admin` na API.

| Rota | Uso |
| --- | --- |
| `GET/POST configuracao` | Consultar/inicializar ou corrigir configuração ainda sem uso |
| `GET/POST categorias` | Consultar/criar categorias |
| `PUT categorias/{id}`; `PATCH categorias/{id}/ativo` | Nome e atividade, com versão; corpo `{nome, ativa, versaoEsperada}` |
| `GET/POST lancamentos` | Lista paginada/criação |
| `GET/PUT lancamentos/{id}` | Consulta/correção |
| `POST lancamentos/{id}/cancelamento` | Cancelamento auditável |
| `GET lancamentos/{id}/historico` | Auditoria completa |
| `POST lancamentos/{id}/ajuste` | Reversão/substituição/anotação |
| `GET meses/{ano}/{mes}` | Resumo independente dos filtros da lista |
| `GET/POST meses/{ano}/{mes}/fechamento` | Snapshot/fechamento |
| `GET fechamentos` | Histórico dos snapshots |

Dinheiro é retornado em strings decimais exatas, sem separadores de milhares. Requisições aceitam strings ou números JSON decimais. O limite é `9999999999999999.99`, com até duas casas; precisão excessiva é rejeitada, inclusive zeros adicionais. Agregações, saldo de abertura/final e conferência respeitam o limite. Backend calcula com `decimal`; frontend usa centavos em `BigInt` para formatação e prévia, sem ser autoridade do saldo.

Configuração exige `dataInicio` e `saldoInicial` explícitos. Lançamento recebe `tipo` (1 receita/2 despesa), `dataMovimento`, `valor`, `descricao`, `categoriaId`, `formaPagamento` (1 dinheiro/2 Pix/3 transferência/4 cartão/5 outro), `pessoaId?`, `animalId?` e `observacao?`.

Correção recebe `{lancamento, versaoEsperada, motivo}`; cancelamento, `{versaoEsperada, motivo}`; ajuste, `{versaoEsperada, motivo, somenteAnotacao, substituto?}`; fechamento, `{versaoEsperada, observacao, saldoConferido?}`. A versão de fechamento é a do resumo, não a de um lançamento. O histórico é append-only pelos casos de uso; snapshots não têm operação de alteração.

Criação e ajuste exigem `Idempotency-Key` (até 100 caracteres). A unicidade é por autor e operação; ajustes incluem o original no escopo. Mesma chave/payload retorna a resposta original persistida; payload diferente retorna 409. Mesmo após correção/cancelamento, o reenvio não cria outro registro. Consulte o GET para obter o estado/versão atual antes de editar uma resposta reproduzida. A interface preserva a chave no retry e bloqueia envio simultâneo.

Lista: `page=1`, `pageSize=25` (máximo 100), `ano/mes` juntos, `de/ate`, `tipo`, `categoriaId`, `search` e `cancelado`. Ordenação é data efetiva decrescente, criação decrescente e ID como desempate. Resposta contém `items`, `totalItems`, `page`, `pageSize`, `totalPages`; cada item traz lançamento e nomes das referências, incluindo inativas.

Status: 201 criação; 200 consulta/mutação; 400 dados inválidos; 401 sem JWT; 403 sem Admin; 404 inexistente; 409 versão, mês fechado, sequência, idempotência, unicidade ou timeout de lock. Erros financeiros são `ProblemDetails`, com código `dados_invalidos`, `nao_encontrado` ou `conflito_caixa` quando aplicável. Não há retry automático para conflitos: recarregue e confira os dados.

## Persistência e concorrência

Camadas: regras/entidades em Domain, contratos e serviço em Application, repositório financeiro específico em Infrastructure, HTTP/autorização na API. Migration `20261002222623_AddFinancialCash` cria apenas tabelas, FKs/índices/constraints financeiros e categorias. Não faz backfill financeiro nem altera tabelas/históricos dos outros módulos.

Todas as mutações adquirem `pg_advisory_xact_lock(37620261002)` antes de ler estado mutável. Ordem: lock do financeiro → Pessoa `FOR SHARE` → Animal `FOR SHARE`. Esses locks de referência também coordenam com alterações de atividade dos módulos existentes. Timeout é cinco segundos, convertido em 409. Serialização global é deliberada para o volume do MVP.

Versões são tokens de concorrência no EF e são conferidas sob lock. Lançamento, auditoria, versões mensais, idempotência e fechamento são publicados atomicamente. Antes do commit, saldos dos meses ocupados são conferidos; overflow reverte toda a transação. Consultas com múltiplas leituras (lista, resumo e histórico) usam `REPEATABLE READ`. Não se fecha usando totais do cliente.

Saldo de abertura = saldo configurado + receitas anteriores − despesas anteriores. Saldo final = abertura + receitas do mês − despesas do mês. Mês vazio mantém o saldo. Saldo negativo é permitido e destacado.

## Validação e homologação

Os resultados, comandos, screenshots e limitações desta execução são registrados em `docs/operations/financeiro-validacao-383.md` e nas tarefas Redmine. Para reproduzir checks normais: `dotnet test GenSW.sln`, `npm test -- --run`, `npm run lint`, `npm run build`, build backend e `git diff --check`. Testes financeiros de infraestrutura e HTTP usam PostgreSQL real pelo helper existente, nunca substituição por SQLite/in-memory para concorrência.

Homologue configuração, receita/despesa, correção, cancelamento, histórico, filtros, fechamento vazio/com movimentos, ajuste, anotação, Admin/usuário comum e navegação por teclado em desktop e celular. A instância isolada de demonstração contém dados de teste; não é publicação em produção.
