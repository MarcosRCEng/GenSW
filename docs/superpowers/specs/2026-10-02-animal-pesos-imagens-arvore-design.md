# Animal — histórico de peso, imagens e árvore genealógica

Data: 2026-10-02. Estado: **proposta para aprovação humana; implementação não iniciada**.

Rastreabilidade: [Evolução #364](https://devops-lab.tailaf9418.ts.net/issues/364), [planejamento #365](https://devops-lab.tailaf9418.ts.net/issues/365). Base inspecionada: `origin/main`, commit `93fdb79` (integração da PR #14). Branch documental: `codex/365-planejamento-animal`.

## 1. Escopo e evidências

Adicionar histórico individual de pesagens, imagens de Animal e Variedade, seleção assistida de progenitores e uma árvore navegável. A filiação continua sendo a fonte da genealogia; não criar tabela de pedigree, relações a partir de Cruzamentos ou cópias de quantitativos reprodutivos. Não alterar a Tarefa #363. Reconhecimento visual, DNA, dispositivos, automações e rastreamento ficam fora do escopo.

Padrões efetivamente encontrados:

| Evidência no repositório | Consequência para o design |
| --- | --- |
| `GenSW.Domain/Animals/Animal.cs`: Guid, DateOnly opcional para nascimento, sexo, espécie, raça opcional, status e timestamps UTC | Reutilizar tipos, derivar idade e manter datas de fatos separadas de timestamps técnicos |
| `GenSW.Application/Animals/AnimalListQuery.cs` e serviços de listagem | Paginação `page/pageSize`, máximo 100, resposta `items/page/pageSize/totalItems/totalPages` |
| `GenSW.Domain/Animals/IdentificacaoAnimal.cs` e configuração no DbContext | Tipo controlado com descrição complementar e flag principal são precedentes adequados |
| `GenSW.Application/Animals/Filiacoes/FiliacaoAnimalService.cs` | Substituição inativa vínculo anterior em transação; valida sexo, ciclos e progenitor nos dois papéis |
| `GenSW.Infrastructure/Animals/Filiacoes/FiliacaoAnimalRepository.cs` | Lock somente do descendente; verificação de ciclos percorre relações; pedigree atual consulta por nó e só traz ascendentes |
| `GenSW.Application/Animals/AnimalService.cs` | Impede troca de sexo incompatível com filiação ativa; não coordena hoje todas as mutações genealógicas |
| `GenSW.Infrastructure/Persistence/GenSWDbContext.cs` | FK Restrict, índices, checks, pesos existentes decimal(10,2), migrations EF Core/PostgreSQL |
| `GenSW.API/Controllers/FiliacoesAnimaisController.cs` | API autenticada em `/api/v1`, pedigree legado com `generations=1..8` |
| `GenSW.Web/src/features/animals/filiations/components/AnimalFiliationsPanel.tsx` | Campo de ID manual e pedigree em lista aninhada |
| `GenSW.Web/src/shared/http/httpClient.ts` | Cliente atual serializa JSON e lê JSON; multipart e binário exigem extensão explícita |

Os caminhos de backend acima são relativos a `src/Backend/`; os de frontend, a `src/Frontend/`. Não foi encontrada infraestrutura de upload nas fontes consultadas. Espécie, raça e status ativo **ainda não são validados pelo serviço de filiação**: esta evolução deverá fechar a lacuna, sem alegar que essas garantias já existem. O lock atual também não basta para impedir ciclos formados por transações simultâneas sobre animais diferentes.

Referência visual examinada: `C:\Users\Marcos\Desktop\arvore.webp`. Aproveitar apenas cartões com foto circular, conectores e hierarquia legível. Não copiar o arquivo ao repositório. Não reproduzir literalmente cores, texto, orientação ou número de gerações.

## 2. Pesagens individuais

Criar `PesagemAnimal`, em `Domain/Animals`, com `Id`, `AnimalId`, `DataMedicao` (date obrigatório), `PesoGramas` (decimal(10,2)), `TipoMarco`, `DescricaoMarco` (até 100), `IdadeReferenciaDias` (int opcional), `Observacao` (até 2000), `CreatedAtUtc`, `UpdatedAtUtc`. FK Restrict para Animal. Não adicionar colunas de peso ao Animal nem reutilizar peso de ovo ou média de prole.

| TipoMarco (enum persistido como inteiro) | Semântica |
| --- | --- |
| 1 Livre | Medição comum; não exige evento ou idade alvo |
| 2 Nascimento | Declaração explícita do operador; com nascimento conhecido a data deve coincidir |
| 3 IdadeEmDias | Exige `IdadeReferenciaDias >= 0`; representa o alvo N, sem uma coluna para cada idade |
| 4 PrimeiraPostura | Marco informado manualmente; não consulta nem altera Produção de Ovos |
| 5 Abate | Marco informado manualmente; não muda status do Animal |
| 6 Outro | Exige descrição não vazia; permite marco não padronizado |

`DescricaoMarco` pode complementar qualquer tipo; apenas Outro a exige. `IdadeReferenciaDias` só pode estar preenchida em IdadeEmDias. Não há restrição por espécie nem unicidade por dia/marco: repetições são medições válidas. Pesos devem estar entre 0,01 e 99.999.999,99 g, com no máximo duas casas, rejeitando arredondamento silencioso. Datas futuras são rejeitadas usando o dia UTC do TimeProvider, coerente com Animal. Com nascimento conhecido, rejeitar medição anterior a ele.

`idadeDiasNaMedicao` é projeção, nunca uma coluna: diferença entre DataMedicao e DataNascimento, ou null se nascimento desconhecido. Idade alvo e idade real são distintas: uma medição alvo de 30 dias aos 31 dias retorna ambos. Sem nascimento, a idade alvo não inventa data de nascimento nem idade real. Nascimento sem data conhecida pode ser declarado pelo operador; a UI sinaliza que não foi possível conferir a idade.

Na correção posterior do nascimento, recalcular apenas a projeção. Rejeitar alteração que passe a colocar pesagem antes do nascimento ou contradiga marco Nascimento; orientar correção explícita das pesagens envolvidas antes de salvar, sem reescrever fatos automaticamente. Esse check e a gravação de pesagens devem compartilhar lock do Animal. Remover a data de nascimento torna a idade calculada null, preservando datas e marcos.

Permitir cadastrar e corrigir medições também em Animal inativo para completar histórico. MVP com POST, PUT e leitura, sem exclusão física ou trilha de versões nova; correção mantém Id e CreatedAtUtc e atualiza UpdatedAtUtc, seguindo o padrão existente de registros de peso. Uma auditoria completa de versões poderá ser proposta separadamente.

### Contratos propostos

Prefixo `/api/v1/animais/{animalId}/pesagens`:

| Método/rota | Entrada e saída |
| --- | --- |
| POST `/` | `{dataMedicao, pesoGramas, tipoMarco, descricaoMarco?, idadeReferenciaDias?, observacao?}`; 201, Location e recurso |
| PUT `/{pesagemId}` | Mesmo corpo; 200 com recurso; não permite transferir proprietário |
| GET `/{pesagemId}` | 200 com recurso individual |
| GET `/` | `page=1&pageSize=25&dataInicial=&dataFinal=&tipoMarco=`; envelope paginado |

Recurso: campos persistidos mais `idadeDiasNaMedicao: number|null`. Data em ISO `YYYY-MM-DD`; enums numéricos como nos contratos atuais. Ordem: DataMedicao desc, CreatedAtUtc desc, Id desc. Índice `(AnimalId, DataMedicao, Id)`; validar intervalo e enum. Não retornar toda a série na listagem paginada nem incluir gráfico ilimitado no MVP.

## 3. Imagens: domínio e armazenamento

Entidades concretas **`ImagemAnimal` e `ImagemVariedade`**, cada uma com FK obrigatória para seu proprietário. Evitar entidade polimórfica com duas FKs opcionais. A primeira registra aparência de um indivíduo ao longo do tempo; a segunda documenta características visuais do catálogo. Nenhuma cópia automática entre elas, inclusive ao trocar a variedade de um Animal. Inativar Animal/Variedade não remove fotos históricas. Não usar imagem de Variedade como se fosse foto do indivíduo no pedigree.

Campos por imagem: Id, FK do proprietário, `Legenda` (até 200), `DataCaptura` (date opcional, sem futuro), `Ordem` (int >= 0), `Representativa` (bool), `Ativa` (bool), chaves privadas de arquivo e miniatura, MIME final, bytes finais, largura/altura finais, CreatedAtUtc e UpdatedAtUtc. Usuário informa apenas arquivo e metadados editoriais. Chaves, MIME, dimensões e tamanhos são calculados pelo servidor; não devolver caminhos privados. DataCaptura é opcional e não é extraída de GPS/EXIF.

No máximo uma representativa ativa por Animal e uma por Variedade: índice único parcial por proprietário onde Ativa e Representativa. Definir representativa desmarca a anterior dentro de transação com lock do proprietário. Inativar representativa limpa sua flag; reativar não restaura preferência antiga. Ordenar não muda representatividade. Ordem pode empatar; desempate estável por CreatedAtUtc e Id ascendentes. Operação de reordenação aceita lista completa de IDs ativos, sem duplicados ou estrangeiros, e grava posições 0..N-1 atomicamente.

**Fallback único para todos os consumidores:** representativa ativa e disponível; se ausente, primeira imagem ativa e disponível por Ordem, CreatedAtUtc, Id; se nenhuma, ícone neutro. O fallback não promove imagem implicitamente nem modifica flags. Se conteúdo falhar entre a projeção e o download, mostrar ícone neutro nessa renderização, sem tentativas recursivas. API retorna `origem: representativa|ordenacao|nenhuma` e `imagem: resumo|null`; galeria e árvore aplicam a mesma seleção no servidor. Fotos de animais inativos continuam elegíveis para visualização histórica.

### Armazenamento e segurança

- Primeira entrega: volume privado persistente, fora de `wwwroot`, repositório e `.gensw/`. Interface pequena de armazenamento na Application, implementação na Infrastructure; metadados no PostgreSQL. Objeto remoto é extensão futura, não obrigação desta etapa.
- Aceitar JPEG, PNG e WebP estáticos, um arquivo por upload, máximo 5 MiB de entrada, 20 megapixels e 8192 pixels por dimensão. Máximo 50 imagens ativas e 100 MiB de derivados ativos por proprietário; cotas verificadas sob lock. Corpo multipart limitado a 6 MiB, incluindo metadados. Sem importação por URL externa.
- Verificar assinatura e decodificar conteúdo com biblioteca mantida/licenciada a selecionar na implementação. Não confiar em extensão ou Content-Type; rejeitar SVG, animações, arquivos truncados e bombas de descompressão. Limitar CPU/memória/tempo da decodificação; não executar conteúdo.
- Aplicar orientação e reencodar em JPEG/PNG, removendo EXIF/GPS e outros metadados; gerar imagem de visualização com maior lado <= 2048 e miniatura <= 256. Não persistir original; preservar proporção, recorte circular somente no frontend. Nome de armazenamento aleatório, sem uso de nome/caminho enviado pelo cliente.
- Upload prepara arquivos privados temporários, valida, publica derivados e só então confirma metadados ativos. Falha de banco remove derivados por compensação; falha de arquivo nunca confirma metadados disponíveis. Documentar inspeção/limpeza manual idempotente de temporários e órfãos, sem implantar agendadores nesta evolução.
- Inativação lógica retém binários, interrompe conteúdo via API e libera apenas cota ativa. Não oferecer purga no MVP. Documentar consumo de disco, monitoramento operacional e retenção como responsabilidade do operador; limite do volume deve impedir novos uploads com erro controlado, sem afetar fotos existentes.
- Endpoints de metadados e conteúdo exigem a mesma autenticação/autorização do proprietário, inclusive miniaturas. Sem URLs públicas, token em query string, diretório listado ou segredo em log. Pedido com ID de outro proprietário retorna 404. `Cache-Control: private, no-store` e `X-Content-Type-Options: nosniff`; MIME fornecido pelo servidor.
- Frontend carrega miniaturas como Blob usando sessão autenticada, cria object URLs e as revoga ao sair/trocar sessão. Estender cliente HTTP para multipart e Blob preservando refresh/cancelamento; não enviar FormData como JSON nem esperar que `<img src>` envie Bearer. Não criar autenticação paralela.
- Backup/restauração devem cobrir banco e volume em ponto consistente. Confirmar caminho, capacidade e permissões do volume no ambiente de implementação antes de habilitar upload; nenhuma credencial entra no documento/configuração versionada.

### API de imagens

Mesmos contratos em `/api/v1/animais/{animalId}/imagens` e `/api/v1/variedades/{variedadeId}/imagens`, com verificação própria de proprietário.

| Operação | Contrato |
| --- | --- |
| POST `/` multipart | `arquivo`, `legenda?`, `dataCaptura?`; cria ativa, não representativa, ao fim da ordem; 201 com recurso |
| GET `/` | `page=1&pageSize=25&ativo=true`; envelope paginado, Ordem/CreatedAtUtc/Id; recurso contém apenas metadados públicos |
| PUT `/{id}/metadados` | `{legenda, dataCaptura}`; 200; binário imutável, substituição por novo upload |
| PUT `/{id}/representativa` | `{representativa: boolean}`; 200; true exige ativa e disponível |
| PUT `/{id}/ativo` | `{ativo: boolean}`; 200; reativação revalida cotas e existência dos arquivos |
| PUT `/ordem` | `{ids: Guid[]}` de todas as ativas; 204; conjunto obsoleto/diferente retorna 409 |
| GET `/preferencial` | `{origem, imagem: {id, legenda, thumbnailPath}|null}`; 200, sem expor storage key |
| GET `/{id}/conteudo?versao=miniatura\|visualizacao` | Binário autenticado; imagem inativa/ausente retorna 404 |

`thumbnailPath` é rota relativa da API, não endereço privado. Recursos incluem Id, ID do proprietário, campos editoriais, Ativa, Representativa, MIME, largura/altura, tamanhoBytes, timestamps e rotas de conteúdo. Não retornar base64 na árvore ou nas listagens.

## 4. Seleção assistida e preservação de integridade

Nova consulta: `GET /api/v1/animais/{animalId}/progenitores-elegiveis?tipoFiliacao=1&search=&page=1&pageSize=25`. Página máxima 100; busca por código interno/nome, case-insensitive, com parâmetros e escape de curingas no padrão do repositório. Busca vazia retorna a primeira página; ordenar CodigoInterno e Id. Envelope paginado com resumos `{id,codigoInterno,nome,sexo,especieId,racaId,ativo}`. Espécie/raça vêm do Animal persistido, nunca de parâmetros manipuláveis do cliente.

Um mesmo predicado de elegibilidade deve ser usado pela consulta e revalidado no POST existente de filiação:

1. Progenitor existe, está ativo e é diferente do descendente.
2. Mesma EspecieId; se o descendente tem RacaId, progenitor deve ter a mesma (null não atende). Se descendente não tem raça, qualquer raça da espécie, inclusive null, é elegível. Variedade não restringe.
3. Pai requer Macho; Mãe requer Fêmea. Sexo indefinido não atende. Rejeitar tipo fora do enum antes de derivar sexo.
4. Progenitor não pode ser descendente direto/indireto do Animal no grafo de filiações ativas. Manter vedação do mesmo progenitor nos dois papéis.

Não filtrar por EscopoAnimal nem criar regra de idade parental não solicitada. Animal inativo pode receber correção histórica, mas um progenitor recém-selecionado deve estar ativo. Um progenitor já vinculado que depois seja inativado permanece no histórico e na árvore, identificado como inativo; não reaparece como candidato. Troca de filiação continua inativando o vínculo anterior na mesma transação, preservando datas e índice de uma relação ativa por tipo. Erro de validação deve desfazer toda a troca.

Para visualização humana do histórico, adicionar resumo opcional `progenitor: {id,codigoInterno,nome,ativo}` à resposta existente de filiações, mantendo todos os campos atuais. Não exigir consulta por linha nem digitação de UUID. UI usa nome e código; UUID fica interno ao contrato. Cadastro inicial deve salvar o Animal primeiro; mudanças pendentes de espécie/raça devem ser salvas antes de pesquisar e definir filiação.

### Ciclos, concorrência e alterações cadastrais

Separar limite **visual** de profundidade do check de integridade: este percorre todo o componente necessário, com conjunto visitado, nunca aceita vínculo só porque atingiu limite de tela. Substituir consultas por nó por CTE recursiva parametrizada com deduplicação de IDs. Na busca, excluir descendentes antes de contar/paginar; no POST, revalidar no instante da mutação. Estouro de timeout/cancelamento falha fechado, sem salvar.

Proposta mínima e conservadora para o volume atual: um lock transacional advisory PostgreSQL dedicado à genealogia, compartilhado por todas as mutações de filiação e alterações de espécie/raça/sexo/status de Animal. Adquirir antes de locks de linhas, reler estado após adquirir e manter até commit. Pesagens usam apenas lock da linha Animal; operações que precisem dos dois usam sempre advisory primeiro. Não criar lock global da aplicação nem usar locks em memória. Evitar deadlocks com ordenação de IDs quando houver múltiplas linhas.

Esse protocolo serializa mudanças genealógicas inclusive entre processos e impede A→B/B→A ou ciclos maiores concorrentes. Testar chamadas reais em conexões distintas. Definir espera máxima de 5 s e retornar 409 com código de conflito transitório se esgotada; não deixar transação aberta nem repetir sem limite. O custo é serializar esse pequeno conjunto de escritas; revisar somente com medição futura de contenção.

Alteração cadastral de espécie/raça/sexo valida vínculos ativos nas duas direções: Animal como filho e como progenitor. Rejeitar alterações que criem incompatibilidade, preservando a regra condicional da raça do descendente. Inativação é permitida sem apagar genealogia, mas coordenada com o POST para impedir nova seleção de quem já está inativo no instante do commit.

Não revalidar nem reescrever automaticamente o passado. Relações legadas inconsistentes permanecem legíveis com sinalização; relatório somente de leitura antes do rollout e correção manual explícita. Campos cadastrais sem mudança relevante não devem ficar bloqueados por inconsistência legada. Toda nova filiação deve cumprir as regras. Histórico inativo não é reativado nem usado no check do grafo vigente.

## 5. Árvore genealógica: projeção, limites e navegação

Novo endpoint `GET /api/v1/animais/{animalId}/arvore?ascendentes=3&descendentes=1`. Raiz no centro, ascendentes acima e descendentes abaixo. Considerar somente relações de filiação ativas, mas incluir os Animais vinculados mesmo inativos. Não inferir cônjuge, acasalamento ou vínculo a partir de Cruzamento/Ciclo/Prole. Grafo genealógico pode ter ancestral compartilhado: deduplicar Animal por ID no contrato; conectores preservam ambos os vínculos. Layout pode usar cartão de referência repetido com indicação de que é o mesmo indivíduo.

Contrato resumido:

```json
{
  "raizId": "guid",
  "nos": [{
    "animalId": "guid", "codigoInterno": "A001", "nome": null,
    "sexo": 1, "ativo": true, "dataNascimento": null,
    "imagem": null, "origemImagem": "nenhuma",
    "paiConhecido": false, "maeConhecida": false,
    "temAscendentesAdicionais": false, "temDescendentesAdicionais": true
  }],
  "arestas": [{"filiacaoId": "guid", "progenitorId": "guid", "descendenteId": "guid", "tipoFiliacao": 1}],
  "limites": {"ascendentes": 3, "descendentes": 1, "maxNos": 100, "maxArestas": 200},
  "truncada": false,
  "avisos": []
}
```

O exemplo ilustra o formato; uma resposta real só inclui arestas cujas duas pontas constam em `nos`. `imagem` usa o mesmo resumo de `/imagens/preferencial`. Pai/Mãe conhecido indica existência do vínculo, mesmo quando fora do recorte; assim a tela distingue desconhecido de não carregado. Avisos codificados cobrem inconsistência legada e ciclo legado detectado, sem travar ou ocultar silenciosamente toda a visualização.

- Profundidade validada: ascendentes 0..4, descendentes 0..2; raiz em nível zero. Valores fora da faixa retornam 400, sem ajuste silencioso. Máximo 100 nós únicos e 200 arestas por resposta, respeitados **durante** a consulta, não depois de carregar o grafo inteiro.
- Consulta por fronteiras em lote: raiz, três níveis ascendentes e um descendente por padrão; nunca expandir descendentes de cada ancestral nem ascendentes de cada descendente automaticamente. Priorização determinística: raiz, ascendentes, descendentes; dentro da fronteira, CodigoInterno/Id. Usar consultas limitadas e trazer um item extra para detectar truncamento.
- Flags de continuação são calculadas em lote com EXISTS; fotos/metadados dos nós em lote, AsNoTracking. Não invocar o `Build` recursivo legado nem a API de Animal por nó. Meta verificável: <= 12 comandos SQL por resposta inicial nas profundidades máximas, sem contar os downloads das miniaturas.
- Expansão explícita: `GET /api/v1/animais/{noId}/arvore/relacoes?direcao=ascendentes|descendentes&page=1&pageSize=20` (máximo 50). Retorna `nos` (nó focal e vizinhos da página), `arestas`, `page/pageSize/totalItems/totalPages`, flags e avisos. Apenas uma geração por clique; pais ordenados por papel/Id, filhos por CodigoInterno/Id. Não aceitar direção arbitrária nem percorrer outros níveis nessa operação.
- Paginação por página segue o padrão atual; sob mutações simultâneas pode mudar a composição entre páginas. UI deduplica por ID e oferece atualizar/recentrar; não prometer snapshot entre cliques. Dentro de uma resposta usar snapshot consistente da transação de leitura.
- Limitar estado acumulado da tela a 200 nós/400 arestas. Ao atingir, orientar recentrar no nó selecionado em vez de crescer indefinidamente. Recentralizar refaz recorte limitado, mantém navegação voltar e cancela requisições antigas. Zoom/pan não dispara busca.
- Imagens sob demanda para cartões visíveis, no máximo quatro downloads simultâneos. Sem imagem, foto inválida ou 404: ícone neutro com nome/código. Pai/Mãe desconhecido: placeholder sem ID nem aresta inventados. Não equiparar ausência de dado com ausência biológica de progenitor.
- Índice parcial de filiações ativas `(ProgenitorId, AnimalId)` para descendentes; reutilizar índice ativo por AnimalId/Tipo para ascendentes. Medir consulta candidata e árvore com PostgreSQL: fixture de 10 mil animais e até 20 mil vínculos, incluindo alta descendência, ancestrais compartilhados e profundidade maior que a tela. Meta inicial p95 <= 500 ms por consulta de metadados em homologação aquecida, documentando hardware; timeout de consulta de 3 s e cancelamento, sem carga ilimitada.

Manter `/pedigree?generations=1..8` e seu contrato atual para compatibilidade. O frontend novo usa `/arvore`; não mudar o limite legado incidentalmente. Eventual otimização/depreciação do legado é tarefa futura, sem expandir este escopo.

## 6. Frontend e comportamento HTTP

No Animal existente: painéis de Pesagens, Imagens e Genealogia, acompanhando a organização de `features/animals`. Listagem de peso mostra data, gramas, marco, idade alvo/real e observação; formulários exibem apenas campos pertinentes ao marco. Galeria permite upload, legenda/data, ordem, representativa e inativação. Galeria de Variedade fica na edição de Variedade e comunica sua finalidade de catálogo.

Combobox pesquisável de progenitores com debounce de 300 ms, cancelamento/resposta obsoleta ignorada, páginas explícitas, nome+código, rótulo Pai/Mãe e mensagens de ausência/carregamento/erro. Não carregar todos os animais. Após conflito no salvamento, atualizar candidatos e explicar a regra sem perder o restante do formulário.

Substituir ação “Visualizar pedigree” por “Árvore genealógica”. Cartões com foto circular, código/nome, indicação de inativo, conectores identificando pai/mãe, seleção por teclado, expansão e recentralização explícitas. Layout responsivo com zoom/rolagem e alternativa textual acessível; não depender só de cor. Não introduzir biblioteca de grafo pesada antes de avaliar o layout limitado durante implementação.

APIs novas seguem `[Authorize]`, DTOs próprios API/Application, serviços, repositórios e exceptions existentes. Convenção: 400 para dados/parâmetros inválidos; 401/403 conforme autenticação/permissão; 404 para proprietário/recurso não encontrado; 409 para elegibilidade, concorrência, cota ou estado incompatível; 413 para tamanho do upload; 415 para formato não suportado; 503 para armazenamento indisponível/timeout operacional. Erros com ProblemDetails e código estável em extensão, sem stack trace, caminhos privados ou conteúdo de credenciais. Não copiar encapsulamento acidental de ProblemDetails observado no controller legado.

## 7. Compatibilidade, migrations e entrega

Migrations aditivas, após aprovação: tabelas `PesagensAnimal`, `ImagensAnimal`, `ImagensVariedade`, FKs Restrict, checks de peso/enum/idade alvo/ordem e índices listados. Checks de representatividade exigem imagem ativa. Índices parciais únicos de representativa por proprietário. Não adicionar tabela de árvore ou colunas por marco. Não alterar dados, relações ou métricas de Cruzamentos, Ciclos, Proles e Produção de Ovos.

Não backfill de pesos a partir de agregados ou fotos a partir de Variedade. Animais existentes começam sem pesagens/fotos e têm fallback neutro; a árvore usa a filiação existente. As novas restrições relacionais de filiação são de serviço e protocolo transacional: não criar check retroativo que invalide linhas legadas ao migrar. Atualizar guardas do cadastro apenas no escopo indicado.

Sequência: aprovar design → migrations/modelos e APIs de peso/imagem → elegibilidade e integridade → projeção da árvore → frontend → regressão integrada e aceite manual. Banco antes do backend; backend antes do frontend. Durante atualização dos escritores de filiação/cadastro, interromper brevemente essas escritas e substituir todas as instâncias antigas: misturar versões que ignoram o lock não garante integridade. Leituras podem continuar.

Rollback operacional: retirar UI/API novas mantendo tabelas e dados. `Down` somente em banco descartável ou após backup/aceite explícito de perda; não executar rollback destrutivo em ambiente compartilhado. Migrations não serão aplicadas nesta etapa de planejamento.

## 8. Tarefas, dependências e aceite verificável

Todas filhas de #364; #365 é a única iniciada nesta etapa. #366–#371 aguardam aprovação em Planejado. Cada tarefa passará a Em andamento antes de trabalho técnico e a Em validação quando houver evidências, sem conclusão automática.

| Tarefa | Entrega e aceite principal | Dependências |
| --- | --- | --- |
| [#366](https://devops-lab.tailaf9418.ts.net/issues/366) Peso | Seis marcos, precisão, datas e idade alvo/real; POST/PUT/GET paginados; nascimento corrigido não altera medições; testes dos limites e locks | Aprovação; suporte de migrations #371 |
| [#367](https://devops-lab.tailaf9418.ts.net/issues/367) Imagens | Proprietários separados; segurança, cotas, falhas compensadas; representativa única concorrente e fallback estável; conteúdo autenticado | Aprovação; volume privado e migrations #371 |
| [#368](https://devops-lab.tailaf9418.ts.net/issues/368) Progenitores | Busca e POST com mesmo predicado; seleção sem UUID manual; concorrência A→B/B→A e ciclos longos rejeitada; cadastro e histórico preservados | Aprovação; índices/testes #371 |
| [#369](https://devops-lab.tailaf9418.ts.net/issues/369) Árvore API | Ascendentes/descendentes, nós deduplicados, ausência explícita, imagens preferenciais; limites e paginação; orçamento SQL e desempenho medidos; legado preservado | #367, #368 |
| [#370](https://devops-lab.tailaf9418.ts.net/issues/370) Frontend | Peso/galerias Animal e Variedade, combobox, cartões e conectores; teclado/tela estreita; sessão, fallback e navegação limitada | #366–#369 |
| [#371](https://devops-lab.tailaf9418.ts.net/issues/371) Migrações/testes/docs | Migrations aditivas com dados prévios; PostgreSQL real para concorrência; segurança e regressão; backup/restauração; evidências de aceite | Suporte desde o início; encerra após #366–#370 |

Matriz mínima de verificação futura:

- Domínio/Application: todos os marcos, descrição obrigatória, null de nascimento, 30 alvo/31 real, pesos inválidos, datas contraditórias e duas medições no mesmo dia. Nenhuma alteração em agregados existentes.
- PostgreSQL: unicidade de imagem sob concorrência, cota/ordem concorrentes, reativação e proprietário; substituição de filiação com rollback; ciclo simultâneo de dois e três animais; espécie/raça/sexo/status alterados durante seleção/gravação; correção de nascimento concorrente à pesagem.
- API/segurança: paginação/limites, auth e acesso cruzado por proprietário, arquivo falso/truncado/grande/animado, EXIF removido, estouro de pixels, falha de storage/banco, metadados sem caminhos privados. Conteúdo inativo não servido.
- Árvore: pais ausentes, ancestral compartilhado, Animal inativo, legado inconsistente/cíclico, nenhum nó extra além do teto, alta descendência, expansão/recentragem, sem N+1 e sem consultas recursivas do cliente.
- Frontend: erros de validação claros, resposta de busca obsoleta descartada, refresh autenticado em upload/Blob, URLs revogadas, fallback de foto, limites de memória, teclado e viewport estreito. Validar manualmente hierarquia e legibilidade.
- Regressão: filiação histórica, mudança de sexo já protegida, cadastro/taxonomia, Cruzamentos, Ciclos, Proles onde houver cobertura e Produção de Ovos; não alterar #363 nem seus critérios.

## 9. Resultado desta etapa e decisão humana

Entregues apenas design e rastreabilidade. Sem código funcional, migrations executadas, deploy ou merge. Aceite solicitado para as decisões deste documento, incluindo marcos com idade alvo, armazenamento privado com limites, regras de nova filiação e preservação do histórico, lock transacional da genealogia e árvore limitada com expansão por clique.

Após aprovação, começar pelas tarefas de modelo/API e suporte de migrações. Aprovação do planejamento não equivale a aceite de implementação, merge ou liberação em produção.
