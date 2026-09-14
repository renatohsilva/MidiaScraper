# Improvement Roadmap — MídiaScraper

> Roadmap consolidado a partir de `CODE_REVIEW.md`, `UI_UX_REVIEW.md` e `FEATURE_RECOMMENDATIONS.md`. Cada fase pressupõe a anterior concluída, exceto onde indicado.

---

## Fase 1 — Correções e Qualidade

**Objetivo:** eliminar riscos reais e estabelecer a base arquitetural sem a qual as fases seguintes ficam cada vez mais caras.

| Item | Justificativa | Prioridade |
|---|---|---|
| Corrigir injeção de argumentos no comando do yt-dlp (`ProcessStartInfo.ArgumentList` em vez de string) | Vulnerabilidade com caminho de exploração realista (colar URL maliciosa); risco de execução de comando arbitrário. `CODE_REVIEW.md` #1 | 🔴 Crítica |
| Cancelar/matar processo yt-dlp ao fechar a janela (handler de `Closing`) | Processos órfãos consumindo recursos indefinidamente. `CODE_REVIEW.md` #3 | 🟠 Alta |
| Validar hash/assinatura do yt-dlp.exe baixado automaticamente | Risco de cadeia de suprimentos ao executar binário não verificado. `CODE_REVIEW.md` #6 | 🟠 Alta |
| Restringir URL a `http`/`https` | Reduz superfície de entrada aceita, reforça a correção acima. `CODE_REVIEW.md` #7 | 🟡 Média |
| Extrair lógica de negócio do code-behind (`IMediaDownloader`, `YtDlpBootstrapper`, ViewModel) | Pré-requisito para testes automatizados e para qualquer estratégia de scraping adicional nas fases 3-5. `CODE_REVIEW.md` #2 | 🟠 Alta |
| Escrever suíte de testes unitários (xUnit) para a lógica extraída, incluindo teste de regressão do caso de injeção | Hoje zero cobertura; sem isso, qualquer refatoração futura é às cegas. `CODE_REVIEW.md` #8 | 🟠 Alta |
| Migrar parsing de progresso para `--progress-template` estruturado | Parsing atual via regex é frágil a mudanças de formato do yt-dlp. `CODE_REVIEW.md` #5 | 🟡 Média |

**Por que primeiro:** a correção de injeção (item 1) é a única coisa desta auditoria com potencial de dano real hoje. A extração arquitetural (itens 5-6 desta fase) não é urgente por si só, mas **toda fase 3 em diante depende dela** — adiar só aumenta o custo de refatorar depois com mais funcionalidades já acopladas ao code-behind.

---

## Fase 2 — UX

**Objetivo:** fechar a lacuna entre o fluxo prometido ("identificar mídias → escolher → baixar") e o que a interface hoje realmente mostra.

| Item | Justificativa | Prioridade |
|---|---|---|
| Corrigir `Topmost` agressivo na inicialização | Rouba foco de outras janelas sem necessidade. `UI_UX_REVIEW.md` #9 | 🟢 Baixa |
| Adicionar `AutomationProperties.Name` aos controles com ícone-emoji | Acessibilidade para leitores de tela. `UI_UX_REVIEW.md` #10 | 🟡 Média |
| Validar contraste de texto secundário (`#475569`/`#64748B` sobre fundo escuro) | Risco de não atingir WCAG AA para texto pequeno. `UI_UX_REVIEW.md` #11 | 🟡 Média |
| Separar visualmente erros do log técnico (banner de erro dedicado) | Erros hoje se perdem misturados ao console verboso. `UI_UX_REVIEW.md` #5 | 🟡 Média |
| Estado vazio mais intencional na tela inicial | Hoje o texto de status é reaproveitado para "nunca usei" e "acabei de terminar". `UI_UX_REVIEW.md` #8 | 🟢 Baixa |
| Exibir ETA no card de progresso | Dado já existe na saída do yt-dlp, hoje descartado. `UI_UX_REVIEW.md` #4 / `FEATURE_RECOMMENDATIONS.md` 1.1 | 🟡 Média |

**Nota:** os itens de UX mais estruturais (lista de mídias, preview, seleção individual) estão deliberadamente na **Fase 3**, porque não são apenas "UX" — dependem de funcionalidades novas (preview via `--dump-json`, fila) e de a arquitetura da Fase 1 já existir.

---

## Fase 3 — Funcionalidades

**Objetivo:** entregar o conjunto de funcionalidades que muda o produto de "baixar um vídeo por vez" para "gerenciar downloads de mídia".

| Item | Justificativa | Prioridade |
|---|---|---|
| Preview de metadados antes do download (`--dump-json`/`--simulate`) | Base para as duas próximas entradas; resolve decisão "às cegas". `FEATURE_RECOMMENDATIONS.md` 2.1 | 🟠 Alta |
| Lista de mídias com seleção individual (playlists) | Resolve a lacuna central entre proposta do produto e UI atual. `FEATURE_RECOMMENDATIONS.md` 2.2 | 🟠 Alta |
| Fila de downloads / múltiplas URLs em lote | Elimina a limitação de "um download por vez". `FEATURE_RECOMMENDATIONS.md` 2.3 | 🟠 Alta |
| Histórico persistente de downloads | Contador atual é efêmero; sem histórico não há reuso nem auditoria do que já foi baixado. `FEATURE_RECOMMENDATIONS.md` 2.4 | 🟡 Média-Alta |
| Reutilização de URLs recentes | Complementa o histórico com um atalho de reentrada rápida. `FEATURE_RECOMMENDATIONS.md` 1.3 | 🟡 Média-Alta |
| Retry automático com backoff | Reduz fricção manual em falhas de rede transitórias. `FEATURE_RECOMMENDATIONS.md` 2.5 | 🟡 Média |
| Configurações persistidas (pasta, formato, concorrência padrão) | Sem isso, cada sessão recomeça do zero. `FEATURE_RECOMMENDATIONS.md` 2.6 | 🟡 Média |
| Abrir/mostrar arquivo recém-baixado; limite de velocidade | Quick wins de baixo custo, complementares às entradas acima. `FEATURE_RECOMMENDATIONS.md` 1.4/1.5 | 🟡 Média |

---

## Fase 4 — Performance e Escala

**Objetivo:** suportar volume maior de downloads e uso mais intenso, uma vez que a fila (Fase 3) já existe.

| Item | Justificativa | Prioridade |
|---|---|---|
| Download paralelo configurável | Só faz sentido depois que a fila sequencial (Fase 3) estiver estável — paralelizar antes disso multiplicaria a complexidade sem uma base testada. `FEATURE_RECOMMENDATIONS.md` 3.1 | 🟡 Média |
| Detecção de arquivos duplicados antes de baixar | Depende do histórico persistente (Fase 3) para checagem eficiente; evita desperdício em lotes grandes. `FEATURE_RECOMMENDATIONS.md` 3.2 | 🟡 Média-Baixa |
| Observabilidade / logs persistidos em arquivo (ex.: Serilog) | Necessário para diagnosticar problemas em uso mais pesado/prolongado, sem depender do console em memória. `FEATURE_RECOMMENDATIONS.md` 3.5 | 🟡 Média |

---

## Fase 5 — Evolução

**Objetivo:** funcionalidades que mudam a proposta de valor do produto (de "cliente de yt-dlp" para "scraper de mídia geral") ou que têm custo alto e demanda incerta — avaliar antes de comprometer recursos.

| Item | Justificativa | Prioridade |
|---|---|---|
| Scraping genérico para sites não suportados pelo yt-dlp (HTML estático) | É a lacuna mais direta entre a descrição do produto e a implementação real; alto impacto, mas exige a fundação arquitetural da Fase 1 já madura e testada. `FEATURE_RECOMMENDATIONS.md` 3.3 | 🟠 Alta estratégica |
| Suporte a páginas dinâmicas via navegador headless | Cobre um subconjunto de sites que nem yt-dlp nem scraping estático resolvem; custo de manutenção alto (dependência pesada). `FEATURE_RECOMMENDATIONS.md` 3.4 | 🟢 Baixa-Média — validar demanda antes |
| Renomeação automática com template customizável; exportação de lista sem baixar | Refinamentos de conveniência, baixo impacto individual. `FEATURE_RECOMMENDATIONS.md` 4.1/4.2 | 🟢 Baixa |
| Suporte multiplataforma (Avalonia/MAUI) | Custo de reescrita de UI alto; sem evidência de demanda hoje — não recomendado sem justificativa de negócio concreta. `FEATURE_RECOMMENDATIONS.md` 4.3 | 🟢 Baixa |

---

## Top 10 — Prioridade Geral de Ataque

Ordenados por prioridade real (impacto × urgência), cruzando as três análises:

1. 🔴 **Corrigir injeção de argumentos via URL** (`ArgumentList` em vez de string concatenada) — único risco de segurança com exploração realista hoje.
2. 🟠 **Extrair lógica de negócio do code-behind** (`IMediaDownloader`/ViewModel) — desbloqueia testes e toda a Fase 3-5.
3. 🟠 **Cancelar processo ao fechar a janela** — bug de recurso concreto, correção barata.
4. 🟠 **Validar hash/assinatura do yt-dlp.exe baixado** — fecha o risco de cadeia de suprimentos.
5. 🟠 **Suíte de testes unitários** para a lógica extraída (item 2), incluindo regressão do caso de injeção (item 1).
6. 🟠 **Preview de metadados antes do download** — maior alavanca de UX por esforço médio; habilita os dois itens seguintes.
7. 🟠 **Lista de mídias com seleção individual** — fecha a lacuna central entre a proposta do produto e a UI atual.
8. 🟠 **Fila de downloads / múltiplas URLs** — remove a limitação mais sentida no uso diário (um download por vez).
9. 🟡 **Histórico persistente + reutilização de URLs recentes** — memória entre sessões, hoje inexistente.
10. 🟡 **Migrar parsing de progresso para `--progress-template`** — remove a fragilidade do regex atual e viabiliza o ETA (item de UX barato e pedido explicitamente).

---

## Regras seguidas nesta auditoria

- Nenhum código foi alterado.
- Nenhuma sugestão foi implementada.
- Toda recomendação está ancorada em código real do repositório (arquivo + linha), não em opinião genérica.
- Decisões arquiteturais atuais que fazem sentido (uso de `async/await`, cancelamento via `CancellationToken`, fallback de descoberta do yt-dlp) foram registradas como pontos positivos, não como problemas.
- Reescrita completa não foi proposta em nenhum ponto — todas as recomendações são incrementais sobre a base existente.

**Próximo passo:** aguardando aprovação para iniciar a implementação, começando pela Fase 1.
