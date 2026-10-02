-- Somente leitura; executar antes da atualização dos escritores.
BEGIN READ ONLY;
SET LOCAL statement_timeout = '30s';
SELECT f."Id", f."AnimalId", f."ProgenitorId", f."TipoFiliacao",
       filho."CodigoInterno" AS "Filho", progenitor."CodigoInterno" AS "Progenitor"
FROM "FiliacoesAnimal" f
JOIN "Animais" filho ON filho."Id" = f."AnimalId"
JOIN "Animais" progenitor ON progenitor."Id" = f."ProgenitorId"
WHERE f."Ativa" AND (
    f."AnimalId" = f."ProgenitorId" OR filho."EspecieId" <> progenitor."EspecieId"
    OR (filho."RacaId" IS NOT NULL AND filho."RacaId" IS DISTINCT FROM progenitor."RacaId")
    OR (f."TipoFiliacao" = 1 AND progenitor."Sexo" <> 1)
    OR (f."TipoFiliacao" = 2 AND progenitor."Sexo" <> 2));

-- UNION elimina pares repetidos, inclusive quando o legado contém ciclos.
WITH RECURSIVE alcance(origem, destino) AS (
    SELECT "ProgenitorId", "AnimalId" FROM "FiliacoesAnimal" WHERE "Ativa"
    UNION
    SELECT a.origem, f."AnimalId"
    FROM alcance a JOIN "FiliacoesAnimal" f ON f."ProgenitorId" = a.destino AND f."Ativa"
)
SELECT DISTINCT origem AS "AnimalEmCiclo" FROM alcance WHERE origem = destino;
ROLLBACK;
