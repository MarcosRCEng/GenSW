# Propriedades e vínculo operacional de Animais — #404

## Regras e limites

Propriedade é uma unidade operacional física. Seu nome é obrigatório (até 200 caracteres), normalizado e único sem diferenciar maiúsculas/minúsculas. Localização textual (até 500) e observação (até 2000) são opcionais. Não há coordenadas, titular jurídico, compra/venda, estoque, caixa ou permissões por Propriedade.

Animal permanece válido sem Propriedade, inclusive nos escopos Operacional e Referência. O cadastro do Animal não ganha campo obrigatório nem backfill. Filiação é a fonte do pedigree; Cruzamentos, Ciclos, Proles e Fluxo de caixa conservam suas regras.

Cada vínculo registra Animal, Propriedade, início, término opcional e observação. Término ausente significa vínculo atual. Os períodos usam datas civis `YYYY-MM-DD`, sem futuro, considerando hoje UTC como nos serviços existentes. Transferir fecha o atual e abre o seguinte na mesma data; movimentos no mesmo dia são permitidos e preservados. O fim não pode anteceder o início. Após encerramento, uma associação não pode começar antes do último término. Não há edição ou exclusão do histórico nesta evolução.

A inativação de Propriedade conserva todos os vínculos e seu histórico, inclusive o atual. Impede novas entradas enquanto inativa; os animais existentes podem ser transferidos ou desvinculados. A reativação libera novas entradas. Animais inativos também podem ter seu vínculo operacional administrado: esta vertical não acrescenta restrições ao cadastro de Animal.

## Navegação e API

Propriedades ficam em **Cadastros básicos**, com lista pesquisável, filtro de status, paginação, cadastro, Visualizar e Editar. No Animal salvo, Editar permite associar/transferir/encerrar; Visualizar mostra atual e histórico em leitura. O vínculo é opcional e administrado separadamente do salvamento do cadastro de Animal.

Todas as rotas abaixo exigem autenticação e usam o prefixo `/api/v1`.

| Método e rota | Uso |
| --- | --- |
| `GET /propriedades` | Lista: `page`, `pageSize`, `search`, `ativo`, `sortBy` (`nome`, `ativo`, `createdAtUtc`), `sortDirection` (`asc`, `desc`). |
| `POST /propriedades` | Cadastro: `nome`, `localizacao`, `observacao`. |
| `GET /propriedades/{id}` | Consulta individual, incluindo inativas. |
| `PUT /propriedades/{id}` | Edição de nome/localização/observação. |
| `PATCH /propriedades/{id}/ativo` | Corpo `{ "ativo": true/false }`. |
| `GET /animais/{animalId}/propriedades` | `{ atual, historico }`; histórico completo, incluindo o atual. |
| `POST /animais/{animalId}/propriedade/transferir` | Primeiro vínculo ou transferência: `propriedadeId`, `dataInicio`, `vinculoAtualIdEsperado` (ID atual ou `null`), `observacao` opcional. |
| `POST /animais/{animalId}/propriedade/desvincular` | Encerramento: `dataFim`, `vinculoAtualIdEsperado` (ID atual). |

A resposta de Propriedade contém ID, nome, localização, observação, ativo e auditoria UTC. A lista inclui `items`, `page`, `pageSize`, `totalItems` e `totalPages`. Cada vínculo inclui ID, Animal, ID/nome/status **atuais** da Propriedade, início/fim, observação e auditoria UTC. Renomear a Propriedade atualiza sua identificação nas consultas históricas; o período e o ID de origem permanecem preservados. Não é um snapshot cadastral de nomes antigos.

As operações de vínculo retornam `200` e o estado atualizado `{ atual, historico }`. Dados inválidos retornam `400`; recursos ausentes, `404`; conflitos de nome, destino inativo, destino já atual ou vínculo desatualizado, `409` com código de problema. O cliente deve recarregar o histórico e exigir nova decisão após `vinculo_desatualizado`; nunca reenviar silenciosamente contra um vínculo diferente.

## Persistência, concorrência e publicação

O schema é aditivo: tabelas de Propriedades e períodos de vínculo, sem alterar colunas nem preencher dados de Animal. Chaves estrangeiras restritivas preservam as referências. Um índice único parcial em Animal para término nulo impede dois vínculos atuais, inclusive em gravações que não passam pela API.

O nome possui uma chave interna `NomeNormalizado`, calculada por `ToUpperInvariant` e protegida por índice único. Isso mantém a comparação de nomes com acentos independente do locale do PostgreSQL. A chave não é exposta no DTO público; escritores externos precisam calculá-la com a mesma regra.

As mutações usam transação e lock do Animal. O destino é protegido contra inativação concorrente; o vínculo esperado é conferido dentro da transação. Fechamento e abertura são confirmados juntos. A validação de sequência temporal inclui o último vínculo fechado quando não há atual. O índice único parcial é a proteção final de unicidade; escritores externos devem respeitar o protocolo transacional para preservar também as demais regras.

1. Obter backup pelo procedimento PostgreSQL existente e validar a restauração em ambiente isolado.
2. Aplicar a migration aditiva `20261005132012_AddOperationalProperties` com a ferramenta EF e configuração externa de conexão. A API não aplica migrations no startup. Inspecionar o SQL gerado antes da publicação em outro ambiente.
3. Publicar backend e, em seguida, frontend compatíveis. Verificar cadastro, consulta, status, associação, transferência, encerramento e rejeição de operação desatualizada.
4. Confirmar que animais preexistentes continuam consultáveis sem vínculo e que Filiação/reprodução/caixa permanecem funcionais.

Rollback operacional: retirar frontend/API novos e manter as tabelas e seus dados. Não executar `Down` em base compartilhada sem backup e autorização explícita para perder o histórico. Não executar exclusões manuais como forma de transferir Animal.

As [evidências de validação](../validation/2026-10-05-propriedades-404.md) identificam a migration, os testes e o ambiente usado. Revisão, aceite humano e merge ficam pendentes no PR; as Tarefas permanecem **Em validação**.
