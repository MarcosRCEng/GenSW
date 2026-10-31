# F04 — matriz de cenários e oráculos de aceite

P4.0 [#508](https://devops-lab.tailaf9418.ts.net/issues/508), 31/10/2026. Complementa o [contrato](2026-10-31-f04-custos-materiais-508-design.md) e o [plano condicionado](../plans/2026-10-31-f04-custos-materiais-508-implementation.md). **Fixtures exclusivamente sintéticas; não são preços, recebimentos ou ovos reais, nem seeds. Estes são testes futuros, não testes F04 já executados.**

IDs sintéticos de destino: A=`00000000-0000-0000-0000-000000000001`, B=`00000000-0000-0000-0000-000000000002`, C=`00000000-0000-0000-0000-000000000003`. Guid D minúsculo/ordinal ascendente desempata. Valores da tabela são BRL; comandos usam strings invariantes.

| ID | Cenário / fixture | Oráculo obrigatório | Camadas de verificação P4.1 |
| --- | --- | --- | --- |
| C01 | Referência 9,00/kg, recebimento comprovado 10 kg por 20,00 | Custo é 20,00, não 90,00; referência não muda livro/snapshot/caixa | Domínio, PG, API, UI |
| C02 | Recebimento sem documento; entrada F03 com prova individual mas sem custo | Quantidade física correta, cobertura Desconhecido, total/unitário completo null; nenhum 0 automático | PG, API, browser |
| C03 | Mesmo item: lote L1 10 kg/20,00 e L2 10 kg/30,00; consumir 1 kg de cada | 2,00+3,00=5,00; sem média; tentar receber preço distinto em L1 falha atomicamente | PG real, API/UI |
| C04 | Camada 10 kg/20,00, transferir 3 kg | Origem 7 kg/14,00, destino 3 kg/6,00, mesma camada; transferir de volta conserva 20,00 | PG real, reconciliação |
| C05 | Camada 3 un/0,01; retirar/transferir 1 un de cada vez | Parcelas 0,00;0,00;0,01 pelo piso/resíduo; última esgota exatamente, todas com cobertura conhecida | Domínio, PG real |
| C06 | Componente 0,01; A=50%, B=50% | A=0,01 e B=0,00. Inverter array não muda o resultado; ambos conhecidos | Domínio, PG/API |
| C07 | Componente 0,01; A=33,333333%, B=33,333333%, C=33,333334% | Soma 100%; C recebe 0,01 pelo maior resto, A/B 0,00 | Domínio, PG/API |
| C08 | Componente 0,02; A=50%, B=25%, C=25% | A=0,01; B=0,01; C=0,00, empate B/C resolvido por ID | Domínio, PG/API |
| C09 | Dois componentes 0,01 cada, A/B=50/50 | Rateio por componente: A=0,02, B=0,00; não ratear total agregado para obter 0,01/0,01. Proveniência/somas por componente preservadas | Domínio, PG real |
| C10 | Consumo 10,01; A=50%, B=30%, perda C=20%; A 2 kg/B 3 kg | A=5,01; B=3,00; perda=2,00. Unitários completos 2,505000/kg e 1,000000/kg; soma 10,01, sem baixa física adicional da perda | PG real, API/browser |
| C11 | Duas parcelas da mesma linha F02, IDs A/B e lotes de saída distintos | Cada parcela positiva é destino próprio e recebe camada própria; agregar por LinhaId não elimina parcela | PG, API/UI |
| C12 | 2 kg conhecidos por 4,00 + 1 kg sem custo; A/B=50/50 e saídas positivas | Conhecido A=2,00/B=2,00; lacuna de 1 kg da origem acompanha ambos; total/unitário completo null. Não calcular percentual global kg+un | PG real, API/browser |
| C13 | Consumo 10 kg com conhecido 4,00 e lacuna; nenhuma saída positiva | Admin/evidência; perda total=100%, conhecido 4,00 e total null; nenhum lote, divisão por zero ou custo unitário | PG real, API/browser |
| C14 | Saída A=0, saída B positiva; solicitar A=10%/B=90% | Rejeitar percentual em A; com A=0%/B=100%, A não tem lote/custo unitário e B recebe componente/lacuna integral | Domínio, PG/API/UI |
| C15 | Percentuais 99,999999 ou 100,000001; duplicar ID; negativos/excesso de casas | 400/erro contratual, zero escrita física/monetária/reserva; nenhuma normalização para 100 | Domínio, PG/API |
| C16 | Mesmo rateio/revisões, moeda USD ou escala total 0,001 | Rejeitar; sem câmbio/rounding de total implícito. Zero documentado explícito é conhecido; valor vazio é desconhecido | Domínio, API |
| C17 | Associação F03 a entrada já custeada ou desconhecida, sem entrada nova | Zero novo custo/quantidade; mantém exatamente origem, camada e lacunas; não contabilizar duas vezes pelos IDs de ovos | PG real, API |
| C18 | Compensação F03 física admissível; invalidação apenas documental | Compensação retira parcelas originais exatas uma vez; invalidação não movimenta custo/físico; uso posterior impede compensação conforme guardas | PG real, SQL/API |
| C19 | Reversão F02 por erro admissível, saídas íntegras sem uso posterior | Compensa centavos/quantidades originais, sem recalcular maior resto, não cria Caixa; segunda reversão rejeitada | PG real, API |
| C20 | Transferir e devolver, reservar e liberar ou consumir saída antes da reversão F02 | Preservar impedimentos físicos atuais; nenhum crédito fictício de ancestrais. Concorrência uso×reversão tem um vencedor íntegro | PG RC/RR, API |
| C21 | Documento tardio do saldo legado sem custo; origem já parcialmente consumida | Avaliação presente só da quantidade ainda identificada, sem cobertura duplicada. Passado/snapshot/descendentes intactos, parte consumida segue lacuna histórica/anotação | PG real, API/UI |
| C22 | Correção do valor/fonte de camada já usada | Original auditável, avaliação/compensação presente com Admin/motivo/prova; sem reprecificar saídas passadas. Reversão incompatível bloqueada | PG real, API/browser |
| C23 | Material intermediário com custo 3,00 e ancestrais consultáveis | Nova execução consome 3,00 uma vez, não 3,00 mais ancestrais; lacunas herdadas visíveis | PG real, API |
| C24 | Primeira transferência de posição histórica sem camada | Novo fato reconhece quantidade legada desconhecida uma vez sob lock; saldo físico e snapshots históricos não mudam | PG real, concorrência |
| C25 | Comando antigo omite custo/rateio F04; replay de resposta anterior à migration | Físico continua válido e replay antigo idêntico; recebimento desconhecido; execução sem rateio fica pendente, nunca total falsamente completo | PG upgrade, API/browser |
| C26 | Falha após débito físico, antes do snapshot de custo; retry após resposta perdida | Rollback integral na falha; sucesso durável reproduz uma resposta e um fato por chave; payload diferente=409; troca de autor não duplica fato F04 | PG/API/UI |
| C27 | Correção×consumo, transferência×consumo, recebimento incompatível×confirmação, dois avaliadores do mesmo saldo | Locks/revisões garantem um estado serial válido ou conflito; sem custo negativo, cobertura sobreposta, reserva violada ou perda de centavo | PG real, barreiras RC/RR |
| C28 | SQL direto altera/apaga snapshot/camada ou insere destino inválido, soma rateio errada, parcela sem vínculo físico | Guardas imediatas/diferidas recusam e rollback; testar ordem invertida de inserts e commits, sem desabilitar triggers | PG real RC/RR |
| C29 | Upgrade de base anterior com 20 migrations e fatos F01–F03/Caixa | Tabelas F04 vazias, nenhum seed/backfill; IDs, contagens, referências e hashes de linhas/snapshots/respostas anteriores iguais antes de fixtures | PG upgrade, SQL idempotente, dump/restore |
| C30 | Leitura/reconciliação em concorrência, falha ou timeout | Mesmo corte físico/monetário e cobertura; conflito/erro de leitura não vira “sem custo”/zero; reconciliação nunca repara dados | PG, API/browser |
| C31 | Anônimo; papel comum; Admin revogado após prévia ou antes do replay | 401/403 pertinentes; revalidação após lock; rotina comum permitida, avaliação/referência Admin protegidas no serviço e HTTP | API real, UI |
| C32 | Referência vencida/sobreposta, estimativa com material sem referência ou conversão | Seleção explícita de versão/contexto; snapshot estimado com fonte/data/unidade e lacuna; custo comprovado e histórico não alterados | Domínio, API/browser |
| C33 | Unidade g/mL canônica, un fracionada; máximo monetário/overflow intermediário | Conversão exata aplicável; un fracionada recusada; aritmética exata com limites explícitos, nenhuma conversão float | Domínio, PG/API |
| C34 | Saída positiva recebe custo 0,01 para 3 un | Unitário exibido 0,003333 com arredondamento/resíduo; total autoritativo 0,01; nunca reconstruir total como 0,009999 | Domínio, API/UI |
| C35 | Navegação desktop/390px, teclado, selecionar referência fora da página, editar prévia, erro e recuperar | Filtros/retorno/chave retidos; referência histórica legível; prévia obsoleta invalidada; nenhuma ação dupla; conhecido/lacunas/estimado distinguíveis | Browser real + frontend |

Invariantes transversais em todos os testes: nenhum lançamento automático no Caixa; nenhuma alteração de eventos/snapshots antigos; nenhuma soma monetária “completa” quando há lacuna atribuída; nenhuma criação de estoque/custo novo por associação F03; saída zero sem unitário. Executar as suites de regressão F01–F03, catálogo/formulação, autenticação/imagens e Caixa na implementação, sem usar contagens históricas como prova de testes futuros.
