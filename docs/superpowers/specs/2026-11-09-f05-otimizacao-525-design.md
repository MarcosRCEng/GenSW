# F05 — otimização assistida de formulações

Data: 09/11/2026, America/Sao_Paulo. P5.0 [#525](https://devops-lab.tailaf9418.ts.net/issues/525), filha da Evolução [#430](https://devops-lab.tailaf9418.ts.net/issues/430). **Proposta para aceite; não é contrato aprovado nem autorização P5.1.**

Pacote: [decisões](2026-11-09-f05-otimizacao-525-decisions.md), [cenários](2026-11-09-f05-otimizacao-525-scenarios.md), [avaliação de solvers](2026-11-09-f05-otimizacao-525-solvers.md), [plano P5.1](../plans/2026-11-09-f05-otimizacao-525-implementation.md), [instrução copiável](../plans/2026-11-09-p5-1-implementacao-f05-525-prompt.md) e [evidências P5.0](../../validation/2026-11-09-p5-0-f05-525.md).

## 1. Base e autoridade das referências

Fetch de `origin/main`: `296d84876f19b79f13c735c94a326cf3f1a1a3d3`, integração da [PR #36](https://github.com/MarcosRCEng/GenSW/pull/36). F04 foi aceita funcionalmente na [#520](https://devops-lab.tailaf9418.ts.net/issues/520). A [#522](https://devops-lab.tailaf9418.ts.net/issues/522) registra aplicação autorizada de 21 migrations na OPERACIONAL-REAL; a [#523](https://devops-lab.tailaf9418.ts.net/issues/523) registra backup pós-lançamentos com hashes e restauração verificados. São evidências históricas reconsultadas, não nova inspeção do banco nesta P5.0.

A [PR #37](https://github.com/MarcosRCEng/GenSW/pull/37), correção isolada do Swagger, continua **aberta**, cabeça `1cfa5d7d7c66e1d70d6514b92a667d4ad288fec7`, sem merge. Não integra esta base nem é incorporada por inferência. [#493](https://devops-lab.tailaf9418.ts.net/issues/493) segue Planejado para validação geral após implementações; [#509](https://devops-lab.tailaf9418.ts.net/issues/509) segue Planejado como gate npm separado antes do piloto. A #524 preparou a instrução; não executa F05. Antes da #525, a consulta dos filhos da #430 e a evidência #524 não identificavam Tarefa F05 nem aceite de contrato F05.

Referências lidas: [design #430](2026-10-07-continuidade-f01-f06-430-design.md), [plano #430](../plans/2026-10-07-continuidade-f01-f06-430-implementation.md), [design #413](2026-10-05-producao-insumos-produtos-413-design.md), [operação de formulação](../../operations/catalogo-formulacao.md) e [contrato F04](2026-10-31-f04-custos-materiais-508-design.md). Seus diagnósticos de fases ainda futuras são datados; o código integrado e os aceites posteriores determinam o estado atual. Este pacote detalha a proposta F05, preservando contratos e aceites F01–F04.

### Correspondência com o código integrado

Todos os caminhos abaixo são relativos à raiz do repositório, na revisão indicada.

| Contrato existente | Evidência / implicação para F05 |
| --- | --- |
| Catálogo, unidades e conversões | `src/Backend/GenSW.Domain/Catalog/Item.cs` e `CatalogRules.cs`: classes/capacidades cumulativas; kg/L/un; entrada decimal invariante até 14 inteiros/6 casas; `un` inteira; conversão documental por item, sem densidade padrão. |
| Perfis e semântica | `src/Backend/GenSW.Domain/Formulation/NutritionProfile.cs`: estado separado da origem e do qualificador; componentes versionados, perfis publicados imutáveis. |
| Metas e normalização | `src/Backend/GenSW.Domain/Formulation/NutritionEngine.cs`: chave componente/método/contexto; energia exige espécie/fase compatíveis exatamente; BN/MS; totais parciais não são totais completos. `AceitarEstimados=true` é default manual atual; F05 propõe política própria, sem alterá-lo. |
| Receitas e resolução | `src/Backend/GenSW.Domain/Formulation/Recipe.cs` e `src/Backend/GenSW.Application/Formulation/FormulationService.cs`: mistura simples, inclusão, referências explícitas, publicação imutável, expansão limitada. F05 não expande intermediários/processamento. |
| Simulação/comparação congeladas | `src/Backend/GenSW.Application/Formulation/Contracts.cs` e `src/Backend/GenSW.Infrastructure/Persistence/FormulationModelConfiguration.cs`: snapshot e replay; `SnapshotsFormulacao.Tipo` admite somente `Simulacao/Comparacao`. Não acrescentar F05 ao JSON antigo por conveniência. |
| Interface existente | `src/Frontend/GenSW.Web/src/features/formulation/SimulationPages.tsx` e `RecipeEditor.tsx`: fontes explícitas, contribuições e criação humana de variação. Reutilizar padrões de navegação, sem mudar o cálculo manual. |
| Saldo e reserva | `src/Backend/GenSW.Infrastructure/Inventory/InventoryRepository.cs`: leitura RepeatableRead/READ ONLY. `src/Backend/GenSW.Application/Production/ProductionService.cs`: reserva nasce no início F02, não na consulta/planejamento. |
| Custos | `src/Backend/GenSW.Application/MaterialCosts/MaterialCostService.Reads.cs`, `Contracts.cs` e `src/Backend/GenSW.Domain/MaterialCosts/MaterialCostRules.cs`: custo por camada/quantidade/corte, cobertura completa ou lacuna; total/quantidade são autoridade, unitário exibido é arredondado. Retirada usa piso de centavos e resíduo, não multiplicação linear do unitário de tela. |

## 2. Primeiro problema operacional e recorte N1

**Pergunta proposta:** dada uma mistura simples manual de massa fixa que falha no conjunto de metas informadas, qual redistribuição entre os ingredientes explicitamente selecionados atende todas as metas e limites com a menor alteração de massa em relação à fórmula de referência?

Exemplo do problema, exclusivamente sintético: as misturas A60/B40 e A30/B70 do #413 falham em metas diferentes; A50/B50 atende ao conjunto do planejamento #430. O otimizador deve explicar a mudança e seus limites. Isso não demonstra demanda por requisito nutricional de nenhuma espécie; a prioridade operacional e as fontes reais precisam ser aceitas pelo responsável em D05-01/D05-03.

N1 recomendado para P5.1: mistura simples, uma saída alimentar de massa igual à entrada, sem perdas, retenção, secagem, drenagem, sub-receita, embalagem ou consumível; itens alimentares canônicos em **kg**, com perfil publicado explicitamente escolhido; metas duras em BN; objetivo não econômico de menor desvio. Entrada em g pode ser normalizada exatamente para kg. Sem candidatos em L/un ou conversão entre dimensões neste recorte. Não converter um intermediário estocado em seus ancestrais.

Sem necessidade demonstrada de decisões inteiras: a variável é massa divisível. Sacos fechados, número máximo de ingredientes, compra mínima, escolha liga/desliga e contagem indivisível constituem problemas diferentes. Não introduzir MILP ou CP-SAT apenas porque existem itens em `un` no catálogo. A resolução de seis casas é regra de transferência/revalidação, não promessa de ótimo numa malha inteira.

Fora de P5.1 recomendado: objetivo econômico, disponibilidade como restrição ou indicador novo F05, reserva, execução física, metas MS, fórmulas não lineares/robustas, prescrição, tabela automática por espécie, produção em cadeia, F06, Caixa, #493 e #509. A consulta de estoque existente continua disponível e **não reserva**.

## 3. Entradas, fontes, unidades e elegibilidade

| Entrada | Contrato proposto |
| --- | --- |
| Massa `M` | Positiva em kg BN, até seis casas e limites do catálogo. Não representa tamanho mínimo de produção nem capacidade física disponível. |
| Referência `r_i` | kg BN não negativos, soma exata `M`; receita/versão e revisão exatas quando importada, ou formulação manual identificada. Novo candidato tem referência zero. Sem reescalar/normalizar silenciosamente. |
| Candidatos | Identificador estável da linha; um ItemId por candidato em N1; Item ativo, `PodeEntrar`, classe `Alimentar`, unidade kg. Perfil publicado do próprio item, fonte/método/preparação/amostra/contexto e aplicabilidade escolhidos. Ativo/capacidade não certificam adequação alimentar. |
| Inclusão | `l_i`/`u_i` em % da massa alimentar BN, intervalo fechado 0–100, até seis casas, `l_i≤u_i`. Ausência vira 0/100 **visível antes de executar**. Fixação exige limites iguais. Importar limites existentes da receita; mudança requer confirmação registrada. |
| Meta `j` | ID; componente e versão da definição, método/contexto, modalidade energética quando pertinente, BN, unidade, mínimo e/ou máximo; valores não negativos, min≤max. Sem meta não existe problema N1. Não escolher automaticamente quais nutrientes otimizar. |
| Origem da meta | `InformadaUsuario`: autor, data e justificativa explícita, sem atribuição técnica inventada. `ReferenciaTecnica`: fonte/edição/seção/data, espécie/fase/preparação/aplicabilidade e responsável pela seleção. Ausência de fonte técnica não vira uma recomendação do sistema. |
| Política de estimativa | Default F05 `AceitarEstimados=false` por meta. Aceitação explícita, hipótese, fonte e motivo por meta permitem coeficientes estimados naquela restrição, rotulando o resultado. Não mudar o default das simulações anteriores. |
| Contexto | EspécieId quando aplicável, fase de uso e estado/processo. Preservar matching exato do motor para componente/método/contexto; energia exige o contexto e igualdade de espécie/fase previstos no motor. Ausência de espécie não significa universalidade. |

Todos os ingredientes com `u_i>0` precisam de coeficiente utilizável para cada meta obrigatória, mesmo que uma solução tentasse escolher `x_i=0`. Não permitir que o algoritmo aproveite lacunas. Exclusão é ação humana explícita (`u_i=0`, justificativa) anterior à validação da matriz; manter a linha excluída e o motivo no snapshot. Se `r_i>0` nessa linha, sua remoção participa do desvio. Não excluir automaticamente ingrediente com dado faltante.

Normalizar concentrações de massa em g/kg BN e energia em MJ/kg BN pela semântica atual. Entrada em g que gere massa kg com mais de seis casas deve ser corrigida explicitamente antes da execução, sem arredondamento implícito da baseline/M. Conversão kcal↔MJ apenas na mesma modalidade, segundo convenção já documentada; não equiparar energia humana, EB, ED, EM, EMAn ou EL. Não equiparar PB a proteína digestível, gordura a EE, FB a FDN/FDA/fibra alimentar ou mineral total a disponível.

Perfil observado em MS pode originar coeficiente BN somente com matéria seca/umidade conhecida e compatível da mesma amostra/contexto: `aBN=aMS*d`, `d=kg MS/kg BN`. Guardar observação e derivação exatas; estimativa de `d` contamina o coeficiente. **Metas MS ficam fora de N1 por escopo**, não por exigirem necessariamente modelo não linear: com dados completos, `ΣaBN*x ≥ metaMS*Σd*x` continua linear, desde que o denominador seja positivo.

| Condição do dado | Tratamento antes do solver |
| --- | --- |
| Conhecido, Pontual, Medido/Declarado, compatível | Coeficiente escalar, inclusive zero explicitamente documentado. Não elevar declaração a medição. |
| Desconhecido/observação ausente/denominador MS necessário ausente | `DadosIncompletos`, listar candidato×meta, fonte requerida e motivo. Não zero, média dos conhecidos nem inviabilidade. |
| NaoAplicavel em ingrediente alimentar requerido pela meta | `DadosIncompativeis/CoeficienteNaoAplicavel`; motivo preservado; não é zero. Embalagem nem chega à matriz N1. |
| Minimo/Maximo/Faixa/AbaixoDeteccao | Não usar como teor pontual. `DadosIncompletos/CoeficienteNaoPontual`; preservar limite/qualificador. Modelo intervalar/conservador exige outro aceite. |
| Estimado (inclui derivação que o utiliza) | Se política o exclui, `DadosIncompletos/EstimativaExcluida`. Se aceito, propagar origem/hipótese e aviso até resultado/rascunho. |
| Método/modalidade/contexto/base não convertível | `DadosIncompativeis`, sem fallback para perfil genérico nem conversão semântica. |
| Nutriente sem meta e ausente | Não bloqueia N1; resultado daquele componente continua parcial/indeterminado no diagnóstico, sem alegação de laudo completo. |

Maturidade da OPERACIONAL-REAL não foi consultada: nenhum parâmetro ou meta real é necessário para este contrato. Prontidão real exige, futuramente, responsável/fonte e cobertura da matriz dos candidatos escolhidos; se for solicitada avaliação, fazer leitura identificada, agregada e sanitizada em Tarefa própria, sem publicar registros/configuração/imagens ou alterar a base.

## 4. Modelo matemático N1

`i=1..n` candidatos incluídos; `j=1..k` metas. `x_i` é kg BN; `a_ji` é g/kg BN para massa ou MJ/kg BN para energia; `L_j/U_j` são concentrações nas mesmas unidades. Percentuais são divididos por 100 ao formar as frações.

```text
minimizar D = Σ t_i                                  [kg]
sujeito a Σ x_i = M                                  [kg BN]
           M*l_i/100 ≤ x_i ≤ M*u_i/100                [kg BN]
           L_j*M ≤ Σ a_ji*x_i ≤ U_j*M                [g ou MJ]
           t_i ≥ x_i-r_i; t_i ≥ r_i-x_i; t_i ≥ 0
```

Omitir somente lado de meta não informado, nunca uma meta falha. Linhas explicitamente excluídas têm `x_i=0` e desvio `r_i` constante. `D/2` é massa redistribuída, pois ambas as misturas somam M. Mostrar D e D/2 com rótulos diferentes. Sem pesos nutricionais/econômicos ocultos, metas flexíveis ou relaxamento automático. Se a referência já atende e é representável, D=0. O objetivo não encontra a composição “mais nutritiva”; minimiza apenas a mudança declarada.

Múltiplos ótimos são possíveis. Ordenar candidatos/linhas por ID canônico, fixar versão/opções/seed e uma thread; não acrescentar pequenos custos artificiais para desempatar. Não prometer unicidade nem identidade binária entre plataformas. O snapshot preserva a resposta efetivamente aceita; replay não roda novamente. Desempate lexicográfico, se necessário operacionalmente, exige escolha explícita posterior.

Verificações: intervalo individual mal formado é dado incompatível; soma de mínimos >100 ou soma de máximos <100, com dados completos, é contradição demonstrável. Fazer diagnóstico de dados antes de alegar inviabilidade global. Sem fallback silencioso para problema com menos ingredientes/metas.

## 5. Economia e estoque: fronteiras deliberadas

N1 não exige custo e não afirma economia. Custo desconhecido não impede um ajuste nutricional com dados completos; a saída econômica fica `NaoAvaliado`, sem total zero. Pedir modo econômico/disponibilidade no endpoint N1 deve retornar resultado `DadosIncompativeis/ModoForaRecorte`, sem executar outro objetivo por conta própria.

**Modo econômico futuro E1, não autorizado por P5.1:** objetivo distinto `min Σ c_i*x_i`, com as mesmas metas duras; eventual desvio como segundo objetivo também exige decisão. `c_i` teria de vir de fonte F04 **completa e comprovada**: com `K_i` em centavos e quantidade coberta `Q_i` em kg, `c_i=(K_i/100)/Q_i` BRL/kg, razão exata. Congelar camada/lote/posição, evidência opaca autorizada, cortes físico/monetário, revisões e conversões pertinentes. Não usar referência de preço, Caixa, unitário arredondado, parcela conhecida de custo parcial, custo ausente como zero ou média entre camadas.

Antes de E1, definir com o responsável: quais quantidades a evidência cobre; limite que impede extrapolar a quantidade comprovada; seleção de múltiplas camadas; se disponibilidade efetiva deve limitar o modelo; validade temporal da fonte; e comparação proporcional versus custo da retirada. F04 calcula retiradas com piso de centavos e resíduo até a última parcela. Assim, `c_i*x_i` é custo **proporcional modelado a partir de fonte comprovada**, não reprodução exata nem garantia de mínimo custo contábil da execução. A fixture A=1/B=3 BRL/kg e total200 é oráculo sintético desse objetivo, não custo dos registros reais.

Não há razão demonstrada para adotar inteiros para resolver essa diferença de centavos agora. Se a demanda for minimizar o custo exato de retiradas F04, especificar esse outro problema, sua regra de ordenação/fracionamento e validação antes de escolher LP/MILP.

Disponibilidade futura exigiria corte consistente de posições elegíveis, saldo físico/reservado/disponível, lotes/locais e suas revisões, validade/data operacional e reservas com IDs/revisões/quantidades. **Sequência física sozinha não congela reservas**: início F02 reserva sem gerar movimento físico. Leitura não cria reserva nem garante saldo futuro; F02 continua revalidando e reservando somente em comando humano de início. F05 nunca confirma ordem, baixa saldo ou lança Caixa.

## 6. Precisão, recursos e verificação independente

Valores documentais e modelo lógico usam decimais/racionais exatos; adapter nativo usa ponto flutuante. Congelar ambos e o escalonamento. Para evitar números extremos do catálogo destruindo o modelo, normalizar massa para `p_i=x_i/M`, concentração por linha usando `s_j=max(1,|a_ji|,|L_j|,|U_j|)` e desvio por M; guardar fatores e verificar o modelo original no final. Não eliminar coeficiente pequeno silenciosamente: se a conversão/limiar nativo apagar ou tornar não finito algum coeficiente relevante, retornar `DadosIncompativeis/EscalaNumericaNaoSuportada`.

Proposta inicial de limites técnicos (não parâmetros nutricionais): 50 candidatos incluídos, 50 metas, uma execução nativa simultânea por instância; 5 s de orçamento total de solver/diagnóstico e watchdog externo de 10 s. Prazo de leitura/transação é distinto do orçamento do solver. Capacidade excedida retorna erro transitório sem fingir dados ausentes. Limites/configuração versionados; aumentá-los exige evidência de carga, não novo requisito alimentar.

Propor HiGHS simplex, uma thread, seed0, tolerâncias primal/dual `1e-8` na matriz escalada; opções efetivas e verificações de residual devem ser confirmadas no adapter em P5.1. Tolerância do solver **não flexibiliza metas do usuário**. Resíduo numérico aceito pelo nativo não basta para transferir uma receita.

Quantização proposta para transferência: converter a solução bruta finita em representação decimal reproduzível, guardar seus resíduos; distribuir `M*10^6` microkg pelo piso de cada parcela e maiores restos fracionários, desempate por ID ordinal canônico. Somente executar essa distribuição se a soma bruta estiver dentro da tolerância de balanço escalada `1e-8`; fora dela é falha numérica, não licença para normalizar. Não cortar massas negativas: qualquer massa bruta negativa, ainda que tolerada pelo nativo, rejeita o candidato (`ErroAlgoritmo/MassaNegativa`). Candidato bruto fora de outro limite além da tolerância é rejeitado; a verificação quantizada posterior não admite violação alguma.

Guard adicional obrigatório: `K=M*10^6−Σfloor(x_i*10^6)` deve ser inteiro e `0≤K≤n_e`, em que `n_e` conta somente parcelas incluídas com resto fracionário positivo. Distribuir no máximo um microkg por parcela elegível; candidato excluído U=0 não recebe resíduo. Balanço relativo tolerado sozinho não garante esse guard, especialmente para M grande. Se falhar, `Inconclusivo/PrecisaoIncompativel`, sem normalização ou redistribuição iterativa de massa.

Depois da quantização, recalcular **exatamente** massa, inclusões, todas as metas e D usando os coeficientes racionais originais. Massa deve fechar M e nenhum limite duro pode ser violado, mesmo por menos de uma casa exibida. Usar inteiros/racionais de precisão suficiente, sem overflow intermediário de `decimal`. Resultado quantizado aprovado pode ser viável sem ser ótimo na malha de seis casas; mostrar objetivo bruto e objetivo após quantização, sem alegar ótimo discreto. Qualquer resíduo exige prévia e aceite humano antes do rascunho. Não alterar Q01/ToEven de movimentos físicos F01.

Se a quantização falhar, `Inconclusivo/PrecisaoIncompativel`: preservar candidato contínuo, violações e diagnóstico; bloquear rascunho. Não reparar metas nem mover massa ocultamente; usuário pode formular novo pedido. `B=1/3` em 1kg ilustra solução contínua que não cabe exatamente em seis casas. Não declarar inviabilidade contínua a partir disso.

Inviabilidade demonstrada exige prova verificável independentemente do status nativo: contradição analítica válida no modelo completo ou certificado de Farkas conferido contra a matriz racional original (converter igualdades/bounds para `Ax≤b`; `y≥0`, `yᵀA=0`, `yᵀb<0`). Reconstrução de certificado nativo que não fecha exatamente não é prova; retornar `Inconclusivo/InviabilidadeNaoCertificada`. IIS ajuda a explicar, mas não equivale por si só a certificado validado. Não implementar um solver exato próprio para contornar esse gate.

## 7. Estados e conteúdo dos resultados

`EmCalculo` é estado transitório do recurso persistido, separado do desfecho terminal. GET não recalcula. Todos os desfechos terminais preservam o pedido, os dados resolvidos ou faltantes e a evidência do diagnóstico.

| Desfecho | Significado / ação permitida |
| --- | --- |
| `SolucaoViavel` | Candidato quantizado revalidado contra todas as restrições. Informar se objetivo contínuo terminou ótimo numericamente ou apenas viável; não certificar dieta. Único desfecho que habilita solicitar rascunho em N1. |
| `InviabilidadeDemonstrada` | Modelo completo e incompatibilidade provada; mostrar certificado/contradição e restrições envolvidas. Sem candidato ou receita. |
| `DadosIncompletos` | Coeficiente, fonte ou derivação obrigatória ausente/excluída; matriz de lacunas. Não significa inexistência de solução. |
| `DadosIncompativeis` | Semântica/unidade/contexto/recorte/intervalo individual inválidos. Identificar campo e motivo; nenhuma equivalência inferida. |
| `LimiteTempo` | Orçamento/watchdog atingido, com ou sem candidato. Se existir candidato, revalidar e rotular somente como diagnóstico, sem ótimo nem botão de rascunho em N1. Novo pedido exige ação explícita. |
| `ErroAlgoritmo` | Falha nativa, carga de biblioteca, NaN/overflow, crash ou execução interrompida; código seguro e correlação, sem stack/path privado. Não confundir falha de leitura com falta de dado. |
| `Inconclusivo` | Status ambíguo, limite de iterações, inviabilidade sem prova ou precisão de transferência incompatível; motivo específico. Não promover a sucesso/inviabilidade. |

Modelo N1 com limites finitos é limitado; status nativo `Unbounded` indica defeito de modelo/adapter (`ErroAlgoritmo`), não oportunidade nutricional. `UnboundedOrInfeasible` é inconclusivo. Exceder limite de iterações não é timeout sem evidência temporal.

Saída mínima: massas BN e inclusões por candidato (incluindo zeros); comparação referência×candidato e D; contribuição de cada ingrediente por componente, totais e concentrações; método/contexto/fontes/estimativas; cobertura e lacunas dos componentes não obrigatórios; valor de cada restrição, mínimo/máximo, folga na unidade original, violação e estado. Marcar ativa exata quando folga racional zero e próxima da fronteira por limiar numérico separado e identificado; proximidade não altera aceite. Mostrar limites de inclusão e balanço de massa, não só metas nutricionais.

`qualidadeObjetivo` distingue `OtimoNumericoContinuo`, `ViavelSemProvaDeOtimo` e `ViavelAposQuantizacao`; guardar status original, resíduos, valor primal/dual quando fornecidos e diferença entre objetivos. Não afirmar que folga ou dual mede segurança alimentar. Mensagem permanente: **“Ótimo matemático não certifica uma dieta; confira fontes, aplicabilidade e avaliação do responsável.”**

## 8. Snapshot, autorização e criação explícita de receita

Snapshot tipado imutável próprio: versão do contrato/modelo/normalização/quantização/validador; aplicação/commit; autor/UTC; payload canônico e hash; baseline e receita/versão/revisões; candidatos incluídos/excluídos, limites e justificativas; cópias dos itens/perfis/observações/definições e fontes autorizadas; metas, origem, contexto e políticas; matriz racional e nativa, fatores de escala; solver/versão/hash do binário/plataforma/options/seed; limites/prazos e tempos reais; status bruto, prova e validação; solução bruta/quantizada/resíduos/contribuições/folgas. Hashes de payload, entradas resolvidas, modelo e resultado têm funções distintas e identificadas.

Corte N1 = instante UTC de leitura consistente + IDs/estados/revisões congelados. `corteFisico`, `corteMonetario` e reservas são **não consultados**, não zero. Fontes técnicas e documentos privados permanecem referências opacas sob autenticação; não copiar URLs privadas, documentos, dados pessoais ou configuração para logs/exportações compartilhadas. O snapshot operacional é dado autenticado, nunca fixture/seed/arquivo público de Git.

Autenticado pode preparar/executar/consultar o recurso, como simulações atuais; nenhum novo papel/ACL por propriedade. A criação de rascunho segue as permissões e validações vigentes de receita, com sessão atual revalidada. Todos os comandos persistidos usam chave idempotente; replay reproduz resultado congelado, sem nova execução, inclusive se perfis/catálogo mudaram. Alteração exige novo pedido/chave, ligado ao anterior.

Executar otimização não cria simulação, receita, versão, publicação, estoque ou ordem automaticamente. Ação humana separada “Preparar rascunho desta solução” mostra prévia, fontes, resíduos e destino (nova receita ou nova versão rascunho). Comando de confirmação explícito revalida sessão, estado/revisões e referências atuais sob protocolo de catálogo; mudança/inativação retorna conflito e pede nova conferência, preservando o resultado histórico. Copiar somente linhas `x_i>0`, pois o serviço atual rejeita quantidade zero; manter excluídas/zeros no snapshot de origem. Usar quantidades absolutas kg, sem forçar percentuais arredondados a100. Preservar contexto/metas/fontes por vínculo próprio F05→RecipeVersion que permite consultar o snapshot: `RecipeData` atual não tem esses campos, e não se deve embutir JSON em `Observacao`. Validar saída simples pelo domínio existente. Publicada nunca é sobrescrita nem publicada automaticamente. A edição posterior do rascunho cria suas próprias revisões e não muda o resultado F05.

## 9. Aceite e conclusão da P5.0

O responsável deve aceitar/ajustar D05-01–D05-12 e identificar a revisão aceita. Autorizar P5.1 N1/B1–B6 é ato explícito separado, que pode constar da mesma mensagem. Fontes/metas reais serão fornecidas por execução, sem converter aprovação do contrato em aprovação de uma dieta. Se o responsável preferir objetivo econômico, estoque, MS ou inteiros desde a primeira entrega, revisar este contrato e o plano antes de implementar.

Esta P5.0 só entrega documentos, commit/push/PR e evidências. Não instala dependência, não implementa algoritmo/migration/seed, não acessa ou altera a base real, não reinicia a aplicação em uso, não integra a PR #37 e não inicia F06/#493/#509. A #525 vai para **Em validação**, com aceite/revisão/merge pendentes; não para Concluído.
