# P3.0 — contrato proposto da ponte explícita de ovos F03

27/10/2026, America/Sao_Paulo. [Tarefa #499](https://devops-lab.tailaf9418.ts.net/issues/499), filha da [Evolução #430](https://devops-lab.tailaf9418.ts.net/issues/430). Revisão documental versionada `P3.0-499-r2`, derivada da r1 anexada à Tarefa, com publicação Git expressamente autorizada. **Proposta para aceite; F03 não implementada nem aceita.** Este documento detalha a seção F03 do desenho de continuidade; preserva os contratos aceitos F01/F02. Mudanças propostas sobre recebimentos de itens geridos por F03 precisam do aceite B03 e da autorização do recorte.

Leia o [quadro de decisões](2026-10-27-f03-ponte-ovos-499-decisions.md), o [plano P3.1](../plans/2026-10-27-f03-ponte-ovos-499-implementation.md), as [evidências](../../validation/2026-10-27-p3-0-contrato-f03-499.md) e a [instrução condicionada](../plans/2026-10-27-p3-1-implementacao-f03-499-prompt.md). As decisões abaixo são recomendações, não fatos sobre o acervo.

## 1. Base e fronteira

Base inspecionada após fetch: `origin/main = acd8578e811c964c4a8d8b9aebdb66dd7983dd93`. F01 aceita; F02 V1–V5 integrada pelo PR31 e ainda em validação funcional na #495. A #493 permanece Planejado para validação geral final. As duas CIs verdes da F02 são evidência dessa base, não cobertura de F03.

`ProducaoOvo` registra AnimalId, data de postura, peso em gramas, observação e timestamps. Não informa quantidade, unidade/amostra ou revisão numérica. O índice AnimalId/data não é único. `TotalLancamentos` conta registros, não ovos. As rotas zootécnicas POST/GET/PUT continuam com seus contratos atuais; não há DELETE de produção de ovos nessa base. O contador React de recarga não é revisão do domínio.

Uma linha só poderá representar **uma unidade física na ponte** após declaração humana e evidência de que aquele lançamento corresponde a um ovo individual. Não aplicar essa semântica retroativamente a todas as linhas. Registro coletivo, amostra, peso agregado ou cardinalidade desconhecida permanece fora da ponte deste recorte; estoque coletivo continua manual F01, sem fabricar IDs individuais. Ciclos possuem totais agregados, sem IDs de ovos ou vínculo com ProducaoOvoId; nunca derivar identidade deles, de animal/data/peso ou da sequência de linhas.

Consulta agregada READ ONLY em três bases acessíveis encontrou, em cada uma, 110 lançamentos zootécnicos e zero itens/lotes/eventos/movimentos/posições/comandos de estoque. Não demonstra ausência de estoque físico, planilhas, outras bases ou equivalência das linhas. As três bases tinham 18 migrations, enquanto a base de código inclui também a migration F02. Não foram atualizadas nem escolhidas como destino de implementação. Ver [limites da evidência](../../validation/2026-10-27-p3-0-contrato-f03-499.md).

## 2. Operações e dependências

| Operação proposta | Efeito | Condições |
| --- | --- | --- |
| Prévia | Somente leitura consistente; não reserva ovo, saldo ou capacidade manual | IDs escolhidos explicitamente; nenhuma seleção automática por filtro |
| Entrada nova | Uma entrada F01 em `un` e pontes correspondentes no mesmo commit | Cada ID comprovadamente individual, sem ponte vitalícia e sem recebimento anterior, conciliação aprovada e atual |
| Associar entrada anterior | Fato F03 imutável que referencia EventoId e MovimentoEstoqueId; delta de estoque **zero** | Mesmos item e unidade física comprovada, capacidade histórica disponível e equivalência identificada |
| Compensar entrada F03 | Débito compensatório exato e único, sem apagar origem; pontes terminais | Admin, erro de registro comprovado, grupo integral intacto, sem qualquer uso posterior |
| Invalidar associação documental | Fato de invalidação/anotação, delta zero; ponte e capacidade continuam ocupadas | Admin, motivo/evidência; não desfazer o recebimento manual |
| Anotar / conciliar | Fatos corretivos documentais ou leitura de divergências | Sem reconstrução de passado, saldo automático ou liberação de reentrada |

Entrada simples depende de F01. Consumo posterior em transformação usa F02 existente, selecionando o lote como insumo, sem criar ordem/consumo na confirmação F03. Não receber em lote de saída F02 (`SaidaProducao`), já protegido contra recebimentos adicionais. Não há dependência de F04. F03 não cria Caixa, custo, genealogia animal ou identidade individual de ovos no consumo F02.

Recomendação para entrada nova: o operador escolhe Item de ovos em `un`, um lote F01 vazio e sem fatos/reservas anteriores, explicitamente dedicado ao grupo, e Local compatível. O servidor revalida esse destino e registra sua origem F03 de forma estruturada. Depois da entrada, o lote não aceita recebimento ordinário adicional, tal como a proteção de origem já existente para lotes produzidos, mas sem classificá-lo como saída F02. Novo grupo usa novo lote. Essa restrição é proposta B03, não comportamento atual F01. Transferências e usos posteriores mantêm as regras F01/F02.

As demais regras físicas são preservadas: Item ativo e PodeEntrar, unidade fixada, responsável ativo, revisões esperadas, Local ativo, situação/validade do lote. Pendência/vencimento exige Segregacao e bloqueio segundo F01; F03 não libera material. Não inferir validade da espécie ou da postura. Data observada não altera saldos no passado.

## 3. Seleção, conciliação e prova

Antes de entrada, o responsável define um recorte de conferência: itens explicitamente escolhidos como ovos para F03, origem física/período/controle documental e registros abrangidos. Essa classificação é explícita; não descobrir itens pelo nome. O recorte documenta o que foi e não foi consultado, inclusive registros externos. Admin aprova a conferência; operador confirma a seleção concreta e a declaração de não recebimento anterior. Uma caixa marcada isoladamente não transforma acervo desconhecido em acervo conciliado.

Cada ID recebe uma decisão humana: `NaoRecebidoComprovado`, `RecebidoComEquivalencia` ou `Pendente`. Não há valor padrão. A primeira permite nova entrada; a segunda exige associação sem saldo; a terceira bloqueia entrada. Se ausência de acervo consultável impedir a prova, manter pendente e registrar responsável/próxima providência: conferir contagem/documentos e, se necessário, abrir tratamento próprio. Não usar ajuste ou entrada manual como atalho para duplicar a mesma origem.

A prova de entrada anterior deve identificar movimento de entrada original manual F01 do tipo Entrada ou Abertura, ItemId, lote/local históricos, quantidade inteira de ovos comprovada, fonte documental/contagem, responsável, data da conferência e mapeamento explícito dos IDs para unidades dessa entrada. Excluir todo crédito raiz e lote de entrada nova F03: ele já possui suas pontes, não pode receber outros IDs por prova manual. Essa exclusão também exige guarda SQL. Ajuste positivo, transferência, compensação e crédito de reversão F02 não são um novo recebimento elegível. Mesmo item, data, peso, texto de origem ou igualdade de saldo não prova equivalência.

No recorte recomendado, movimento elegível tem unidade canônica **e declaração original em `un`**, sem contagem derivada de conversão de kg. Uma entrada apenas em kg, mesmo com fator médio ou saldo compatível, não prova número de ovos. Documento externo com contagem comprovada para entrada somente em kg exige contrato/extensão posterior; por ora bloquear associação e nova entrada potencialmente duplicadora. Não converter o livro antigo para `un` nem alterar a entrada.

A prova congela a contagem comprovada `N`, limitada à quantidade original explícita em `un`. Cada associação usa um ordinal documental entre 1 e N. Esse ordinal é uma posição na prova, não identidade física inventada. Unicidade `(MovimentoEstoqueId, ordinal)` e prova única por movimento impedem sobrealocação. A prova aceita pode ter subconjunto associado: `N = associados vitalícios + unidades ainda não associadas`. Não descontar associações compensadas/invalidadas. Somar todas as associações anteriores, mesmo de outro comando, operador ou recorte. Não utilizar saldo atual como capacidade histórica.

Associação após consumo/transferência é permitida apenas como origem histórica comprovada e sem saldo. Ela não afirma que o ovo está disponível ou identifica qual ID foi consumido: F01/F02 movimentam lotes, não indivíduos. Exibir essa limitação. Não preencher lacunas de genealogia física retroativamente.

## 4. Prévia e confirmação

1. Operador escolhe explicitamente IDs, operação, item/lote/local ou movimento anterior, prova/recorte, responsável e motivo. Seleção entre páginas é visível e revisável; filtro nunca significa selecionar todos. Rejeitar IDs repetidos.
2. Servidor captura os registros selecionados e referências em uma transação RepeatableRead READ ONLY, sem reservar recursos. Retorna snapshots/hash/UpdatedAtUtc por ID, revisões físicas, revisão da conciliação/prova, capacidade manual, corte do livro e efeitos esperados. Expõe elegíveis e impedidos com razão por linha. Remover impedidos exige nova seleção consciente e nova prévia.
3. Exibir quantidade exata em `un`, delta e saldo previsto para entrada, ou **nenhum incremento** para associação. Mostrar divergências, pendências, fonte da massa e prova. Apenas seleção explicitamente aceita pode confirmar. Alterar seleção, destino, prova ou declarações invalida a prévia.
4. Confirmação relê sob locks e compara tudo. Um único ID alterado, ponte existente, revisão/corte/prova obsoletos, capacidade insuficiente ou pendência aborta o grupo inteiro. Não confirmar só as linhas remanescentes silenciosamente. É permitido preparar uma seleção parcial menor em outra prévia.
5. Commit único grava pontes, prova/alocações, fato documental, eventuais movimentos/projeção, histórico e comando idempotente. Falha anterior ao commit não deixa lote de confirmação, ponte, capacidade consumida ou comando parcial. Cadastros de lote/local previamente criados continuam cadastros vazios, conforme F01.

Prévia usa token opaco verificável pelo servidor ou registro de prévia versionado; nunca confiar em hashes/quantidades recalculados pelo cliente. O plano escolherá a implementação coerente com os padrões locais e testará adulteração. Não tratar hash como prova do fato físico.

## 5. Ponte vitalícia e snapshot

Uma ponte por `ProducaoOvoId`, sem índice filtrado por estado. Persiste após compensação, invalidação, edição da origem e troca de autor/chave. FK Restrict e imutabilidade preservam origem; não liberar reentrada por exclusão lógica ou chave nova. Ponte referencia grupo/operação, item/lote/local históricos, movimento físico original (direto ou manual), prova/ordinal quando aplicável, autor/responsável, timestamps e versão do algoritmo. Correções são novos fatos ligados à ponte; projeção de estado, se houver, deve coincidir com esses fatos.

Snapshot mínimo: ProducaoOvoId, AnimalId, DataPostura, PesoGramas persistido, Observacao persistida, UpdatedAtUtc, versão de canonicalização e hash SHA-256. Contexto físico/prova/declarantes fica no fato de confirmação. A observação pode conter dados pessoais: fica somente no armazenamento autenticado exigido pelo contrato, sem cópia em log, anexo público ou relatório de operação. Relatórios de evidência usam IDs técnicos autorizados ou exemplos sintéticos, sem conteúdo nominal.

Canonicalização versionada: ordem fixa de campos; Guid normalizado; data ISO; decimal invariável correspondente ao valor persistido `numeric(10,2)`; UTC na precisão persistida PostgreSQL; nulo distinto de vazio; conteúdo de observação conforme persistido, sem normalizar semanticamente textos. Comparar hash **e** UpdatedAtUtc observado; hash detecta conteúdo diferente mesmo se timestamps coincidirem. Não inventar `Revisao` para ProducaoOvo. Revisões novas pertencem somente aos recursos F03.

Edição persistida antes da confirmação invalida a prévia. Confirmação que persiste primeiro congela o original; edição posterior zootécnica continua permitida e será sinalizada na conciliação, sem mudar saldo, conversão ou snapshot. O contrato serializa fatos por persistência; não promete detectar a abertura de uma tela anterior. Regressão deve preservar POST/GET/PUT atuais, sexo Fêmea/espécie Ovípara e métricas atuais. Não introduzir filtro oculto de Animal ativo/escopo ou mudar genealogia.

## 6. Transação e concorrência

Reutilizar ReadCommitted e a ordem atual catálogo `(413,421)` → inventário `(430,1)`, timeout 5 s. Se a operação consultar dependências mutáveis F02 para compensação, adquirir também produção `(430,2)` antes dos locks de linha. Não abrir transações aninhadas nem chamar sequências de endpoints públicos F01. Não adquirir lock de genealogia `(364,368)` ou Caixa.

Manter a ordem relativa física F01 Item → Locais ordenados → Lote → Posições ordenadas; nos itens geridos inserir o lock da âncora de conciliação depois de Item e antes de Locais/Lote/Posições. Recursos adicionais de prova e linhas `ProducoesOvos` são bloqueados em ordem determinística depois da coordenação compartilhada; todos os caminhos F03 usam a mesma ordem. Reler origens do banco após os locks, sem reutilizar valores EF previamente rastreados, com `FOR SHARE` ou lock equivalente que conflite com UPDATE real até commit. Isso fecha a janela entre comparação e ponte, embora o PUT atual não possua lock explícito. Testar a interação PostgreSQL real; não confiar no nome `GetForUpdateAsync`, que atualmente é leitura EF comum.

Dois comandos sobre o mesmo ID: um cria a ponte; o outro recupera replay se mesma chave/payload, ou recebe conflito vitalício. Dois IDs diferentes disputando a última unidade de movimento manual: no máximo um ocupa o slot. Locks de aplicação não bastam: índices, FKs, checks e guardas de commit devem preservar unicidade, capacidade e completude também com SQL concorrente sem advisory, em ReadCommitted e RepeatableRead.

**Recebimento manual concorrente:** a confirmação usa o mesmo lock F01 e revalida corte do livro e revisão da conciliação para os itens envolvidos, abrangendo outros lotes/locais. Uma âncora persistida por Item governa a revisão dos recortes: todo crédito relevante (inclusive Entrada/Abertura/Ajuste manual) atualiza atomicamente a revisão e invalida a conferência afetada, com guarda também para SQL sem advisory. Se um crédito ocorreu após a prévia, exigir nova conferência; não apenas recalcular saldo e continuar. O recebimento manual dos itens explicitamente geridos por F03 deve participar da mesma política: identificar origem individual por ponte atômica ou origem coletiva distinta comprovada. Pendência é fato documental **sem saldo** e impede o ingresso individual afetado. Não criar saldo em uma etapa para tentar vinculá-lo depois sem proteção transacional.

No lote de origem F03, bloquear novo recebimento manual ordinário também em SQL. Fora dele, guardar a declaração estruturada de origem/conferência e invalidar conciliação afetada no mesmo commit do recebimento. Se o recebimento não provar origem distinta, bloquear o comando potencialmente duplicador e encaminhar tratamento humano. Apenas adquirir lock não impede que dois comandos semanticamente duplicados sejam executados em sequência; são necessários prova e fluxo humano. Nenhum banco consegue reconhecer o mesmo ovo físico em lançamentos livres sem identidade/prova. A entrega deve declarar essa fronteira e fechar o caminho de UI/API gerido por F03; não prometer ausência universal de duplicação em registros externos.

## 7. Idempotência e HTTP

F03 tem comando próprio persistido. Proposta: chave única global por `(OperacaoF03, Idempotency-Key)`, independente de autor e dos IDs da seleção; operação inclui confirmar entrada, associar, compensar e invalidar. Na confirmação não existe raiz previamente persistida para escopar a chave por grupo. Compensação inclui GrupoId no hash; não mudar escopo silenciosamente. Chave não vazia até 100 caracteres; UUID gerado pelo cliente é uma prática, não ID de ovo. Não alterar o escopo F01 atual `(AutorId, Operacao, RecursoId, Chave)` nem o F02 por ordem.

Hash canônico do payload inclui operação, conjunto ordenado de IDs, snapshots/expectativas, item/lote/local ou movimento/prova/ordinais, recorte/revisões/corte aceitos, responsável, motivo/evidência e decisões explícitas. Excluir autor atual do hash para permitir recuperação por outro operador autorizado; manter autor original no fato. Não normalizar documentos/textos como números. Revalidar usuário ativo/permissão atual após esperar locks e **antes do replay**. Depois, procurar replay antes de revalidar estado/revisões atuais.

Mesma chave/hash devolve status HTTP, Location, corpo e IDs originais, mesmo após edição, compensação ou invalidação; GET separado mostra estado atual. Chave igual/hash diferente: 409. Chave nova com ID já ligado: 409, inclusive após compensação. Resposta perdida depois do commit: repetir a mesma chave/payload; jamais gerar chave nova automaticamente. Falha anterior ao commit não registra sucesso. Chaves/fatos não são expurgados para permitir reutilização.

Proposta de rotas aditivas: `/api/v1/producao/ponte-ovos` com candidatos paginados, prévia, confirmações, grupos/detalhes, comandos/replay, provas/conciliações e tratamentos Admin. Definir nomes finais junto ao código existente. Não apresentar essas rotas como já disponíveis.

| HTTP | Semântica proposta |
| --- | --- |
| 200 / 201 | Prévia/leitura/tratamento ou criação de grupo; replay preserva status/Location originais |
| 400 | Formato, seleção vazia/repetida, contagem fracionária ou prova obrigatória ausente |
| 401 / 403 | Sem sessão / permissão atual insuficiente, inclusive no replay |
| 404 | Recurso inexistente; referência histórica inativa não vira inexistente |
| 409 | Origem alterada, ponte vitalícia, conciliação pendente/obsoleta, capacidade esgotada, destino inelegível, idempotência divergente, compensação impedida ou conflito transitório |

ProblemDetails com códigos estáveis e orientação de nova prévia/conferência/retry. Invariante quebrada produz falha sanitizada com correlação e bloqueio, nunca saldo zero, sucesso parcial ou reparo. Nenhuma informação de configuração/SQL/credencial deve vazar.

## 8. Massa, edição e compensação

Entrada recomendada conta ovos em `un`; PesoGramas é evidência zootécnica do snapshot, sem presumir que foi medido individualmente ou que representa a massa atual do lote. Somatório só pode ser rotulado como soma dos pesos registrados selecionados, com lacunas/semântica explícitas; não como massa física comprovada. Peso padrão da espécie não entra em conversão.

Uso posterior em F02 que exija massa usa conversão documental imutável do mesmo Item, explicitamente vinculada ao lote e aplicável ao seu contexto. Massa observada comprovada pode fundamentar uma conversão nova aprovada, sem gerá-la automaticamente da postura. Ausência de conversão/massa aplicável bloqueia o uso que dela depende; não impede consulta/estoque em unidades. Alteração de peso depois da ponte não reconverte estoque nem atualiza receita executada.

Compensação de entrada nova exige Admin, prévia, chave, coerência com contagem física e prova de erro de registro. Recomenda-se grupo integral em lote dedicado: quantidade original inteira no local original, nenhuma reserva histórica (mesmo liberada), movimento posterior (mesmo transferência/devolução), consumo/execução descendente ou mudança de revisão/situação/validade/referências do lote. Lote/posição não podem estar encerrados ou incompatíveis com o débito; reservas ativas e saldo não negativo permanecem protegidos. Uma falha impede todo o grupo.

Se permitido, debitar exatamente a quantidade canônica original, com referência estruturada ao movimento original e uma compensação única por grupo; não reaplicar conversão atual. Registrar estado terminal e encerramento do lote zerado sem apagar fatos. O mecanismo físico deve ser implementado explicitamente e integrado às guardas F01: `EventoReferenciaId` isolado não implementa compensação, e Ajuste atual é contagem presente, não inversão genérica. Perda real de ovos efetivamente recebidos exige saída/descarte presente F01, sem compensar um recebimento verdadeiro. Compensação parcial fica fora deste recorte.

Associação manual não criou saldo: invalidá-la só acrescenta fato documental e não debita a entrada antiga. Não liberar slot nem permitir nova entrada/associação do mesmo ProducaoOvoId nesta P3.1. Correção de destino depois do fato exige análise humana e contrato específico se não bastar anotação; não remapear origem silenciosamente.

Quando houver uso posterior, erro de peso/texto, acervo divergente ou prova insuficiente: preservar fatos, anotar, suspender ação afetada e conferir. Ajuste presente F01 Admin, quando materialmente cabível, exige contagem atual e referência estruturada à ponte/grupo, sem declarar nova importação, liberar ID ou restaurar ancestral consumido. Nenhum ajuste automático por edição zootécnica. O contrato de compensação não é autorização de operar em dados reais.

## 9. Permissões, auditoria e conciliação

Proposta D03: autenticado consulta candidatos/grupos/estado e prepara prévia ordinária; entrada nova comprovada pode ser confirmada por autenticado após conciliação Admin vigente. Admin aprova provas e recortes, associa recebimentos anteriores, invalida/compensa, registra tratamento excepcional e consulta conciliação global. Autor vem da sessão; responsável é usuário existente ativo, sem elevar privilégio. Serviço e controller verificam permissões, inclusive após espera e no replay. Não criar novo papel ERP nem alterar papéis globais; papel Codex do Redmine é apenas rastreabilidade.

Fatos de prova, associação, entrada, anotação, invalidação e compensação são imutáveis e ligados a autor/responsável, instante, motivo, referência e comando. Consultas autenticadas retornam labels históricos, IDs técnicos e vínculo ao livro. Não depender do texto Origem para integridade. Auditar decisões de não recebimento e suas limitações; hash é integridade do conteúdo observado, não atestado de existência física.

Conciliação usa snapshot RepeatableRead READ ONLY e cortes de sequência/revisões coerentes. Confrontar: ponte única vitalícia; snapshot versus origem atual; prova e slots dentro de capacidade histórica; entrada nova = número de pontes individuais; associação = delta zero; compensação única e exata; estado terminal não libera IDs/slots; livro assinado = projeção F01; reservas F02 ≤ físico; tratamentos pendentes e recebimentos sem prova. Separar divergência documental, física e de disponibilidade. Detectar corrupção sem alterar nada; preservar evidências e encaminhar correção autorizada em cópia isolada.

## 10. Gate de saída P3.0 / entrada P3.1

Os [cenários F03 do plano](../plans/2026-10-27-f03-ponte-ovos-499-implementation.md) são os critérios verificáveis deste contrato. O03 e os demais itens do [quadro](2026-10-27-f03-ponte-ovos-499-decisions.md) continuam pendentes. Recomendação operacional permanece **sem entrada automática** até comprovação de unidade e conciliação.

P3.1 somente poderá editar código/migrations após: (A) aceite explícito desta revisão do contrato, com exceções identificadas; e (B) autorização explícita do recorte B1–B5 do plano ou subconjunto definido. Registrar origem humana na Tarefa correspondente e revalidar journals antes de perguntar de novo. Aceite do contrato não equivale a autorização de implementação; autorização de implementação não aceita funcionalmente a entrega. Sem merge/deploy/migration compartilhada, F04–F06 ou antecipação da #493 por inferência.
