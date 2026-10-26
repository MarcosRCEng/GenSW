# Auditoria npm da F02 — Tarefa #495

Consulta ao registro npm em 25/10/2026, sem remediação ou alteração de dependências. O resumo JSON contém nomes, intervalos, severidades e referências dos advisories, sem logs privados.

- Conteúdo versionado de `package-lock.json` preservado frente à baseline: `true`; `git diff --exit-code -- package.json package-lock.json` retornou 0. O checkout Windows usa CRLF e o blob Git usa LF.
- SHA-256: `608141fda28bc0b03686daa99eb7ede8c26b9e3a51bc7b09f72e30cd64285cdb`.
- `npm audit` e `npm audit --omit=dev` retornaram código 1 por vulnerabilidades. Esse resultado foi registrado; testes, lint e build são gates separados.

| Consulta | Críticas | Altas | Moderadas | Baixas | Total de pacotes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Completa | 2 | 5 | 5 | 2 | 14 |
| Sem dev | 0 | 3 | 1 | 0 | 4 |

Na consulta completa, `tinypool` e `vitest` permanecem classificados como críticos. A consulta sem dev ainda inclui `vite`, `esbuild`, `postcss` e `source-map-js`, conforme a classificação do lockfile.

A existência do advisory não demonstra exploração no ambiente; também não concede aceite do risco residual. A configuração de publicação e a avaliação operacional geral permanecem no fechamento #493. Não foi executado `npm audit fix`, atualização de versão ou alteração de lockfile para contornar o resultado.

Referências retornadas pelo npm para os pacotes críticos e para a consulta sem dev:

- [Tinypool: Prototype Pollution gadget in worker options leads to Remote Code Execution](https://github.com/advisories/GHSA-5gmw-xhrv-c9v3) — `tinypool`.
- [Tinypool: Prototype Pollution Gadget to RCE in run() options](https://github.com/advisories/GHSA-85c8-ppgw-ccpr) — `tinypool`.
- [Vitest: Path Traversal / Arbitrary File Read via @vitest/mocker Redirect Mock](https://github.com/advisories/GHSA-82fw-gwwq-j7x9) — `vitest`.
- [esbuild enables any website to send any requests to the development server and read the response](https://github.com/advisories/GHSA-67mh-4wv8-2f99) — `esbuild`.
- [PostCSS has XSS via Unescaped </style> in its CSS Stringify Output](https://github.com/advisories/GHSA-qx2v-qp2m-jg93) — `postcss`.
- [PostCSS: Arbitrary file read and information disclosure via attacker-controlled sourceMappingURL in CSS comments](https://github.com/advisories/GHSA-6g55-p6wh-862q) — `postcss`.
- [PostCSS: incomplete fix of GHSA-6g55-p6wh-862q — attacker-controlled sourceMappingURL reads arbitrary .map files when `from` is unset](https://github.com/advisories/GHSA-fxqj-rqcc-2cmp) — `postcss`.
- [PostCSS: Path Traversal in Previous Source Map Auto-Loading (sourceMappingURL) leads to Arbitrary .map File Disclosure](https://github.com/advisories/GHSA-r28c-9q8g-f849) — `postcss`.
- [source-map-js allows event-loop denial of service through indexed source-map section offsets](https://github.com/advisories/GHSA-68fv-2mgg-jv7q) — `source-map-js`.
- [Websites were able to send any requests to the development server and read the response in vite](https://github.com/advisories/GHSA-vg6x-rcgg-rjx6) — `vite`.
- [Vite bypasses server.fs.deny when using ?raw??](https://github.com/advisories/GHSA-x574-m823-4x7w) — `vite`.
- [Vite has an `server.fs.deny` bypass with an invalid `request-target`](https://github.com/advisories/GHSA-356w-63v5-8wf4) — `vite`.
- [Vite's server.fs.deny bypassed with /. for files under project root](https://github.com/advisories/GHSA-859w-5945-r5v3) — `vite`.
- [Vite allows server.fs.deny to be bypassed with .svg or relative paths](https://github.com/advisories/GHSA-xcj6-pq6g-qj4x) — `vite`.
- [Vite middleware may serve files starting with the same name with the public directory](https://github.com/advisories/GHSA-g4jq-h2w9-997c) — `vite`.
- [Vite's `server.fs` settings were not applied to HTML files](https://github.com/advisories/GHSA-jqfw-vq24-v9c3) — `vite`.
- [vite allows server.fs.deny bypass via backslash on Windows](https://github.com/advisories/GHSA-93m4-6634-74q7) — `vite`.
- [Vite has a `server.fs.deny` bypassed for `inline` and `raw` with `?import` query](https://github.com/advisories/GHSA-4r4m-qw57-chr8) — `vite`.
- [Vite Vulnerable to Path Traversal in Optimized Deps `.map` Handling](https://github.com/advisories/GHSA-4w7w-66w2-5vf9) — `vite`.
- [launch-editor: NTLMv2 hash disclosure via UNC path handling on Windows](https://github.com/advisories/GHSA-v6wh-96g9-6wx3) — `vite`.
- [vite: `server.fs.deny` bypass on Windows alternate paths](https://github.com/advisories/GHSA-fx2h-pf6j-xcff) — `vite`.
- [launch-editor: escapeCmdArgs() omits % escaping on Windows](https://github.com/advisories/GHSA-698v-4r3x-3vcv) — `vite`.
- [launch-editor: Editor CLI flag misinterpretation causing arbitrary command execution for some editors](https://github.com/advisories/GHSA-r825-w8mm-5pxw) — `vite`.
- [launch-editor: `fs.existsSync()` existence check can be bypassed on Windows](https://github.com/advisories/GHSA-587h-7wmj-h5pm) — `vite`.
- [Vite: Cross origin script execution through Vite custom middleware](https://github.com/advisories/GHSA-9jrq-w75r-8gcw) — `vite`.
- [Vite: Files outside `server.fs.allow` could be served when project paths match system paths on non-Windows machine](https://github.com/advisories/GHSA-rq7h-c2jc-7f22) — `vite`.
