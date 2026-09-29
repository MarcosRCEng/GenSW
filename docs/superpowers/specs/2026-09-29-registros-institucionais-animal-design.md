# NA-05 — Registros institucionais do Animal — Design

**Redmine:** #356 (NA-05, filha de #297)

`RegistroAnimal` é uma entidade histórica subordinada a `Animal`, com `TipoRegistro` inicial `SISBOV` ou `UELN`, `NumeroRegistro`, vigência e auditoria. O número é imutável depois da criação; substituição é inativação seguida de novo cadastro.

`TipoRegistro + NumeroRegistro` é único, por comparação case-insensitive, em todo o histórico. Cada animal pode manter SISBOV e UELN ativos simultaneamente, mas somente um ativo de cada tipo. O PostgreSQL reforça as duas regras com índices únicos (um por expressão e um parcial); a aplicação bloqueia a linha do Animal nas mutações para serializar a decisão por animal.

As rotas autenticadas são somente aninhadas em `/api/v1/animais/{animalId}/registros`: `POST`, `GET` e `PATCH /{registroId}/inativar`. Não há DELETE físico, integração externa, pedigree/filiação ou cruzamentos.
