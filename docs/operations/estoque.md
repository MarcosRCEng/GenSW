# Operação de estoque manual — F01 / Evolução #430

F01 registra materiais físicos de itens existentes, por lote e local. O cadastro de lote fixa a unidade do item e começa sem saldo. As telas em `/estoque` oferecem Saldos, Locais, Lotes, Movimentos e Reconciliação. Os comandos têm prévia e confirmação; a prévia mostra uma observação de disponibilidade, sem reservar material.

Implementação rastreada nas Tarefas [#434](https://devops-lab.tailaf9418.ts.net/issues/434) e [#435](https://devops-lab.tailaf9418.ts.net/issues/435). O contrato e os papéis foram aceitos na [#433](https://devops-lab.tailaf9418.ts.net/issues/433); o aceite funcional da entrega é uma etapa posterior. Consulte o [desenho](../superpowers/specs/2026-10-07-continuidade-f01-f06-430-design.md) e a [matriz T01–T16](../superpowers/plans/2026-10-07-continuidade-f01-f06-430-implementation.md).

## Fluxo de uso

1. Cadastre os locais reais, com código, nome e finalidade Ordinário ou Segregação. Propriedade é contexto opcional; não representa autorização ou depósito automático.
2. Cadastre o lote de um item existente, com código interno único, origem, fonte e responsável. Informe datas e validade quando conhecidas. Perfil nutricional e conversão documental são escolhas explícitas do mesmo item, com justificativa de aplicabilidade.
3. Para inventário inicial, Admin registra uma Abertura na posição lote/local ainda sem movimentos. Para recebimento operacional, registre Entrada, preservando a identidade de origem/fonte do lote. Origem diferente requer outro lote.
4. Solicite a prévia do comando, confira quantidade, unidade, saldo anterior/posterior, referências, avisos e revisões. Quando houver quantização, aceite explicitamente o valor e o resíduo com motivo.
5. Confirme o comando e consulte o evento imutável. Transferência aparece como um evento com débito e crédito iguais. Correções posteriores usam contagem presente, motivo/evidência e referência ao evento anterior quando pertinente.

## Papéis e comandos

Todas as consultas exigem sessão e incluem registros históricos/inativos por ID ou filtro. O servidor reconsulta usuário ativo e papéis atuais antes do comando ou replay; uma claim Admin antiga não conserva uma permissão removida.

| Comando | Papel | Condições principais |
| --- | --- | --- |
| Criar/editar metadados de Local/Lote | Autenticado | Referências válidas; identidade física do lote preservada; novo vínculo de Propriedade ativa |
| Entrada | Autenticado | Item ativo com PodeEntrar, lote/local ativos; vencido ou Bloqueado somente em Segregação |
| Transferência | Autenticado | Lote elegível; origem/destino Ordinários ativos; quantidade canônica |
| Saída manual | Autenticado | Lote elegível, saldo, motivo e destino/finalidade textual |
| Consumo interno | Autenticado | Lote elegível, saldo e Item.UsoInterno |
| Abertura | Admin | Conferência física, posição sem qualquer movimento anterior |
| Ajuste | Admin | Quantidade contada alvo, data, motivo/evidência; delta calculado no servidor, sem saldo negativo |
| Segregação | Admin | Destino Segregação ativo; bloqueia o lote inteiro, inclusive saldo em outros locais |
| Retorno da segregação | Admin | Origem Segregação ativa, destino Ordinário ativo; lote ativo, Liberado e não vencido; item ativo |
| Descarte | Admin | Evidência/motivo e quantidade limitada pelo saldo; admite material inelegível |
| Ativo/finalidade de Local; situação/validade/referências/ativo de Lote | Admin | Revisão e motivo/evidência; finalidade exige todas as posições zeradas; encerramento exige saldo global zero |

Admin pode criar lote de inventário para item inativo/sem PodeEntrar, sempre Bloqueado. Inativação não apaga saldo nem histórico. Encerrado é terminal para movimentos e liberação. Não há autorização para consumir ordinariamente material vencido, fracionar contagem ou produzir saldo negativo.

## Validade e referências

Validade é opcional. Ausência aparece como **validade não informada** e não afirma aptidão sanitária; não há política inferida de espécie/classe. A data é válida até o próprio dia operacional, inclusive; vencido significa validade anterior à data de São Paulo no servidor. Datas observadas não retroagem o saldo ou substituem a data operacional.

Lote Liberado pode tornar-se inelegível por vencimento, inativação de item/local/lote ou localização em Segregação. Bloqueio afeta todo o lote. Corrigir validade não apaga fatos anteriores. Para retirar material da segregação, Admin primeiro libera pelas condições válidas e depois executa o retorno explícito. Recebimento nunca libera um bloqueio automaticamente.

Perfil ausente permite estoque quantitativo e não afirma composição. Perfil/conversão selecionados são congelados nos eventos com seus dados e fontes. Trocar referência exige comando Admin e histórico; os movimentos anteriores conservam os snapshots. Mudança de referência não reconverte o saldo existente.

## Unidades e resolução

Unidades canônicas são kg, L e un. HTTP transmite quantidades como strings invariantes, até 14 algarismos inteiros e seis decimais, sem vírgula, sinal ou notação exponencial. Contagem `un` sempre é inteira. Sequências e cortes bigint também são strings para conservar valores acima de 2^53.

g→kg e mL→L são exatos. Conversões entre dimensões exigem uma versão documental vinculada ao lote e contexto aplicável; não há fator padrão, caminho transitivo ou densidade implícita. O inverso divide pelo fator original, sem multiplicar por um recíproco previamente arredondado.

Saldo e movimentos usam `numeric(20,6)`. Quando o cálculo gera mais de seis casas, a prévia apresenta calculado, armazenável e resíduo `calculado − armazenável`. ToEven só é aplicado com aceite explícito desses valores e motivo; o servidor recalcula. Resultado zero, overflow ou contagem fracionária são recusados. O resíduo não vira saldo paralelo ou perda fictícia. Transferência, segregação e retorno apontam a quantidade canônica e não reconvertem/quantizam novamente.

## Livro, projeção e idempotência

Eventos, linhas, histórico e respostas de comandos são imutáveis. Posição zerada permanece. Projeção, linhas, metadados afetados, auditoria e resposta idempotente confirmam na mesma transação. Reconciliação compara livro e projeção no mesmo snapshot, com paginação e filtros; nenhuma leitura repara saldo.

Toda mutação exige `Idempotency-Key` de 1–100 caracteres e revisões esperadas. Posição inexistente tem revisão 0. Replay do mesmo autor/operação/recurso/chave e payload semântico retorna status, Location e corpo originais antes de revalidar versões/eligibilidade, mantendo a autorização atual. Strings decimais equivalentes têm o mesmo hash; mudanças de motivo, revisão, referências ou aceite são divergentes e retornam 409.

Após falha de rede, repita a mesma chave e payload. Ao corrigir dados conscientemente, obtenha outra prévia e chave. Uma chave diferente ou outro autor não deduplica universalmente dois recebimentos do mesmo fato: é necessária revisão operacional do documento/lote/histórico. Erros não são armazenados como sucesso.

Escritas usam ReadCommitted e timeout de lock de 5 s: advisory catálogo `(413,421)`, depois físico `(430,1)`, depois linhas Item→Local→Lote→Posição em ordem estável. Isso sincroniza capacidades/unidade/referências com o catálogo. Não há locks de Caixa, Genealogia ou Animal. O protocolo serializa as escritas do primeiro módulo físico; medição de carga deve preceder eventual mudança de granularidade. Leituras compostas usam RepeatableRead e timeout SQL de 3 s, página padrão 25 e máxima 100. Cada página informa instante/corte; páginas diferentes podem ter observações posteriores.

## HTTP

Prefixo autenticado `/api/v1/estoque`. POSTs de criação/evento retornam 201 e Location; edição/transição retorna 200. Replay conserva esses valores e pode acrescentar `Idempotency-Replayed: true`.

| Recursos | Rotas |
| --- | --- |
| Locais | GET/POST `/locais`; GET/PUT `/{id}`; PATCH `/{id}/ativo`; POST `/{id}/finalidade`; GET `/{id}/historico` |
| Lotes | GET/POST `/lotes`; GET/PUT `/{id}`; PATCH `/{id}/ativo`; POST `/{id}/{bloqueio,liberacao,encerramento,validade,referencias}`; GET `/{id}/historico` |
| Saldos/livro | GET `/saldos`, `/saldos/{loteId}/{localId}`, `/movimentos`, `/movimentos/{id}` |
| Comandos | POST `/aberturas`, `/entradas`, `/transferencias`, `/saidas-manuais`, `/consumos-internos`, `/ajustes`, `/segregacoes`, `/retornos-segregacao`, `/descartes` |
| Leitura auxiliar | POST `/previas` (sem chave/gravação, mesmo papel do comando); GET `/responsaveis`, `/responsaveis/{id}`, `/conversoes/{id}`, `/reconciliacao` |

Listas retornam `items,page,pageSize,totalItems,totalPages,observadoEmUtc,sequenciaAte`. Responsáveis expõem somente ID/nome/ativo; seleção histórica por ID conserva o rótulo mesmo fora da página/inativa. Erros seguem ProblemDetails com código: 400 formato/conversão/quantização; 401 sessão/usuário inativo; 403 papel; 404 referência; 409 revisão, saldo, elegibilidade, código/chave divergente ou espera concorrente. Timeout de consulta indica que ela não foi concluída, sem resultado parcial apresentado como completo.

## Migração e recuperação

Migration F01: `20261007225228_AddManualInventory`. Acrescenta sete tabelas, sequência, FKs Restrict, índices/checks e guards de imutabilidade, unidade, contagem, abertura, transferência e conciliação transacional. Não cria saldo inicial nem tabelas de fases futuras. A aplicação não migra o banco no startup.

Antes de aplicação compartilhada futura: autorização própria, identificação do schema/migrations atuais, suspensão coordenada de escritores incompatíveis, backup consistente, restore isolado verificado e revisão do SQL de **todas** as pendências desde a origem real. Não misturar backend antigo que permita mudar unidade sem conhecer seu uso físico. O procedimento de imagens em [animal-evolution.md](animal-evolution.md) continua aplicável: volume existente, configuração explícita e backup/restore coordenados; não provisionar um substituto vazio.

Recovery depois de fatos: manter schema e livro, aplicar correção prospectiva ou restore coordenado aprovado. `Down` remove fatos e não é recuperação sem perda. Divergência na reconciliação requer investigação e conferência humana; ajuste só registra a contagem presente com motivo/evidência, sem apagar o livro.

F01 não executa receitas, reserva materiais, importa ovos, calcula custos, resolve dietas, lança Caixa ou representa compra/venda. Essas verticais dependem de contratos e autorizações posteriores. Para resultados reais dos testes e ambiente de revisão, consulte a [validação da entrega](../validation/2026-10-08-estoque-435.md).
