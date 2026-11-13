# F05 — matriz sintética e oráculos de aceite

P5.0 [#525](https://devops-lab.tailaf9418.ts.net/issues/525), vinculada à [#430](https://devops-lab.tailaf9418.ts.net/issues/430), em 09/11/2026, America/Sao_Paulo. Complementa o [contrato proposto](2026-11-09-f05-otimizacao-525-design.md), o [quadro de decisões](2026-11-09-f05-otimizacao-525-decisions.md) e o [plano P5.1 condicionado](../plans/2026-11-09-f05-otimizacao-525-implementation.md). **Todos os dados abaixo são fictícios. A matriz especifica verificações futuras; não relata testes de solver executados, parâmetros reais ou seeds.**

## 1. Fixtures e problema de referência

Reutilizar os exemplos A/B do [desenho #413, §8](2026-10-05-producao-insumos-produtos-413-design.md) e do [plano #430, §5](../plans/2026-10-07-continuidade-f01-f06-430-implementation.md). A/B representam itens alimentares fictícios, ativos, com unidade canônica `kg`, capacidade de entrada em produção e perfis publicados explicitamente selecionados. Os métodos e o contexto da energia são os mesmos nos dois perfis; o nome EM é apenas a modalidade escolhida para esta fixture.

| Coeficiente pontual conhecido | A | B | Unidade/base |
| --- | ---: | ---: | --- |
| Proteína bruta (PB) | 100 | 400 | g/kg BN |
| Fibra bruta | 20 | 80 | g/kg BN; mesmo método |
| Energia metabolizável (EM) | 12 | 8 | MJ/kg BN; mesmo contexto explícito |
| Umidade | 10 | 20 | % da massa BN; mesma amostra do perfil |
| Fração de matéria seca derivada | 0,9 | 0,8 | kg MS/kg BN |

Salvo indicação contrária: `M=100 kg BN`, baseline `r_A=60 kg`, `r_B=40 kg`, limites de inclusão de cada candidato entre 0% e 100%, e metas sintéticas PB≥250 g/kg BN, EM≥10 MJ/kg BN e fibra≤50 g/kg BN. As fontes dessas metas são a própria fixture, identificada como `InformadaUsuario`; não representam necessidades de espécie ou prescrição profissional.

O primeiro recorte proposto ajusta a baseline e minimiza `D=|x_A-r_A|+|x_B-r_B|` em kg BN, sujeito a `x_A+x_B=M`, metas duras e limites explícitos. Candidato novo pode ter `r_i=0`; a baseline completa precisa somar `M`. Não há objetivo econômico, penalidade de nutriente, restrição física de disponibilidade, reserva ou variável inteira nesse recorte. Identificadores sintéticos estáveis A/B/C, em ordem ordinal, serão definidos nas fixtures de teste; a ordem de um array não constitui prioridade nutricional.

Com `b=x_B`, em kg, e `M=100`:

```text
x_A = 100-b
PB_BN    = 100+3b       g/kg BN
EM_BN    = 12-0,04b     MJ/kg BN
Fibra_BN = 20+0,6b      g/kg BN
MS_total = 90-0,1b      kg MS
```

F1=A60/B40 tem PB=220 g/kg BN, fibra=44 g/kg BN, EM=10,4 MJ/kg BN e MS=86 kg. Sua PB na MS é `22000/86=255,813953... g/kg MS`; esse resultado de consulta não transforma uma meta MS em meta BN. F2=A30/B70 tem PB=310, fibra=62, EM=9,2 e MS=83 kg. F1 falha PB; F2 falha energia e fibra. Nenhuma atende o conjunto original.

As três metas originais exigem, respectivamente, `b≥50`, `b≤50` e `b≤50`. Logo A50/B50 é a única composição viável, com PB=250 g/kg BN, EM=10 MJ/kg BN, fibra=50 g/kg BN e MS=85 kg. As três metas estão ativas. As contribuições devem mostrar PB A=5000 g/B=20000 g, EM A=600 MJ/B=400 MJ e fibra A=1000 g/B=4000 g. A solução tem `D=20 kg` para a baseline F1, `D=40 kg` para F2 e `D=0 kg` para uma baseline A50/B50.

## 2. Matriz de cenários

Os estados abaixo são resultados de domínio, separados de erros HTTP de formato, autenticação e conflito. Uma prova de inviabilidade precisa abranger o problema completo e seus dados elegíveis; um status bruto do solver não é essa prova. Resultados e rascunhos nunca alteram estoque, custo F04, receita publicada ou simulação antiga.

| ID | Cenário / alteração à fixture | Oráculo obrigatório | Verificação futura |
| --- | --- | --- | --- |
| S05-01 | Baseline F1=A60/B40, metas originais | `SolucaoViavel`: A50/B50, `D=20 kg`, contribuições acima, três metas ativas e todas as demais restrições conferidas depois da quantização. Nenhum selo de dieta. | Domínio, adaptador, API e interface |
| S05-02 | Baseline F2=A30/B70, mesmas metas | Mesma composição A50/B50, `D=40 kg`. Não afirmar que F2 é melhor por conter mais PB antes da otimização. | Domínio e API |
| S05-03 | Baseline A50/B50, mesmas metas | A50/B50, `D=0 kg`. Limite inferior global de `D` é zero: certificado simples de ótimo; não criar variação automaticamente. | Domínio, API e interface |
| S05-04 | Aumentar EM mínima para 10,1 MJ/kg BN, mantendo PB≥250 | `InviabilidadeDemonstrada`: PB exige B≥50 kg e energia B≤47,5 kg. Mostrar as duas restrições e a contradição; não devolver A50/B50 como sucesso aproximado. | Domínio/prova independente e API |
| S05-05 | PB mínima de 450 g/kg BN | `InviabilidadeDemonstrada`: combinação convexa de 100/400 não ultrapassa 400. Mostrar limite calculado e fontes completas. | Domínio/prova e interface |
| S05-06 | B≤20%, PB≥250; demais metas podem ser retiradas para isolar a prova | `InviabilidadeDemonstrada`: PB máxima `0,8×100+0,2×400=160 g/kg BN`. Não alterar o limite de B para satisfazer a meta. | Domínio/prova e API |
| S05-07 | PB mínima 260 e máxima 250; ou inclusão mínima de B=30% e máxima=20% | `DadosIncompativeis`: limites contraditórios no próprio campo/conjunto; localizar o erro antes do solver. Distinguir de inviabilidade entre restrições individualmente válidas. | Validação, API e interface |
| S05-08 | Inclusão mínima A=60% e B=60%, máximas=100% | `InviabilidadeDemonstrada`: mínimos somam120% e exigem120 kg para M100. Dados individuais são válidos e completos; a soma prova a impossibilidade. | Domínio/prova e API |
| S05-09 | PB de B `Desconhecido`, com limite superior de B positivo | `DadosIncompletos`: identificar B/perfil/componente/fonte faltante. Não usar zero, média, PB de outra versão ou eliminar B silenciosamente. O total PB de F1 é indeterminado; contribuição conhecida6000 g e cobertura60%. | Domínio, pré-validação e interface |
| S05-10 | Mesmo dado ausente; responsável exclui B explicitamente com mínimo=máximo=0 antes da elegibilidade, baseline A100/B0, e usa PB mínima100/EM mínima10/fibra máxima50 | B fica registrado como excluído, fora da matriz ativa. A100 é `SolucaoViavel`; nenhum coeficiente ausente de B é fabricado. Se B tiver mínimo positivo, a declaração é incompatível. | Domínio, snapshot e interface |
| S05-11 | PB de B `NaoAplicavel`, valor null e motivo, B com máximo positivo | `DadosIncompativeis`/`CoeficienteNaoAplicavel`: o coeficiente não pode satisfazer a meta alimentar. Não equiparar `NaoAplicavel` a zero conhecido nem remover sua massa do denominador. | Domínio e API |
| S05-12 | PB de B pontual400, origem `Estimado`, política padrão sem estimativas | `DadosIncompletos`/estimativa excluída: a política impede usar o coeficiente, apesar do número presente. Não afirmar inviabilidade do universo declarado. | Domínio, API e interface |
| S05-13 | Mesmo perfil estimado, aceite explícito para PB e hipóteses/fontes identificadas; demais coeficientes completos | A50/B50 pode ser `SolucaoViavel`, com PB completa estimada e hipótese visível. O aceite vale para PB e sua cadeia de derivação explicitamente mostrada, sem autorizar estimativas de outras metas nem ocultar MS/conversão utilizadas. Nova política exige nova execução; snapshot anterior permanece. | Domínio, snapshot e interface |
| S05-14 | Energia de B é EB, ou EM com espécie/fase/contexto incompatível com a meta | `DadosIncompativeis`: apontar as chaves divergentes. Conversão MJ↔kcal da mesma modalidade não converte EB/ED/EM nem supre contexto ausente. | Domínio e interface |
| S05-15 | Coeficiente PB de B expresso em MS; MS da mesma amostra é conhecida e compatível; depois repetir com MS desconhecida/estimada excluída | Normalização BN usa `c_BN=c_MS×d` com proveniência congelada; deve reproduzir400 se a fixture usar500 g/kg MS e d0,8. Com MS ausente ou excluída: `DadosIncompletos`; não usar umidade típica. A aceitação de estimativas precisa cobrir toda a cadeia usada. | Domínio/normalização, API e snapshot |
| S05-16 | Solicitar meta em MS, candidato canônico L/un, processamento, sub-receita, embalagem ou objetivo econômico | `DadosIncompativeis`/`ModoForaRecorte`, com motivo específico. Não arredondar contagens, converter com fator presumido nem expandir ancestrais. A mesma funcionalidade já existente na simulação manual permanece preservada. | Validação, API e regressão |
| S05-17 | Custo F04 de B desconhecido; executar modo nutricional autorizado e tentar modo econômico | Nutricional não precisa nem inventa custo. Pedido econômico em P5.1 recebe `ModoForaRecorte`. Em extensão futura, desconhecido bloquearia o objetivo econômico e preço de referência não seria fallback comprovado. | Contrato/API; extensão econômica separada |
| S05-18 | M1kg, baseline A1/B0, somente PB≥200; perfil A/B original | Ótimo contínuo B=1/3, A=2/3, `D=2/3 kg`. Quantização balanceada a6 casas produz A0,666667/B0,333333; PB199,999900 viola a meta. `Inconclusivo`/`PrecisaoIncompativel`, sem botão de rascunho ou relaxamento oculto. | Domínio/quantização, adaptador e interface |
| S05-19 | S05-18 mais fibra≤40 g/kg BN | O LP contínuo exige B=1/3; nenhum par de microkg atende as duas metas: B0,333333 falha PB, B0,333334 gera fibra40,000040. Preservar a distinção entre LP contínuo e resolução transferível; `Inconclusivo`/`PrecisaoIncompativel`. | Prova/quantização e interface |
| S05-20 | Três candidatos: A com PB100, B/C com PB400; M1, baseline A1/B0/C0, somente PB≥250 | Há múltiplos ótimos: A0,5 e B+C0,5; `D=1 kg`. Aceitar qualquer composição quantizada que passe todas as restrições e prova de objetivo; mostrar não unicidade quando demonstrada. Não atribuir preferência técnica a B/C. Ordenação canônica, versão e parâmetros ficam registrados. | Domínio, adaptador e snapshot |
| S05-21 | Limite de tempo atingido antes de obter candidato; relógio/adaptador controlado na fixture | `LimiteTempo`, sem declaração de inviabilidade/ótimo, com duração e limite registrados. Não depender de máquina lenta ou fixture aleatória para reproduzir o timeout. | Adaptador, serviço, API e interface |
| S05-22 | Limite de tempo com incumbent recebido e validado independentemente | Resultado continua `LimiteTempo`; candidato e validação podem aparecer como diagnóstico, com ótimo não demonstrado. Neste recorte não liberar geração de rascunho. Candidato inválido não ganha status viável por existir. | Adaptador, validação e interface |
| S05-23 | Exceção nativa, NaN/infinito, dimensão errada ou vetor com resíduo que viola restrição | `ErroAlgoritmo`, código seguro/correlação e evidência técnica sanitizada. Nenhum sucesso, custo0, resposta de dieta ou gravação de receita. Distinguir falha do algoritmo de timeout controlado. | Adaptador, serviço e API |
| S05-24 | Solver anuncia infeasible, mas não há certificado independente validado; ou anuncia unbounded para o modelo limitado por soma/inclusão | Sem certificado: `Inconclusivo`, não `InviabilidadeDemonstrada`. Unbounded neste modelo limitado indica erro de montagem/adaptador e resulta em `ErroAlgoritmo`. Guardar diagnóstico sem expor stack/configuração privada. | Prova independente, serviço e API |
| S05-25 | Mesma chave/payload canônico após resposta perdida; chave igual/payload diferente; execução nova com chave nova | Replay devolve o mesmo resultado/snapshot e identidade, sem rodar solver novamente. Divergência retorna409. Uma nova execução explícita cria outro resultado; normalizar decimais semanticamente equivalentes e rejeitar IDs duplicados. | PostgreSQL, API e interface |
| S05-26 | Perfil/item/receita alterados depois de congelar entradas; consulta histórica e nova solicitação | Resultado anterior conserva entradas, metas/fontes, corte, exclusões, políticas, modelo, parâmetros, versão do solver e contribuições originais. Novo uso revalida elegibilidade; nunca recalcular histórico ao abrir a tela. Inativação não impede consultar evidência antiga. | PostgreSQL, serviço e interface |
| S05-27 | Resultado viável sem ação humana; depois ação explícita para gerar rascunho/variação vinculada | Só a ação explícita cria novo rascunho, com origem/referência do resultado e quantidades revalidadas. Receita publicada e snapshots anteriores permanecem idênticos. Publicação é decisão posterior própria; estado diferente de `SolucaoViavel` não habilita a ação neste recorte. | API, PostgreSQL, interface e navegador |
| S05-28 | Consultar resultado/saldo; repetir em anônimo; falha durante persistência; preservar módulos anteriores | Consulta de saldo não reserva, consome ou altera custos. Anônimo recebe401. Falha não deixa resultado parcial nem rascunho órfão. Em base sintética isolada, comparar fatos/snapshots F01–F04 e módulos anteriores, inclusive receita publicada; nenhuma fixture ou migration na OPERACIONAL-REAL. | PostgreSQL, autorização, regressão e navegador |

Variações obrigatórias de S05-09/11: (a) PB de B igual a zero **Conhecido/Pontual** e documentado é coeficiente válido; com PB mínima250 a inviabilidade é demonstrável pelo máximo100, não por dado faltante; (b) PB de B apenas Minimo/Maximo/Faixa/AbaixoDeteccao é `DadosIncompletos/CoeficienteNaoPontual`, sem usar o limite como teor exato. Variação de S05-08 com máximas A40%/B40% prova soma máxima80% insuficiente. Para quantização, exercitar também M grande com K fora das parcelas elegíveis e exclusão U=0: nunca distribuir mais de um microkg por parcela ou incluir ingrediente excluído para fechar massa.

## 3. Precisão e diagnóstico verificável

Para S05-18/19, a quantização balanceada opera sobre a massa total e não sobre percentuais exibidos: piso de cada `x_i` para microkg, seguido da distribuição dos microkg residuais por fração decrescente e identificador estável. Para `A=2/3` e `B=1/3`, os pisos somam0,999999 kg; A tem a maior fração e recebe o microkg restante. O fechamento exato de M não prova atendimento nutricional. Recalcular todas as metas e limites usando as quantidades finais e os coeficientes decimais/racionais congelados, sem tolerância oculta para transformar199,999900 em200.

O par A0,666666/B0,333334 alcança PB200,000200 e, se não houver a meta de fibra de S05-19, pode servir de diagnóstico para uma nova execução/decisão explícita. Ele não é reparo automático nem prova de que o primeiro resultado quantizado estava correto. Com fibra≤40, esse par também falha. Nenhuma dessas diferenças vira perda física, resíduo de estoque ou alteração de meta.

Tolerâncias do solver servem para avaliar o cálculo numérico e a atividade das restrições; não alteram os mínimos/máximos aceitos pelo responsável. A classificação de uma restrição como ativa deve apresentar valor, limite, folga e tolerância de atividade, além da verificação estrita do candidato transferível. Repetições com solver/versão diferentes podem escolher ótimos diferentes em S05-20; o snapshot identifica a execução efetivamente obtida e não promete uma composição única inexistente.

## 4. Oráculo econômico reservado para extensão posterior

O planejamento #430 prevê custos **sintéticos** A=1 e B=3 BRL/kg. Para A50/B50, `50×1+50×3=200 BRL`; preço médio2 BRL/kg. Esse cálculo fica preservado como oráculo de uma futura extensão econômica autorizada. Não integra o objetivo L1 do primeiro P5.1, não constitui custo operacional comprovado e não será seed.

Uma extensão futura precisa obter coeficientes a partir de evidências F04 compatíveis e completas, congelar a origem/corte e definir se limita massas à disponibilidade correspondente. Custo desconhecido não é zero, preço de referência ou média automática. Não somar custo do intermediário com seus ancestrais. Mesmo com solução matemática e custo completo, disponibilidade consultada não é reserva, e custo de materiais não inclui por inferência mão de obra, overhead, margem ou Caixa.

## 5. Evidência exigida na execução autorizada

Registrar por cenário o estado esperado/obtido, fonte sintética, modelo e versão, candidato contínuo quando houver, candidato quantizado, contribuições, limites/folgas, prova independente ou sua ausência e identidade do snapshot. Testes de serviço/adaptador usam relógio e falhas controlados; concorrência, unicidade, replay, atomicidade e preservação usam PostgreSQL real isolado. API e interface precisam mostrar estados distintos e a mesma evidência numérica do servidor. Casos não executados serão registrados como pendentes, nunca aprovados por inspeção documental.

Aceite humano do contrato e autorização do recorte precedem implementação e dependência. Esta P5.0 não instala solver, implementa código, cria migration/seed, inicia F06/#493/#509 ou altera a base real.
