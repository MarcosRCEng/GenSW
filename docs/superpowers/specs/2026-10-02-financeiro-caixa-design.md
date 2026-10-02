# Financeiro — fluxo de caixa essencial e fechamento mensal

Data: 2026-10-02. Evolução Redmine #376; planejamento #377.
Estado: proposta para revisão humana; implementação ainda não autorizada por este documento.

## 1. Objetivo e limites

Entregar controle operacional dos valores efetivamente recebidos e pagos, com visão mensal e fechamento auditável. Exemplos: compra de ração, medicamentos e materiais; receita de venda de animais. O resultado mensal representa movimento de caixa e não lucro contábil.

Proposta inicial: um único caixa lógico em BRL para a operação atual. Dinheiro, Pix, transferência e cartão são formas de pagamento informativas, não contas com saldos separados. No cartão, registrar o desembolso/recebimento efetivo, sem antecipar parcelas ou valores futuros. Recebimentos parciais são lançamentos separados; não há controle de obrigação ou quitação de venda.

Fora do MVP: partidas dobradas, plano de contas contábil, livros fiscais, impostos, notas fiscais, estoque, pedidos de compra/venda, contas a pagar/receber, competência, parcelas, conciliação/importação bancária, transferência entre contas, múltiplas moedas, automações e alteração automática do Animal por venda. Não criar entidades vazias desses módulos.

## 2. Decisões propostas e confirmação

Confirmado pelo usuário: um único caixa em BRL, somente valores realizados e mês fechado imutável, com ajustes no mês atual. Não haverá reabertura. Se futuramente forem necessárias contas a pagar/receber ou múltiplos caixas, revisar o design: a ampliação modifica saldo, modelo e fechamento.

Demais escolhas rotineiras: saldo inicial obrigatório (zero permitido), categorias gerenciáveis, Pessoa e Animal opcionais, lançamentos em meses abertos corrigíveis com auditoria, sem exclusão física. Usuários autenticados consultam e registram; configuração inicial, fechamento exige papel Admin já existente, verificado na API. Uma política financeira mais restrita é evolução posterior, sem refazer Identity neste MVP.

## 3. Experiência operacional

Menu Financeiro → Fluxo de caixa: seletor de mês, estado Aberto/Fechado, saldo inicial do mês, receitas, despesas, resultado e saldo final. Lista paginada com data, descrição, categoria, tipo, valor, forma de pagamento, Pessoa/Animal quando presentes e estado. Filtros por período, tipo, categoria, texto e situação. Cancelados ficam consultáveis e não entram nos totais. Totais consideram todos os lançamentos efetivos do período, independentemente da paginação; filtros da lista não mudam silenciosamente os indicadores do mês.

Formulário: Receita/Despesa, data efetiva, valor em reais, descrição, categoria compatível, forma de pagamento, Pessoa opcional, Animal opcional apenas para Receita/Venda de animais e observação. Seleção pesquisável de Pessoa e Animal, nunca UUID digitado como mecanismo principal. Valor informado é positivo; o tipo determina entrada/saída. O usuário confirma antes de salvar. Botão desabilitado durante envio e chave de idempotência preservada na tentativa, evitando duplicidade por duplo clique/reenvio.

Categorias iniciais: despesas com Insumos, Alimentação, Saúde animal, Serviços e Outras despesas; receitas com Venda de animais e Outras receitas. A descrição distingue materiais individuais; não cadastrar catálogo de produtos. Categoria possui natureza Receita/Despesa e código semântico estável para Venda de animais. O nome é editável, mas a natureza e o código não; inativação preserva uso histórico. Seed idempotente somente de categorias, sem gerar lançamentos ou saldo.

Fechamento: prévia com totais e saldo; confirmação explícita, motivo/observação e, opcionalmente, saldo conferido. Mostrar diferença entre saldo calculado e conferido, sem criar ajuste automático. Histórico dos fechamentos sempre consultável. Ajustar lançamento fechado exige motivo obrigatório, referência ao original e lançamento compensatório no mês atual aberto. Mostrar o efeito antes da confirmação. Formulários rotulados, foco/teclado, avisos acessíveis e estados vazios/erro; BRL e datas em pt-BR. Telas funcionam em celular.

## 4. Modelo mínimo

- ConfiguracaoCaixa: singleton, DataInicio (primeiro dia do mês), SaldoInicial decimal(18,2), versão, autor e timestamps UTC. DataInicio marca o início do controle; não reconstruir movimentos anteriores. Saldo inicial pode ser negativo. Após primeiro lançamento/fechamento, configuração imutável; correção posterior via lançamento explícito em mês aberto, sem alterar o histórico silenciosamente.
- CategoriaFinanceira: Guid, Nome normalizado único por natureza, Natureza, Codigo opcional estável, Ativa, versão e timestamps.
- LancamentoCaixa: Guid, Tipo, DataMovimento DateOnly, Valor decimal(18,2), Descricao (1–200), CategoriaId, FormaPagamento (Dinheiro/Pix/Transferencia/Cartao/Outro), PessoaId?, AnimalId?, Observacao? (até 2000), estado Efetivo/Cancelado, versão, autor e timestamps. Chave única de idempotência por autor/operação de criação. Origem Ordinario/AjusteReversao/AjusteSubstituicao, LancamentoOriginalId? e MotivoAjuste? para compensações explícitas; uma reversão por original reforçada por índice único parcial.
- AuditoriaLancamento: append-only com lançamento, operação Criado/Corrigido/Cancelado, valores anteriores/novos dos campos financeiros e referências, motivo, autor e timestamp. Não registrar tokens ou credenciais. Correção e cancelamento exigem motivo; cancelamento não pode ser revertido nesta etapa.
- MesCaixa: primeiro dia do mês como chave única, estado Aberto/Fechado e versão; criado sob demanda, inclusive mês sem movimentos.
- FechamentoCaixa: Guid, mês, saldo de abertura, receitas, despesas, saldo final, contagens, saldo conferido?, observação, autor/data. Snapshot imutável. Um mês tem um único fechamento imutável; não existem revisão substitutiva ou reabertura.

Os valores de lançamentos têm duas casas decimais, são maiores que zero e não são armazenados em float. Rejeitar precisão excessiva em vez de arredondar silenciosamente. Respeitar limites decimal(18,2), inclusive agregação e fechamento. Cálculos usam decimal no backend; frontend não é autoridade de saldo.

DataMovimento não pode anteceder DataInicio nem ser futura em relação à data operacional America/Sao_Paulo, obtida por TimeProvider e conversão explícita. Auditoria usa UTC e não muda a data informada do movimento. Data inicial não pode ser futura.

## 5. Integração e histórico

Pessoa pode representar fornecedor/comprador sem criar novos papéis comerciais. Novos vínculos oferecem Pessoas e Animais ativos. Animal referenciado precisa existir, mas não é obrigatório para receita de venda: suporta lançamento consolidado ou histórico com referência livre na descrição. Não há escolha automática do valor a partir de Animal.

Inativação posterior de Pessoa, Animal ou categoria não altera lançamentos nem saldos; referências existentes continuam consultáveis. Correção de campos não relacionados permite preservar referências inativas já existentes. Trocar a categoria ou tipo deve manter compatibilidade; vínculo Animal exige categoria semântica Venda de animais. FKs restritivas preservam os registros. Nenhuma alteração de Filiação, Proles, Ciclos, Cruzamentos, pesagens, produção de ovos ou status de Animal.

## 6. Saldo, fechamento e concorrência

Saldo de abertura = saldo inicial configurado + receitas efetivas anteriores ao mês − despesas efetivas anteriores ao mês. Saldo final = abertura + receitas do mês − despesas do mês. Resultado = receitas − despesas. Mês sem movimentos conserva saldo. Não exigir saldo não negativo; evidenciar saldo negativo no painel.

Fechar somente mês civil já encerrado na data operacional, em sequência a partir de DataInicio; meses sem movimentos também podem ser fechados. Fechamento recalcula os totais no servidor dentro da transação. Depois de fechado, rejeitar criação, correção e cancelamento no mês por UI e API. Alterar data de um lançamento verifica tanto mês de origem quanto destino.

Mês fechado nunca é reaberto. Correção de valor/tipo usa operação explícita de ajuste no mês atual aberto: reversão compensatória do lançamento original (mesmo valor e natureza oposta) e, quando necessário, lançamento substituto correto, criados atomicamente com motivo e referências auditáveis. Reversão pode representar correção de registro, sem movimento bancário novo; os relatórios a identificam como ajuste. Para corrigir somente descrição/referências, registrar anotação auditável vinculada ao original, sem alterar seu conteúdo ou movimentar saldo. Cada original pode ser revertido uma única vez, com unicidade no banco; substituto admite futura correção pelo mesmo mecanismo. Não cancelar/editar reversão diretamente: nova correção referencia o ajuste e explicita o efeito. A confirmação exibe os lançamentos e o impacto líquido. Saldo futuro incorpora o ajuste; totais e snapshots de meses fechados permanecem intactos. Ajuste excepcional não exige categoria de natureza invertida: preserva a categoria original e a consulta o identifica separadamente, sem violar a regra dos lançamentos ordinários.

Todas as mutações financeiras (configuração, categorias, lançamentos, fechamento/ajuste) adquirem o mesmo lock transacional PostgreSQL específico do módulo antes de ler/validar/gravar. Para o volume do MVP, serialização global é aceitável e evita corrida entre novo lançamento e fechamento, bem como entre categoria/vínculo e lançamento. Ordem de locks documentada, timeout limitado e resposta de conflito para retry explícito. Leituras de lista/resumo/histórico coerentes usam snapshot transacional; fechamento não usa totais fornecidos pelo cliente.

Versão esperada obrigatória em correção/cancelamento/fechamento/ajuste para evitar perda de atualização; divergência retorna 409. Idempotência de criação persistida atomicamente: mesma chave e mesmo payload retorna lançamento original; mesma chave com payload diferente retorna 409, inclusive lançamento posteriormente corrigido/cancelado. Fechamento simultâneo não cria dois fechamentos para o mesmo mês. Auditoria, lançamento e fechamento são gravados atomicamente. Não usar somente validações em memória para exclusividade.

## 7. API e arquitetura

Seguir Domain → Application → Infrastructure → API, repositórios específicos, EF Core/PostgreSQL, DI, JWT existente e ProblemDetails. Proposta de rotas sob /api/v1/financeiro:

- GET/POST /configuracao (POST inicial, somente Admin).
- GET/POST /categorias; PUT /categorias/{id}; PATCH /categorias/{id}/ativo.
- GET/POST /lancamentos; GET/PUT /lancamentos/{id}; POST /lancamentos/{id}/cancelamento; GET /lancamentos/{id}/historico.
- POST /lancamentos/{id}/ajuste (reversão/substituição ou anotação, idempotente).
- GET /meses/{ano}/{mes} (resumo); GET /meses/{ano}/{mes}/fechamento (snapshot e auditoria).
- POST /meses/{ano}/{mes}/fechamento (Admin). Sem endpoint de reabertura.

Usar paginação padrão 25 e máximo 100, filtros e ordenação defensivos. Criação 201; leitura/mutação 200; dados inválidos 400; não autenticado 401; sem papel exigido 403; inexistente 404; conflito de versão, mês fechado, sequência ou idempotência 409. API retorna totais monetários exatos, sem conversão a float para decisões de domínio. Não criar exportação, anexos ou dashboards externos nesta etapa.

## 8. Sequência de entrega

1. FIN-01 — Base do caixa, categorias e saldo inicial: entidades, configuração, migrations aditivas e contratos. Suporte de testes/documentação desde o início.
2. FIN-02 — Lançamentos e resumo: receitas/despesas, referências pesquisáveis, correção/cancelamento, histórico, idempotência, versão e cálculos.
3. FIN-03 — Fechamento mensal imutável e ajustes: snapshots, sequência, compensação no mês atual, proteção transacional, autorização e auditoria. Depende de FIN-02.
4. FIN-04 — Frontend financeiro: navegação, categorias/configuração, lançamentos, indicadores, fechamento/histórico e acessibilidade. Integra incrementalmente FIN-01–03.
5. FIN-05 — Validação integrada, migrations e documentação: acompanha todas as etapas; consolida evidências, testes de concorrência e homologação.

Uma única branch codex/ de implementação poderá entregar a vertical coerente, com commits rastreáveis por tarefa; preparar a partir da main atual no início da execução. Tarefas planejadas não autorizam implementação antecipada. Não fazer merge ou publicação sem autorização explícita.

## 9. Critérios de aceite e validação

- Registrar despesa de ração e receita de venda; Pessoa/Animal opcionais; seleção pesquisável; totais exatos; datas/valor/natureza inválidos rejeitados.
- Exemplo: saldo inicial R$ 100,00, receita R$ 250,00, despesa R$ 80,00 → resultado R$ 170,00 e saldo final R$ 270,00; mês seguinte abre em R$ 270,00. Cancelar despesa em mês aberto altera saldo para R$ 350,00 e mantém auditoria.
- Correção em mês aberto mantém versões anteriores; referências inativadas preservadas; sem exclusão física. Configuração inicial não gera receita artificial.
- Fechar mês vazio e com movimentos; bloquear mês atual/futuro, salto na sequência e mutação de fechado; ausência de reabertura; ajuste no mês atual com motivo, reversão/substituição e histórico, sem mudar snapshot fechado.
- PostgreSQL real: migrations up em base existente e nova sem backfill financeiro; FK/índices/decimal; lançamento simultâneo ao fechamento; duas correções com mesma versão; ajustes compensatórios simultâneos; idempotência concorrente; rollback de auditoria e snapshot; mover entre meses com fechamento concorrente. Confirmar ausência de gravação depois do fechamento.
- Domain/Application: saldo negativo, limites/precisão, meses/ano bissexto, data operacional, cancelamento, referências e sequência. API: JWT, 403 de não Admin, ProblemDetails, paginação e conflito. Frontend: envio repetido, filtros/totais, campos, erros, teclado e telas vazias.
- Executar dotnet test GenSW.sln, npm test -- --run, npm run lint, npm run build, build backend e git diff --check. Testes de concorrência não substituíveis por SQLite/in-memory.
- Homologação visual/funcional em desktop e celular: configuração, lançamento, correção/cancelamento, consulta, fechamento/ajuste e papel não Admin. Registrar testes reais e limitações. Redmine Em validação enquanto faltar aceite; sem produção nesta entrega.

## 10. Referências inspecionadas

docs/architecture/overview.md; design NA-05; PessoasController (JWT, paginação, ProblemDetails); ApplicationUser/JwtAccessTokenService/InitialAdminBootstrapper (identidade e papel Admin); AppRoutes (rotas protegidas); ProleRepository (transação e locks); suítes existentes de infraestrutura. Módulo Financial está previsto na arquitetura e ainda não possui modelo operacional próprio. Esta proposta não redefine os demais módulos.
