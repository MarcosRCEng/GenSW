# Operação de pesos, imagens e genealogia — Evolução #364

## Configuração e armazenamento

A API recebe `Images:PrivateRoot` (variável `Images__PrivateRoot`), caminho absoluto de um volume previamente provisionado fora do repositório, de `.gensw` e de qualquer `wwwroot`. Não há valor padrão. A conta do serviço precisa ler, criar, renomear e remover arquivos; outras contas não devem acessar o volume. Não publicar esse diretório por servidor HTTP. Em múltiplas instâncias, montar o mesmo volume privado para todas; o banco serializa as alterações, mas arquivos locais diferentes não constituem armazenamento compartilhado.

`Images:MinimumFreeBytes` (`Images__MinimumFreeBytes`) reserva por padrão 100 MiB livres antes de cada publicação. Monitorar capacidade do volume: essa checagem não reserva espaço contra processos externos. Configuração ausente, permissão insuficiente ou falha de escrita retornam 503; as outras funções do cadastro continuam disponíveis. Não adicionar caminhos reais privados ao Git ou a relatórios públicos.

JPEG, PNG e WebP estáticos são identificados pelo conteúdo, com entrada até 5 MiB, lado até 8192 e área até 20 milhões de pixels. O processador Magick.NET-Q8-AnyCPU 14.17.2 limita memória a 256 MiB, disco a zero, uma thread e tempo de processamento a 5 s; há uma decodificação por processo e espera máxima de 5 s. Reorienta, remove perfis/metadados e publica somente PNG de visualização até 2048 e miniatura até 256, sem original. O recurso nativo depende das plataformas suportadas pelo pacote; validar o host de destino antes da liberação.

A cota de cada Animal ou Variedade é independente: 50 imagens ativas e 100 MiB somando os derivados. Imagens inativas continuam no volume e no histórico, não contam na cota e não são servidas. Reativar exige ambos os arquivos e cota disponível, sem restaurar a preferência anterior. Preferência explícita única; fallback por ordem, criação e ID entre imagens ativas disponíveis. Fotos de Variedade nunca são fallback para um indivíduo.

O upload escreve arquivos temporários no próprio volume, renomeia e confirma metadados em transação. Falha antes da confirmação tenta remover ambos os derivados; queda abrupta ou falha na remoção pode deixar órfãos. Não há job ou automação de limpeza.

## Publicação e compatibilidade

1. Fazer backup consistente do banco e volume, verificar restauração em ambiente isolado e executar o relatório de filiação abaixo. Inconsistências legadas não são corrigidas automaticamente.
2. Aplicar as migrations `20261002133151_AddAnimalWeightsAndImages` e `20261002133435_AddGenealogyTraversalIndex` pelo procedimento existente. São aditivas: três tabelas, checks, FKs Restrict e índices; preservam o índice legado de progenitor. Não fazem backfill nem alteram Cruzamentos, Ciclos, Proles ou Produção de Ovos.
3. Suspender brevemente escritas de filiação e alterações de classificação/status/nascimento de Animal, substituir todas as instâncias antigas pelo backend novo e retomar escritas. Instâncias antigas não respeitam o lock de genealogia; não misturar versões de escritores. Leituras podem continuar.
4. Publicar frontend depois do banco e backend. Verificar upload, conteúdo autenticado, busca de progenitores e árvore no host alvo.

O protocolo usa transação PostgreSQL, advisory lock `(364,368)` para genealogia antes do lock de linha de Animal. Pesagem e imagem travam o proprietário; alterações de nascimento/classificação/status participam do mesmo protocolo. Espera de lock limitada a 5 s, retornando 409 `conflito_transitorio`. Busca/árvore usam snapshot repetível e limite SQL de 3 s. Novos escritores devem seguir esse protocolo.

A árvore inicial aceita até quatro gerações ascendentes e duas descendentes (padrão 3/1), 100 nós e 200 arestas. Expansão é uma geração paginada, até 50 itens por requisição. A tela acumula no máximo 200 nós/400 arestas, com recentragem explícita. Pais desconhecidos são ausência de relação; não são animais inventados. O pedigree legado permanece com o contrato de 1 a 8 gerações.

Rollback operacional: retirar frontend/API novos e manter tabelas e dados. Não executar `Down` em banco compartilhado sem backup e aceite explícito de perda. Se voltar a escritores antigos, suspender escritas durante a troca e considerar que as novas garantias transacionais deixam de existir.

## Backup e restauração

Parar escritas de imagens e metadados, obter dump consistente PostgreSQL e snapshot/cópia integral do volume privado no mesmo intervalo. Preservar arquivos de imagens inativas; incluir manifesto de nomes opacos, tamanhos e hashes SHA-256 com o backup. Criptografar e controlar o acesso aos dois conjuntos; aplicar a política de retenção operacional existente. Não apagar imagens inativas como política de retenção sem decisão humana.

Restaurar banco e volume correspondentes em ambiente isolado, provisionar permissões do serviço, configurar `Images__PrivateRoot` e comparar o manifesto. Conferir contagens por proprietário, preferência única, conteúdo autenticado de imagem ativa, 404 de inativa e fallback de arquivo ausente. Somente então planejar a recuperação do ambiente compartilhado. Há teste executável de `pg_dump`/`pg_restore` e restauração de derivados por hash em `AnimalEvolutionTests`; ele utiliza exclusivamente PostgreSQL descartável, sem validar o backup real de produção.

Reconciliação manual: com escritas suspensas e backup disponível, exportar internamente `ArquivoKey`/`MiniaturaKey` das duas tabelas, incluindo inativas. Comparar o conjunto com arquivos `.png` e `.tmp` do volume. Arquivo referenciado ausente exige restauração do backup; não recriar foto por variedade nem alterar filiação. Arquivos sem referência e temporários devem primeiro ser colocados em quarentena restrita após revisão; remoção exige a política de retenção/aceite operacional. Não versionar o inventário privado e não executar limpeza concorrente com uploads.

## Relatório prévio de filiação

Executar `animal-genealogy-audit.sql` em conexão de leitura. Registra incompatibilidades ativas de espécie, raça condicional e sexo, auto-filiação e vértices pertencentes a ciclos. Animais inativos ligados historicamente são preservados. O relatório não modifica dados e não contém caminhos de imagens. O timeout evita execução ilimitada; se expirar, a auditoria continua pendente e precisa de janela adequada. Revisar achados com responsáveis; migração/serviços não reescrevem o histórico automaticamente.
