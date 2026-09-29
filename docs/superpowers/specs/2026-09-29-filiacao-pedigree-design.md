# NA-06 — Filiação e Pedigree

`FiliacaoAnimal` mantém relações Pai/Mãe como histórico, com no máximo uma relação ativa por tipo para cada animal. A troca inativa o vínculo anterior na mesma transação. A leitura de pedigree é uma projeção recursiva das relações ativas, limitada a oito gerações; não há tabela de pedigree.

O serviço rejeita auto-filiação, ciclos, sexo incompatível e um mesmo progenitor ativo nos dois papéis. O repositório também impede mudança de sexo que invalide relação ativa.
