# Otimização assistida de formulações

A F05 ajusta as massas dos ingredientes de uma mistura de referência para atender às metas e aos limites informados. O modelo `f05-n1/1` mantém a massa total em base natural (BN) e minimiza a soma das diferenças absolutas entre as massas da referência e da solução. Dividir essa soma por dois dá a massa redistribuída. A implementação segue o contrato aceito na revisão `2b71a71d76c41f61de9fc6cf806d3ee4ea6c4d34`. [Tarefa #526](https://devops-lab.tailaf9418.ts.net/issues/526) · [Contrato aceito](https://github.com/MarcosRCEng/GenSW/blob/2b71a71d76c41f61de9fc6cf806d3ee4ea6c4d34/docs/superpowers/specs/2026-11-09-f05-otimizacao-525-design.md)

## Preparar a mistura e as metas

1. Em **Formulação e comparação**, abra **Otimização assistida** e selecione **Nova otimização**. O histórico fica em `/producao/formulacao/otimizacoes` e permite filtrar por desfecho, espécie, fase e período.
2. Informe a massa do lote em kg ou g. Identifique a formulação manual ou escolha uma versão publicada e use **Importar composição para a massa informada**. A referência importada conserva as massas e os limites da escala escolhida. A soma das massas de referência precisa coincidir exatamente com o lote.
3. Selecione itens alimentares ativos, aptos para entrada e com unidade canônica kg. Escolha a versão publicada do perfil de cada ingrediente incluído. Cada item aparece uma vez. Um ingrediente acrescentado à receita importada começa com massa de referência zero. Confira a inclusão em percentual da massa BN. Os limites padrão são 0% a 100%. Justifique alterações dos limites importados e exclusões explícitas com inclusão máxima zero.
4. Informe espécie e fase quando aplicáveis. O preparo comum é opcional. Quando preenchido, precisa coincidir com o preparo registrado em cada perfil incluído. Em branco, o campo permite preparos distintos entre ingredientes.
5. Adicione as metas BN com componente, método, contexto, unidade e pelo menos um limite. Para meta informada pelo usuário, registre a justificativa. Para referência técnica, indique fonte, edição, seção, data e aplicabilidade. Para aceitar uma estimativa, marque a opção na meta e informe a fonte e a justificativa, incluindo a hipótese. Use parâmetros escolhidos pelo responsável para o caso em avaliação.
6. Clique em **Revisar coeficientes**. A prévia mostra os dados e os motivos de incompatibilidade ou lacuna por ingrediente e meta. Confira as fontes, os limites e o contexto antes de clicar em **Executar otimização**. A execução faz nova conferência das revisões e congela o pedido aceito.

O recorte admite até 50 ingredientes incluídos, 100 linhas no total e 50 metas. O modelo usa massas contínuas em kg e uma saída alimentar de mesma massa. O escopo é mistura simples. Processamento, retenção, perdas, sub-receitas, embalagem, consumíveis, volume e contagem pertencem ao fluxo manual ou a recortes posteriores. Economia aparece como `NaoAvaliado` e disponibilidade física como `NaoConsultada`. A reserva de estoque é uma ação da ordem de produção. [Catálogo e formulação](catalogo-formulacao.md), [ordens de produção](producao-f02.md).

## Coeficientes e precisão

O solver usa coeficientes pontuais e compatíveis para todas as metas de cada ingrediente incluído. Um zero explícito e documentado é utilizável. Valor desconhecido, não aplicável, faixa, mínimo, máximo e abaixo do limite de detecção recebem diagnóstico. A matriz conserva a origem, a observação, a fonte e a hipótese quando disponíveis. A política padrão exclui estimativas e permite aceite por meta. A origem estimada da matéria seca também se propaga quando ela participa da conversão.

As concentrações do modelo são normalizadas em g/kg BN ou MJ/kg BN. A conversão de kcal para MJ preserva a modalidade energética. Componente, método e contexto seguem a correspondência exata do perfil. Energia exige também a mesma espécie e fase do pedido. Um valor expresso em matéria seca pode alimentar uma meta em BN quando a fração seca aplicável da mesma amostra está documentada. O resumo de composição pode mostrar matéria seca e cobertura como diagnóstico. As metas do modelo são em BN.

O cálculo conserva coeficientes racionais, escalona a matriz para o HiGHS e verifica a solução recebida. A quantização para transferência distribui microkg por maiores restos com desempate por ID. O servidor confere exatamente massa, inclusões e metas nas quantidades quantizadas. Confira o candidato bruto, o quantizado, os resíduos, o desvio e as folgas no detalhe. Uma diferença exibida pequena continua sendo relevante se violar um limite. Uma solução contínua pode terminar inconclusiva quando suas quantidades quantizadas deixam de cumprir a meta.

**Ótimo matemático não certifica uma dieta.** Confira fontes, aplicabilidade e avaliação do responsável. A solução preserva as hipóteses do cálculo e indica se houve ótimo numérico contínuo, viabilidade sem prova de ótimo ou viabilidade após quantização.

## Resultados e rascunho

Uma execução aceita começa em `EmCalculo` e passa a `Finalizada` com um desfecho. O detalhe apresenta referência e candidato, contribuições, restrições com folgas, prova quando disponível, corte e versões registradas.

| Desfecho | Como interpretar |
| --- | --- |
| `SolucaoViavel` | As quantidades transferíveis atendem às restrições com os dados congelados. É possível preparar um rascunho. |
| `InviabilidadeDemonstrada` | A prova analítica ou o certificado verificado demonstra uma contradição no modelo. Confira as restrições identificadas. |
| `DadosIncompletos` | Uma fonte, coeficiente ou derivação requerida está ausente ou excluída pela política. Complete os dados e prepare novo pedido. |
| `DadosIncompativeis` | Unidade, contexto, modalidade, limite individual, escala numérica ou recorte são incompatíveis. Corrija o campo apontado. |
| `LimiteTempo` | O orçamento de cálculo ou a supervisão por tempo interrompeu a execução. Um candidato exibido serve como diagnóstico. |
| `ErroAlgoritmo` | O cálculo, o processo filho ou a finalização falhou. Use o código e a correlação sanitizados para investigar. |
| `Inconclusivo` | A execução terminou sem evidência suficiente de inviabilidade ou sem solução transferível. Consulte o motivo específico. |

Em uma solução viável, selecione **Preparar rascunho desta solução**. Escolha o item de saída alimentar ativo, produzido em kg, e o destino da versão. Para nova receita, informe código, nome e finalidade. Selecione **Visualizar prévia do rascunho**, confira as quantidades e aceite os avisos e resíduos apresentados. **Confirmar criação do rascunho** abre o editor da nova versão com o vínculo para o resultado F05.

Somente as massas positivas entram na versão criada. O histórico conserva os demais candidatos. O servidor revalida revisões, saída, referências e hash da prévia na confirmação. Em caso de conflito, confira as referências e prepare uma nova prévia ou um novo cálculo conforme o diagnóstico. A publicação é uma ação posterior no fluxo de receitas.

## API e recuperação

As rotas usam autenticação existente e o prefixo `/api/v1/otimizacoes-formulacao`.

| Método e caminho sob o prefixo | Resposta |
| --- | --- |
| `POST /previas` | 200 com a matriz de coeficientes e os diagnósticos do pedido. A prévia é leitura. |
| `POST /` | 201 com a execução terminal e `Location`. Uma chave que já identifica execução em curso retorna 202 com `Location` e `Retry-After`. |
| `GET /` | 200 com histórico paginado. Use `page`, `pageSize`, `sortDirection`, `desfecho`, `especieId`, `fase`, `de` e `ate`. A página padrão tem 25 registros e o máximo é 100. |
| `GET /{id}` | 200 com a execução e seu estado atual. |
| `GET /por-chave?chave=…` | 200 com a execução da chave para o autor autenticado ou 404 quando ausente. |
| `POST /{id}/previas-rascunho` | 200 com o conteúdo e o hash da prévia, as revisões conferidas e os avisos. A prévia é leitura. |
| `POST /{id}/rascunhos` | 201 com recibo, IDs da receita e da versão criada e `Location`. |
| `GET /{id}/rascunhos/por-chave?chave=…` | 200 com o recibo da chave para o autor autenticado ou 404 quando ausente. |
| `GET /rascunhos/por-versao/{versaoId}` | 200 com o vínculo da versão à otimização ou 404 para versão sem derivação. |

Os comandos de execução e de criação de rascunho exigem `Idempotency-Key` de até 100 caracteres, por autor e operação. A mesma chave com o mesmo pedido recupera o recurso aceito. Mudar o pedido e reutilizar a chave gera 409. Envie massas, limites e concentrações do pedido como strings decimais invariantes, com ponto e até seis casas. Desfechos do domínio aparecem no corpo da execução terminal. Formato inválido retorna 400. A sessão é exigida com 401 quando inválida, autorização insuficiente retorna 403 e referência inexistente retorna 404. Revisão ou prévia obsoleta retorna 409. Capacidade ocupada retorna 429 com `Retry-After`. Indisponibilidade da persistência pode retornar 503.

Se perder a resposta ou receber erro transitório, use **Consultar o pedido** ou **Consultar o rascunho** antes de reenviar. A interface preserva a chave na sessão do navegador. Quando o servidor ainda não encontra a chave e o formulário conserva o corpo original, **Reenviar o mesmo comando** recupera ou completa a solicitação com a mesma chave. Com um ID conhecido, abra o detalhe. Para mudar dados ou repetir depois de um desfecho terminal, prepare um novo pedido explicitamente.

## Processo de cálculo, instalação e recuperação

O backend usa HiGHS simplex 1.15.1 em processo filho .NET 8 com pipes locais. Os artefatos devem incluir o worker, o binário nativo verificado para Windows ou Linux x64 e os avisos de licença. O cálculo registra versão, hash do binário, plataforma e parâmetros efetivos quando o solver roda. A configuração usa uma thread, seed zero, orçamento de 5 s para diagnóstico e solver e supervisão de 10 s para o processo filho. Cada instância admite um cálculo em curso. [Empacotamento e licenças](../../src/Backend/GenSW.OptimizationWorker/ThirdParty/README.md).

A captura grava pedido, revisão das referências, entradas resolvidas e modelo em uma transação curta antes do cálculo. O prazo durável é de 30 s a partir da captura. A recuperação no startup e a verificação periódica a cada 5 s encerram execuções vencidas com `ErroAlgoritmo/ExecucaoInterrompida`. O token de propriedade e a transição atômica limitam a execução a um resultado terminal. Consulte esse resultado pelo ID ou pela chave original. Para tentar de novo, envie um pedido explícito com nova chave.

A migration aditiva `20261112205407_AddFormulationOptimization` vem após `20261103233403_AddMaterialCosts` e acrescenta `ExecucoesOtimizacao`, `ResultadosOtimizacao` e `DerivacoesOtimizacao`. As estruturas guardam snapshots, idempotência, estado de execução e vínculo do rascunho. As restrições de integridade protegem a transição de estado e a imutabilidade dos resultados e derivações. [SQL idempotente a partir de F04](../validation/evidence/526/optimization-idempotent.sql).

Antes de executar a versão com F05 para validação, confirme o destino e o histórico em `__EFMigrationsHistory`. Revise o SQL de todas as migrations pendentes e aplique no PostgreSQL isolado de validação. Quando o roteiro atingir cadastros com imagens, confira a raiz privada exata, as chaves e os arquivos, o acesso autenticado ao original e à miniatura e a renderização na interface. Aplicação de migrations na OPERACIONAL-REAL ou em ambiente compartilhado exige autorização específica.

Para recuperar um ambiente com dados, prefira correção aditiva ou restauração coordenada de banco e imagens em destino separado. Preserve execuções, resultados e vínculos do período posterior ao backup para conferência antes de trocar o ambiente. O `Down` só permite remover as três estruturas F05 enquanto estiverem vazias. Mantenha referências privadas e credenciais fora de logs e evidências compartilhadas.
