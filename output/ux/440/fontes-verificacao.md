# Verificação das fontes públicas — #440

Data da consulta e verificação: **11/10/2026**, America/Sao_Paulo. Esta lista complementa o [benchmark](benchmark.md). Apenas fontes públicas foram requisitadas; nenhum endpoint de domínio ou dado privado foi usado.

## Método

As 23 URLs públicas únicas do benchmark foram verificadas com GET por `System.Net.Http.HttpClient`, timeout de 25 segundos, sem credenciais, e com identificação do status HTTP, tipo de conteúdo e título HTML. O conteúdo não foi gravado no repositório. A ferramenta Web foi usada para ler e pesquisar as páginas oficiais e identificar a evidência semântica; buscas e leitura direta são distintas na coluna abaixo. Um HTTP 200 confirma resposta da URL, não disponibilidade de todas as funcionalidades nem qualidade do produto. Um HTTP 403 ao cliente de verificação não prova que a página esteja indisponível em navegador comum.

Resultado: **21 respostas HTTP 200 e duas HTTP 403**. Entre as respostas 200, 19 são páginas de documentação com título identificado, uma é a referência PNG e uma é o shell JavaScript do SAP Help Portal. A extração do texto SAP variou entre leitura, busca e nova leitura: por isso o benchmark não afirma observação de um ERP SAP em execução.

## URLs verificadas

| Fonte e URL | HTTP / tipo | Conteúdo e limite |
| --- | --- | --- |
| [Odoo 19 — Search, FR](https://www.odoo.com/documentation/19.0/fr/applications/essentials/search.html) | 200 · HTML | Título oficial em francês identificado; pesquisa/filtros/favoritos recuperados pela ferramenta Web. Leitura Web direta teve falha intermitente antes do GET bem-sucedido. |
| [Odoo 19 — Framework, SV](https://www.odoo.com/documentation/19.0/sv/developer/reference/frontend/framework_overview.html) | 200 · HTML | Título Framework Overview; arquitetura do cliente recuperada pela pesquisa oficial Web. |
| [Odoo 19 — Framework, FR](https://www.odoo.com/documentation/19.0/fr/developer/reference/frontend/framework_overview.html) | 200 · HTML | Título Framework Overview; stack de ações/breadcrumbs recuperada pela pesquisa oficial Web. Não houve execução do cliente ERP. |
| [Frappe — Desk navigation](https://docs.frappe.io/framework/user/en/desk/navigation) | 200 · HTML | Introdução lida na Web; Desktop, Dock e Sidebar documentados. Release exata da nova navegação não é declarada. |
| [Frappe — Sidebar](https://docs.frappe.io/framework/user/en/desk/navigation/sidebar) | 200 · HTML | Texto lido na Web: módulo, seções, entradas e limites de permissão. |
| [Frappe — Workspace](https://docs.frappe.io/framework/user/en/desk/navigation/workspace) | 200 · HTML | Texto lido na Web: composição, contexto de módulo, limites de acesso e fallback. |
| [Frappe — Desk](https://docs.frappe.io/framework/user/en/desk) | 200 · HTML | Texto recuperado na Web: listas/formulários e recursos. Descrição antiga da Sidebar não substitui a documentação corrente de navegação. |
| [Business Central — Role Explorer](https://learn.microsoft.com/en-gb/dynamics365/business-central/ui-role-explorer) | 200 · HTML | Texto recuperado na Web; Explorer por áreas/papéis. Alguns recursos têm referência explícita a 2025 release wave 1 e posteriores. |
| [Business Central — filtros](https://learn.microsoft.com/en-us/dynamics365/business-central/ui-enter-criteria-filters) | 200 · HTML | Página lida na Web: busca/ordenação/filtro, painel e views. |
| [Business Central — views FAQ](https://learn.microsoft.com/en-us/dynamics365/business-central/ui-views-faq) | 200 · HTML | Texto recuperado na Web: views pessoais e limitações móveis. Não houve tenant executado. |
| [Business Central — trabalhar/ações](https://learn.microsoft.com/en-us/dynamics365/business-central/ui-work-product) | 200 · HTML | Página lida na Web: action bars, dependência do contexto e ajustes de lista. A extração também mostra aviso de autorização para partes da página; não foi feito login. |
| [Business Central — mobile FAQ](https://learn.microsoft.com/en-us/dynamics365/business-central/ui-mobile-faq) | 200 · HTML | Texto recuperado na Web: diferenças dos apps móveis; não extrapolado para todo navegador móvel. |
| [SAP Fiori — FCL 1.120](https://www.sap.com/design-system/fiori-design-web/v1-120/page-types/page-layouts/flexible-column-layout) | 403 · HTML Access Denied ao HttpClient | Pesquisa Web recuperou o texto oficial detalhado da guideline; leitura Web posterior trouxe só título/imagens. Evidência de documentação indexada, sem teste de produto nem garantia de renderização integral atual. |
| [SAPUI5 — tutorial FCL 1.120](https://help.sap.com/docs/SAPUI5/3f47ec0c79a547aeaa23090b74c9520c/c4de2df385174e58a689d9847c7553bd.html?locale=en-US&state=PRODUCTION&version=1.120) | 200 · HTML, shell JS | GET confirmou SAP Help Portal; pesquisa Web recuperou título, versão 1.120 e estrutura do tutorial. Nova leitura Web não extraiu texto. Não chamar o 200 de renderização completa do artigo. |
| [SAP Fiori — novidades 1.145](https://www.sap.com/design-system/fiori-design-web/v1-148/discover/whats-new/whats-new-1-145) | 403 · HTML Access Denied ao HttpClient | Leitura e pesquisa Web recuperaram texto oficial, incluindo Side Navigation embutida/sobreposta; hospedagem na árvore 1.148 não muda o título da guideline 1.145. |
| [W3C — WCAG 2.2](https://www.w3.org/TR/WCAG22/) | 200 · HTML | Recommendation lida na Web; data mostrada 12/12/2024. Critérios normativos, sem declaração de conformidade do GenSW. |
| [APG — Disclosure](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/) | 200 · HTML | Texto recuperado na Web: botão, estado expandido e teclado. |
| [APG — Disclosure Navigation](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/) | 200 · HTML | Página lida na Web: links/listas, ausência deliberada de role menu, Escape/foco e limites dos exemplos. |
| [APG — Modal Dialog](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/) | 200 · HTML | Página lida na Web: modal real, foco dentro/retorno, Tab e Escape. |
| [Referência pública indicada](https://miro.medium.com/v2/1*99M6CrkLYinTlYa4pLnlNg.png) | 200 · PNG | Inspeção visual realizada pelo coordenador em arquivo temporário, descrita no benchmark. Autoria/produto/licença não confirmados; sem incorporação de assets. |
| [WCAG Understanding — Focus Not Obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) | 200 · HTML | Documento informativo oficial recuperado na Web; complementa o critério normativo. |
| [WCAG Understanding — Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html) | 200 · HTML | Página lida na Web; exemplos e escopo das exceções de tabela/rolagem. |
| [WCAG Understanding — Target Size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) | 200 · HTML | Documento informativo oficial recuperado na Web; complementa alvo mínimo e exceções. |

## Decisões de evidência

Foram mantidos os links oficiais SAP com limites explícitos, pois as fontes oficiais recuperadas pela Web sustentam os princípios usados. Um fallback adicional no repositório oficial SAP-docs foi investigado e teve erro transitório no GitHub/timeout da leitura; não foi usado para fortalecer uma conclusão. A pesquisa não contorna controle de acesso, cria conta, presume comportamento privado nem atribui conformidade a um produto pelo nome de seu componente.

Na revisão cruzada com o contrato, o benchmark distingue versão filha de Receita e perfil filho de Item, separa consultas de comandos, e trata preservação de contexto como contrato proposto do GenSW. O hash sintético do protótipo não constitui recomendação de colocar consultas ou dados operacionais reais em URL, armazenamento local ou logs.
