# Feature Recommendations — MídiaScraper

> Lista de funcionalidades avaliadas como relevantes para este projeto especificamente, com base no código real (`MainWindow.xaml`/`MainWindow.xaml.cs`) e nas lacunas identificadas em `CODE_REVIEW.md` e `UI_UX_REVIEW.md`. Nem toda funcionalidade sugerida no `CLAUDE.md` está aqui — apenas as que fazem sentido dado o estado atual do produto.

---

## 1. Quick Wins

### 1.1 Exibir ETA (tempo restante estimado)
- **Descrição:** capturar e mostrar o tempo restante estimado que o yt-dlp já reporta na saída de progresso.
- **Problema que resolve:** o dado existe na saída do processo e é descartado (`MainWindow.xaml.cs:372-373` só captura %, tamanho e velocidade) — ver `UI_UX_REVIEW.md` item 4.
- **Benefício para o usuário:** saber quanto tempo falta sem precisar calcular mentalmente a partir de %/velocidade.
- **Complexidade:** Baixa.
- **Impacto:** Médio.
- **Dependências:** nenhuma nova; ajuste no regex/parsing existente.
- **Prioridade:** Alta.

### 1.2 Cancelar processo ao fechar a janela
- **Descrição:** matar o processo yt-dlp/ffmpeg em andamento quando a janela é fechada.
- **Problema que resolve:** processos órfãos consumindo recursos após o app ser fechado (`CODE_REVIEW.md` item 3).
- **Benefício para o usuário:** evita downloads "fantasma" continuando em segundo plano sem controle.
- **Complexidade:** Baixa.
- **Impacto:** Médio (é mais correção de bug do que feature, mas listada aqui por ser rápida).
- **Dependências:** nenhuma.
- **Prioridade:** Alta.

### 1.3 Reutilização de URLs recentes
- **Descrição:** guardar as últimas N URLs usadas (arquivo local simples) e oferecê-las como sugestão/dropdown no campo de URL.
- **Problema que resolve:** hoje não há nenhuma persistência entre sessões — cada uso começa do zero (`UI_UX_REVIEW.md` item 7).
- **Benefício para o usuário:** reexecutar um download recente (ex.: verificar novo upload do mesmo canal) sem procurar o link de novo.
- **Complexidade:** Baixa.
- **Impacto:** Médio.
- **Dependências:** nenhuma (arquivo JSON local basta).
- **Prioridade:** Média-Alta.

### 1.4 Limite de velocidade de download
- **Descrição:** expor na UI a flag `--limit-rate` já suportada nativamente pelo yt-dlp.
- **Problema que resolve:** usuários em conexões compartilhadas/limitadas não têm hoje nenhuma forma de conter o consumo de banda do app.
- **Benefício para o usuário:** usar o app sem saturar a conexão local.
- **Complexidade:** Baixa.
- **Impacto:** Baixo-Médio.
- **Dependências:** nenhuma.
- **Prioridade:** Média.

### 1.5 Abrir/mostrar o arquivo recém-baixado
- **Descrição:** botão para abrir diretamente o arquivo (não só a pasta) do download mais recente ao concluir.
- **Problema que resolve:** hoje só existe "Abrir pasta" genérico (`MainWindow.xaml:446-455`); o usuário precisa localizar o arquivo manualmente na pasta.
- **Benefício para o usuário:** um clique a menos após o download terminar.
- **Complexidade:** Baixa.
- **Impacto:** Baixo.
- **Dependências:** capturar o caminho final do arquivo a partir da saída do yt-dlp (já disponível nas linhas `[download] Destination:`).
- **Prioridade:** Média.

---

## 2. Melhorias Importantes

### 2.1 Preview de metadados antes do download
- **Descrição:** ao confirmar a URL, rodar `yt-dlp --dump-json --simulate` (sem baixar) para obter título, thumbnail, duração e, se for playlist, a contagem de itens.
- **Problema que resolve:** hoje o usuário decide "às cegas" (`UI_UX_REVIEW.md` itens 2 e 6).
- **Benefício para o usuário:** confirma que a URL resolve para o conteúdo esperado antes de gastar tempo/banda.
- **Complexidade:** Média.
- **Impacto:** Alto.
- **Dependências:** nenhuma nova (yt-dlp já suporta); exige um pouco de extração de lógica (ver `CODE_REVIEW.md` item 2, `IMediaDownloader`).
- **Prioridade:** Alta.

### 2.2 Lista de mídias com seleção individual (para playlists)
- **Descrição:** quando a URL resolver para múltiplos itens, mostrar uma lista com checkbox por item em vez de "tudo ou nada".
- **Problema que resolve:** a checkbox atual "Baixar playlist inteira" é binária, sem visibilidade do que será baixado (`UI_UX_REVIEW.md` itens 1 e 6).
- **Benefício para o usuário:** controle granular sobre o que baixar de uma playlist/álbum.
- **Complexidade:** Média.
- **Impacto:** Alto.
- **Dependências:** depende de 2.1 (preview de metadados) para popular a lista.
- **Prioridade:** Alta.

### 2.3 Fila de downloads / múltiplas URLs em lote
- **Descrição:** permitir adicionar várias URLs a uma fila e processá-las sequencialmente (ou com concorrência configurável, ver 3.1), com progresso individual por item.
- **Problema que resolve:** hoje só é possível um download por vez, com o campo de URL bloqueado durante o processo (`CODE_REVIEW.md` item 4, `UI_UX_REVIEW.md` item 3).
- **Benefício para o usuário:** baixar várias mídias sem precisar ficar voltando ao app a cada término.
- **Complexidade:** Média.
- **Impacto:** Alto.
- **Dependências:** exige a extração de `IMediaDownloader` (`CODE_REVIEW.md` item 2) e uma UI de lista (parcialmente compartilhada com 2.2).
- **Prioridade:** Alta.

### 2.4 Histórico persistente de downloads
- **Descrição:** registrar em arquivo local (JSON/SQLite) cada download concluído — URL, título, data, caminho do arquivo, status.
- **Problema que resolve:** o contador atual (`_completedDownloads`) é apenas em memória e zera a cada reinício (`UI_UX_REVIEW.md` item 7).
- **Benefício para o usuário:** consultar o que já foi baixado, quando, e reabrir arquivos antigos.
- **Complexidade:** Média.
- **Impacto:** Médio-Alto.
- **Dependências:** nenhuma obrigatória (arquivo JSON simples é suficiente para o volume esperado).
- **Prioridade:** Média-Alta.

### 2.5 Retry automático com backoff para falhas
- **Descrição:** ao detectar falha de rede (não falha de extração/URL inválida), tentar novamente automaticamente algumas vezes com espera crescente.
- **Problema que resolve:** hoje qualquer falha (`exitCode != 0`) termina o download sem nenhuma nova tentativa; o usuário precisa clicar em "Baixar" de novo manualmente.
- **Benefício para o usuário:** reduz fricção em conexões instáveis.
- **Complexidade:** Média.
- **Impacto:** Médio.
- **Dependências:** nenhuma nova (pode ser implementado sem bibliotecas, mas `Polly` simplificaria a política de retry/backoff).
- **Prioridade:** Média.

### 2.6 Configurações persistidas
- **Descrição:** salvar preferências entre sessões — pasta padrão, formato padrão, concorrência de downloads — em um arquivo de configuração local.
- **Problema que resolve:** hoje toda preferência (`_outputFolder`, formato selecionado, checkboxes) reseta a cada abertura do app.
- **Benefício para o usuário:** não precisar reconfigurar tudo a cada uso.
- **Complexidade:** Baixa-Média.
- **Impacto:** Médio.
- **Dependências:** nenhuma.
- **Prioridade:** Média.

---

## 3. Funcionalidades Avançadas

### 3.1 Download paralelo configurável
- **Descrição:** permitir baixar N itens simultaneamente (com N configurável pelo usuário), em vez de estritamente sequencial.
- **Problema que resolve:** filas grandes (playlists extensas) demoram proporcionalmente ao número de itens sem nenhuma paralelização.
- **Benefício para o usuário:** downloads em lote mais rápidos em conexões com banda disponível.
- **Complexidade:** Alta (requer gerenciar múltiplos processos `yt-dlp` simultâneos, agregação de progresso e de log por item, e controle de concorrência).
- **Impacto:** Alto.
- **Dependências:** depende de 2.3 (fila de downloads) já estar implementada.
- **Prioridade:** Média (depois da fila básica funcionar bem sequencialmente).

### 3.2 Detecção de arquivos duplicados
- **Descrição:** antes de baixar, verificar se um arquivo com o mesmo identificador de mídia (ex.: video ID do yt-dlp) já existe na pasta de destino/histórico.
- **Problema que resolve:** hoje não há verificação — o yt-dlp por padrão já evita reprocessar (`"has already been downloaded"`, tratado em `ProcessYtDlpLine`, `MainWindow.xaml.cs:392-398`), mas isso só é percebido depois de iniciar o processo, não antes, e não há aviso proativo ao usuário via UI (só no log técnico).
- **Benefício para o usuário:** evita surpresas/desperdício de tempo em lotes grandes com sobreposição.
- **Complexidade:** Média.
- **Impacto:** Médio.
- **Dependências:** depende de 2.4 (histórico persistente) para checagem eficiente.
- **Prioridade:** Média-Baixa.

### 3.3 Scraping genérico para sites não suportados pelo yt-dlp
- **Descrição:** quando a URL não corresponder a nenhum extrator do yt-dlp, fazer fallback para um scraper HTML próprio (ex.: `HtmlAgilityPack`/`AngleSharp`) que identifica tags `<img>`/`<video>`/`<source>` na página.
- **Problema que resolve:** esta é a lacuna mais direta entre a descrição do produto no `CLAUDE.md` ("acessa a URL, identifica mídias") e a implementação real, que hoje só funciona para os sites que o yt-dlp já suporta nativamente.
- **Benefício para o usuário:** amplia o produto de "cliente de yt-dlp com GUI" para um scraper de fato genérico.
- **Complexidade:** Alta.
- **Impacto:** Alto (é uma mudança de proposta de valor, não um ajuste incremental).
- **Dependências:** exige a refatoração arquitetural de `CODE_REVIEW.md` item 2 (abstração `IMediaProvider`) como pré-requisito — sem isso, esse fallback teria que ser amarrado à mesma classe monolítica.
- **Prioridade:** Alta estratégica, mas deve vir depois da fundação arquitetural.

### 3.4 Suporte a páginas dinâmicas (JS) via navegador headless
- **Descrição:** para páginas cujo conteúdo de mídia só aparece após execução de JavaScript, usar um navegador headless (ex.: Playwright) como estratégia adicional de extração.
- **Problema que resolve:** nem yt-dlp nem um scraper HTML estático (3.3) conseguem ver conteúdo renderizado via JS client-side.
- **Benefício para o usuário:** cobre sites modernos (SPAs) que hoje simplesmente não funcionam no app.
- **Complexidade:** Alta.
- **Impacto:** Médio (nicho de sites, mas sem alternativa nenhuma hoje).
- **Dependências:** mesma base arquitetural de 3.3; adiciona uma dependência pesada (motor de navegador).
- **Prioridade:** Baixa-Média — avaliar demanda real antes de pagar o custo de manutenção de um navegador headless embutido.

### 3.5 Observabilidade e logs persistidos
- **Descrição:** logging estruturado (ex.: Serilog) gravado em arquivo, com níveis (Info/Warning/Error), além do console em memória atual.
- **Problema que resolve:** hoje o log só existe na `TextBox` em memória (`LogTextBox`) e é perdido ao fechar o app ou clicar em "Limpar" (`ClearLogButton_Click`) — não há como diagnosticar um problema relatado pelo usuário depois do fato.
- **Benefício para o usuário/manutenção:** permite diagnosticar falhas reportadas sem precisar reproduzir ao vivo.
- **Complexidade:** Baixa-Média.
- **Impacto:** Médio (mais para manutenção do que para o usuário final diretamente).
- **Dependências:** biblioteca de logging (Serilog ou similar).
- **Prioridade:** Média.

---

## 4. Funcionalidades Futuras

### 4.1 Renomeação automática de arquivos com template customizável
- **Descrição:** expor o template de saída do yt-dlp (`-o`) como uma configuração de UI (ex.: `%(title)s`, `%(uploader)s/%(title)s`) em vez de fixo.
- **Problema que resolve:** hoje o template é hardcoded (`MainWindow.xaml.cs:300`).
- **Benefício:** organização de arquivos por convenção do próprio usuário.
- **Complexidade:** Baixa-Média.
- **Impacto:** Baixo-Médio.
- **Dependências:** 2.6 (configurações persistidas).
- **Prioridade:** Baixa.

### 4.2 Exportação da lista de mídias encontradas (sem baixar)
- **Descrição:** exportar metadados (título, URL, duração) de uma playlist/lote identificado para CSV/JSON, sem necessariamente baixar tudo.
- **Problema que resolve:** casos de uso de curadoria/catalogação antes de decidir o que baixar.
- **Benefício:** flexibilidade para fluxos que não terminam necessariamente em download imediato.
- **Complexidade:** Baixa.
- **Impacto:** Baixo.
- **Dependências:** 2.1 (preview de metadados).
- **Prioridade:** Baixa.

### 4.3 Suporte multiplataforma
- **Descrição:** portar a UI para um framework cross-platform (Avalonia, MAUI) caso surja demanda de suporte a macOS/Linux.
- **Problema que resolve:** hoje o app é exclusivamente Windows (WPF).
- **Benefício:** alcance de usuários fora do Windows.
- **Complexidade:** Alta.
- **Impacto:** depende inteiramente da demanda real — não há evidência no projeto atual de que isso seja necessário.
- **Dependências:** reescrita de UI.
- **Prioridade:** Baixa — não recomendado sem um motivo de negócio concreto para justificar o custo.

---

## Funcionalidades da lista do `CLAUDE.md` avaliadas e **não** recomendadas por ora

- **Cache "quando fizer sentido"**: não há hoje nenhum padrão de acesso repetido aos mesmos recursos que justifique um cache — a detecção de duplicados (3.2) e o histórico (2.4) já cobrem o cenário real de "evitar rebaixar", sem precisar de uma camada de cache formal.
- **Métricas (no sentido de telemetria/analytics)**: para um app desktop de uso pessoal sem backend, telemetria formal tem custo (privacidade, infraestrutura) desproporcional ao benefício atual; logs locais estruturados (3.5) já resolvem a necessidade real de diagnóstico.
