# Avaliação e remediação npm pré-piloto

13/11/2026, America/Sao_Paulo · [Execução #530](https://devops-lab.tailaf9418.ts.net/issues/530) · [Gate #509](https://devops-lab.tailaf9418.ts.net/issues/509) · [Validação geral #493](https://devops-lab.tailaf9418.ts.net/issues/493)

## Auditoria

Baseline obtido após fetch de `origin/main` em `d078b28b4e48234e9a3ffcddf0409fafa1564d8d`, com [CI pós-merge aprovada](https://github.com/MarcosRCEng/GenSW/actions/runs/43429771514). O levantamento de 21 avisos pós-F03 é histórico. As consultas ao registro npm foram repetidas a partir do lockfile, sem scripts ou mudanças no arquivo durante cada consulta. Ambiente local Windows, Node 24.15.0, npm 11.12.1. O CI usa Ubuntu 24.04 e Node 22.

```text
npm audit --package-lock-only --ignore-scripts --json
npm audit --package-lock-only --ignore-scripts --omit=dev --json
```

| Lockfile | Visão | Baixa | Moderada | Alta | Crítica | Total | Código npm |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline | Completa | 2 | 10 | 5 | 2 | 19 | 1 |
| Baseline | `--omit=dev` | 0 | 1 | 3 | 0 | 4 | 1 |
| Candidato | Completa | 0 | 7 | 0 | 0 | 7 | 1 |
| Candidato | `--omit=dev` | 0 | 0 | 0 | 0 | 0 | 0 |

SHA-256 do lockfile no checkout Windows, baseline `608141fda28bc0b03686daa99eb7ede8c26b9e3a51bc7b09f72e30cd64285cdb` e candidato `e807c1f5c26f88db42d3a9fd8be91e8d138f33add04db270fab5fc68ae729657`. O catálogo muda independentemente do lockfile. O npm conta pacotes atingidos, incluindo dependentes de um mesmo advisory.

## Exposição e tratamento

O CI instala dependências com `npm ci`, incluindo scripts de instalação aplicáveis, e executa Vitest em jsdom, ESLint e build. O build usa TypeScript, Vite, plugin React e PostCSS/Tailwind com fontes do projeto e dependências. O Vite também atende desenvolvimento local em loopback com HTTPS. No baseline, Vite e plugin React estavam em `dependencies`, o que também levava esbuild, PostCSS e source-map-js à visão `--omit=dev`. Os dois primeiros foram classificados como dependências de desenvolvimento junto com a atualização. O ensaio de mapa de fontes encontrou no bundle React, React DOM, React Router e Scheduler. A #491 registrou um host estático de loopback para a candidata histórica. A #493 precisa identificar o host e a exposição efetivos do próximo piloto. [CI](../../.github/workflows/ci.yml) · [Configuração Vite](../../src/Frontend/GenSW.Web/vite.config.ts) · [#491](https://devops-lab.tailaf9418.ts.net/issues/491)

| Aviso ou família | Versões e cadeia observadas | Exposição e tratamento |
| --- | --- | --- |
| Vite, incluindo GHSA-p4f5-76q4-ffq7, e esbuild GHSA-67mh-4wv8-2f99 | Vite direto 5.4.10 → 6.4.6, plugin React 4.3.3 → 4.7.0, esbuild 0.21.5 → 0.25.12 | Vite participa de dev, preview e build. O advisory crítico recente refere-se ao servidor de desenvolvimento e foi conferido no mantenedor mesmo ausente da consulta npm. O advisory do esbuild refere-se ao servidor próprio, sem chamada encontrada no projeto. Atualização mantém o alvo padrão de navegadores da linha Vite anterior. [Vite](https://github.com/vitejs/vite/security/advisories) · [Correção 6.4.6](https://github.com/vitejs/vite/security/advisories/GHSA-p4f5-76q4-ffq7) · [esbuild](https://github.com/evanw/esbuild/security/advisories/GHSA-67mh-4wv8-2f99) · [Alvo Vite 6](https://v6.vite.dev/config/build-options#build-target) |
| Vitest e mocker GHSA-82fw-gwwq-j7x9, GHSA-qxg6-89mg-rqv8 e GHSA-xp26-3454-mjgp. Tinypool GHSA-5gmw-xhrv-c9v3 e GHSA-85c8-ppgw-ccpr | Vitest direto e mocker transitivo 3.2.7 → 4.1.12. Tinypool 1.1.1 saiu do grafo | Testes locais e CI. A configuração usa jsdom e o comando `vitest run`. A atualização resolve os avisos do mocker e elimina a cadeia Tinypool. [Vitest](https://github.com/vitest-dev/vitest/security/advisories) · [Tinypool](https://github.com/tinylibs/tinypool/releases/tag/v2.1.2) |
| PostCSS GHSA-qx2v-qp2m-jg93, GHSA-6g55-p6wh-862q, GHSA-r28c-9q8g-f849 e GHSA-fxqj-rqcc-2cmp. source-map-js GHSA-68fv-2mgg-jv7q e parser GHSA-rj75-hqrm-r3gf | PostCSS direto e via Vite 8.4.49 → 8.5.32. source-map-js via PostCSS 1.2.1 → 1.2.2. Parser via Tailwind e postcss-nested 6.1.4 → 6.1.6 | Pipeline de CSS em build e dev. Não foi identificado processamento de CSS, seletores ou mapas fornecidos por usuários no fluxo da SPA. A linha 6 do parser recebeu correção mantida para consumidores como Tailwind 3. [PostCSS](https://github.com/postcss/postcss/security/advisories) · [source-map-js](https://github.com/7rulnik/source-map-js/releases/tag/v1.2.2) · [Parser](https://github.com/postcss/postcss-selector-parser/releases/tag/6.1.6) |
| ESLint/plugin-kit GHSA-xffm-g5w8-qvg7, js-yaml GHSA-2883-xcg3-v3hh e brace-expansion GHSA-q2hr-2g5m-vwhr, GHSA-qhr7-859c-m2p7 e GHSA-6j4f-fj2g-mc7p | ESLint e @eslint/js 9.13.0 → 9.39.5, plugin-kit 0.2.8 → 0.4.1, js-yaml via @eslint/eslintrc 4.3.1 → 4.3.2, brace-expansion via minimatch 1.1.18/2.1.4 → 1.1.22/2.1.8 | Lint, configuração e globs do código-fonte. Não foi identificado fluxo da SPA que envie dados de usuário a esses parsers. A atualização trata os avisos da linha existente. ESLint 9 está fora de manutenção pelo upstream. A migração de major permanece pendente e tem impacto de compatibilidade nos plugins e no Node. [Plugin-kit](https://github.com/eslint/rewrite/security/advisories/GHSA-xffm-g5w8-qvg7) · [js-yaml](https://github.com/nodeca/js-yaml/security/advisories/GHSA-2883-xcg3-v3hh) · [brace-expansion](https://github.com/juliangruber/brace-expansion/security/advisories) · [Suporte ESLint](https://eslint.org/version-support/) |
| Residual GHSA-g3hv-75wg-m249 | `typescript-eslint@8.15.0 → @typescript-eslint/parser → @typescript-eslint/typescript-estree → minimatch@9.0.9` com propagação a outros pacotes da família. Um advisory moderado e sete entradas npm | A chamada de `minimatch` no serviço de projetos do parser usa os padrões de `allowDefaultProject`. O lint do GenSW usa globs fixos e a configuração não habilita `projectService` ou `allowDefaultProject`. Esse caminho não está configurado no GenSW. A correção 11.0.1 fica fora do intervalo do parser. A versão atual do parser publicada na consulta ainda depende de minimatch 10.x. [Advisory](https://github.com/isaacs/minimatch/security/advisories/GHSA-g3hv-75wg-m249) · [Configuração ESLint](../../src/Frontend/GenSW.Web/eslint.config.js) |

Foi necessário atualizar typescript-eslint para 8.15.0 porque a combinação anterior falhava ao carregar a regra `no-unused-expressions` com o novo ESLint. A configuração TypeScript explicita os tipos Node já usados pelas suítes e a biblioteca de tipos de `Array.at` já usada no código, mantendo o alvo ES2020. [Correção upstream da regra](https://github.com/typescript-eslint/typescript-eslint/pull/10339)

## Verificações do candidato

- `npm ci`, `npm test` (71 arquivos, 769 testes), `npm run lint`, `npm run build` e `git diff --check` aprovados no Windows. O build mantém o aviso de tamanho do bundle. O inventário com mapa de fontes foi feito em diretório temporário descartado.
- Edge em desktop e 390 × 844 com bundle estático, apenas em loopback e com respostas de API sintéticas. Renderização de CSS/JS, login, `me`, refresh ao recarregar, logout e redirecionamento de rota protegida confirmados. O refresh anônimo retornou 401 conforme a fixture. O host estático de teste retornou 404 para o favicon. O ensaio não avalia o backend real, dados, imagens ou o host efetivo do piloto.
- A publicação é revisável. O SHA, os jobs e o resultado final da CI são registrados na #530 e na #509.

## Decisão para o piloto

O risco remanescente identificado pela auditoria está no lint. A recomendação é permitir o piloto **somente após aceite explícito do responsável** sobre o candidato revisado, mantendo a entrega por artefato estático e sem expor ferramentas de desenvolvimento ou de testes no ambiente do piloto. O responsável pelo piloto decide sobre esse risco e sobre o uso temporário da linha ESLint 9 sem manutenção. O mantenedor do frontend deve acompanhar a correção compatível do parser e a migração do ESLint e reavaliar antes da liberação se o SHA, o catálogo ou a exposição do ambiente mudar. A #493 continua planejada até a decisão de risco que permita a validação e a autorização para executá-la.
