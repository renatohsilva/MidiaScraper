# Implementation Plan — MídiaScraper

> Plano de implementação derivado de `CODE_REVIEW.md`, `UI_UX_REVIEW.md`, `FEATURE_RECOMMENDATIONS.md` e `IMPROVEMENT_ROADMAP.md`. Para cada item: arquivos alterados, arquivos novos, impactos possíveis e posição na ordem segura de implementação. **Nenhum código foi alterado nesta etapa.**

## Como ler este documento

- Cada fase espelha `IMPROVEMENT_ROADMAP.md`, quebrada em passos menores, no tamanho aproximado de um commit/PR.
- Cada passo lista **Arquivos alterados**, **Arquivos novos**, **Impactos possíveis** e **Depende de**.
- Ao final de cada fase, o passo seguinte só deve começar depois que o app for testado manualmente rodando (build + fluxo de download real), não só compilando.
- Duas decisões de design não têm uma resposta única "correta" e estão marcadas explicitamente como **⚠️ Decisão pendente** — a lista completa está consolidada na seção final.

### Estrutura de pastas alvo (resultado ao fim da Fase 3)

```
MidiaScraper/
  App.xaml, App.xaml.cs
  MainWindow.xaml, MainWindow.xaml.cs        (permanecem na raiz — sem mover)
  MidiaScraper.csproj
  Models/
    DownloadOptions.cs
    DownloadProgressInfo.cs
    MediaMetadata.cs
    DownloadHistoryEntry.cs
    AppSettings.cs
  Services/
    YtDlp/
      IYtDlpLocator.cs, YtDlpLocator.cs
      YtDlpArgumentBuilder.cs
      YtDlpProgressParser.cs
      IMediaDownloader.cs, YtDlpMediaDownloader.cs
      IMediaMetadataProvider.cs, YtDlpMetadataProvider.cs
    Downloads/
      DownloadQueue.cs
      DownloadHistoryStore.cs
    Settings/
      SettingsStore.cs
  ViewModels/
    MainViewModel.cs
    DownloadItemViewModel.cs
MidiaScraper.Tests/
  MidiaScraper.Tests.csproj
  YtDlpArgumentBuilderTests.cs
  YtDlpProgressParserTests.cs
MidiaScraper.sln                              (novo — agrega os dois projetos)
docs/ ...
```

Decisão explícita: **`MainWindow.xaml`/`MainWindow.xaml.cs` não serão movidos** para uma pasta `Views/` — mover arquivos existentes sem necessidade funcional aumenta o diff e o risco de cada passo sem benefício real nesta fase. Só criamos pastas novas para código novo.

---

## Fase 1 — Correções e Qualidade (fundação)

### Passo 1.1 — Corrigir injeção de argumentos + restringir scheme da URL 🔴

- **Arquivos alterados:** `MainWindow.xaml.cs` (`BuildYtDlpArgs`, `RunYtDlpAsync`, `DownloadButton_Click`)
- **Arquivos novos:** nenhum
- **Impactos possíveis:**
  - `ProcessStartInfo.Arguments` (string única) vira `ProcessStartInfo.ArgumentList` (`IList<string>`). Cada fragmento hoje concatenado em uma string (`-f "bestvideo..."`, `--merge-output-format mp4`, etc.) precisa virar itens separados da lista (ex.: `"-f"`, `"bestvideo[height<=1080]+bestaudio/best[height<=1080]"`, `"--merge-output-format"`, `"mp4"`) — é uma reescrita mecânica de `BuildYtDlpArgs`, mas todos os 5 formatos + legendas + playlist + output template precisam ser conferidos um a um para não perder nenhuma flag na conversão.
  - Validação de `uri.Scheme is "http" or "https"` adicionada em `DownloadButton_Click`, ao lado da validação `Uri.TryCreate` já existente.
  - **Teste manual obrigatório antes de prosseguir:** baixar um vídeo real nos 5 formatos, com e sem legendas, com e sem playlist, para confirmar que a lista de argumentos gerada continua equivalente à string antiga. Idealmente logar a `ArgumentList` resultante (`AppendLog`) durante o desenvolvimento para comparação visual com o comportamento anterior.
- **Depende de:** nada — é o primeiro passo do plano inteiro por ser o único risco de segurança com exploração realista hoje.

### Passo 1.2 — Cancelar processo ao fechar a janela 🟠

- **Arquivos alterados:** `MainWindow.xaml.cs` (adicionar handler de `Closing`, reaproveitando `StopDownload()`), `MainWindow.xaml` (wire `Closing="MainWindow_Closing"` no elemento `<Window>`, ou usar `override OnClosing` no code-behind sem tocar o XAML)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** matar o processo deve ser síncrono e rápido (`Process.Kill(true)` já é usado hoje via `ct.Register`); não deve bloquear o fechamento da janela por muito tempo — evitar `await` longo dentro do handler de `Closing` (que não é `async`-friendly de forma nativa no WPF). Risco baixo, mudança pequena e isolada.
- **Depende de:** nada — pode ser feito em paralelo ao Passo 1.1, mas está listado depois por ser conceitualmente independente e menor.

### Passo 1.3 — Extrair arquitetura (Services/Models), incorporando os fixes 1.1–1.2 🟠

Sub-passos, cada um migrando uma responsabilidade específica do `MainWindow.xaml.cs` já corrigido pelos passos anteriores:

**1.3a — `Services/YtDlp/YtDlpLocator.cs` (+ `IYtDlpLocator.cs`)**
- Migra: `EnsureYtDlpAsync`, `DownloadYtDlpAsync`, `CheckYtDlpVersionAsync`, `FindOnPath`.
- **Novo, dentro deste sub-passo:** verificação de integridade do binário baixado (item 6 do `CODE_REVIEW.md`). ⚠️ **Decisão pendente** — ver seção final. Implementar apenas a opção escolhida; não implementar as duas.
- **Arquivos alterados:** `MainWindow.xaml.cs` (remove os métodos migrados, passa a chamar `_ytdlpLocator.EnsureAsync()`)
- **Arquivos novos:** `Services/YtDlp/IYtDlpLocator.cs`, `Services/YtDlp/YtDlpLocator.cs`

**1.3b — `Services/YtDlp/YtDlpArgumentBuilder.cs`**
- Migra: `BuildYtDlpArgs` (já em formato `ArgumentList` graças ao Passo 1.1) + `Models/DownloadOptions.cs` como parâmetro de entrada (substitui ler `FormatCombo`/`SubtitleCheck`/`PlaylistCheck` diretamente).
- **Arquivos alterados:** `MainWindow.xaml.cs` (monta um `DownloadOptions` a partir dos controles e chama o builder)
- **Arquivos novos:** `Services/YtDlp/YtDlpArgumentBuilder.cs`, `Models/DownloadOptions.cs`

**1.3c — `Services/YtDlp/YtDlpProgressParser.cs`**
- Migra: `ProgressRegex` + `ProcessYtDlpLine`. Retorna um objeto (`Models/DownloadProgressInfo.cs`: `Percent`, `SizeText`, `SpeedText`, `RawLine`, `Kind` enum como `Progress`/`Info`/`Warning`/`Destination`) em vez de chamar `AppendLog`/`SetProgress` diretamente.
- **Arquivos alterados:** `MainWindow.xaml.cs` (recebe o `DownloadProgressInfo` via callback/`IProgress<T>` e decide como renderizar)
- **Arquivos novos:** `Services/YtDlp/YtDlpProgressParser.cs`, `Models/DownloadProgressInfo.cs`

**1.3d — `Services/YtDlp/YtDlpMediaDownloader.cs` (+ `IMediaDownloader.cs`)**
- Migra: `RunYtDlpAsync`, composição do processo (`ProcessStartInfo`, `OutputDataReceived`/`ErrorDataReceived`, `WaitForExitAsync`, `ct.Register` para kill). Usa `YtDlpArgumentBuilder` e `YtDlpProgressParser` internamente. Expõe `Task<DownloadResult> DownloadAsync(string url, DownloadOptions options, IProgress<DownloadProgressInfo> progress, CancellationToken ct)`.
- **Arquivos alterados:** `MainWindow.xaml.cs` — `StartDownloadAsync` vira um método curto que monta `DownloadOptions`, chama `_downloader.DownloadAsync(...)` e atualiza a UI a partir do `IProgress<T>`/resultado. `_ytdlpProcess` como campo de `MainWindow` deixa de existir (passa a viver dentro de `YtDlpMediaDownloader`).
- **Arquivos novos:** `Services/YtDlp/IMediaDownloader.cs`, `Services/YtDlp/YtDlpMediaDownloader.cs`

- **Impactos possíveis (do Passo 1.3 como um todo):**
  - É o passo de maior superfície de risco da Fase 1: toca todos os handlers de botão indiretamente. Deve ser feito com o app rodando e testado manualmente a cada sub-passo (não acumular os 4 sub-passos sem testar).
  - Critério de aceite arquitetural: nenhuma classe em `Services/`/`Models/` deve referenciar `System.Windows.*` — esse é o requisito que viabiliza os testes do Passo 1.4.
  - Comportamento visível ao usuário deve permanecer idêntico ao fim deste passo — é refatoração, não redesenho (o redesenho começa na Fase 2).
  - `MidiaScraper.csproj`: nenhuma dependência nova é necessária aqui — o code-behind continua orquestrando manualmente (sem `ViewModel`/binding completo ainda). ⚠️ Ver decisão pendente sobre adotar `CommunityToolkit.Mvvm` — se a decisão for "sim", este é o ponto onde o pacote seria adicionado; se "não", ele só entra na Fase 3.
- **Depende de:** Passos 1.1 e 1.2 (os fixes devem existir antes de serem movidos, para não misturar "corrigir" com "mover" no mesmo diff).

### Passo 1.4 — Criar projeto de testes + suíte unitária 🟠

- **Arquivos novos:**
  - `MidiaScraper.sln` (agrega `MidiaScraper.csproj` e o novo projeto de teste)
  - `MidiaScraper.Tests/MidiaScraper.Tests.csproj` (SDK-style, referencia xUnit + `MidiaScraper.csproj`)
  - `MidiaScraper.Tests/YtDlpArgumentBuilderTests.cs` — deve incluir **um teste de regressão explícito** para o caso de injeção do Passo 1.1 (URL contendo `"` não deve gerar um token de argumento extra na `ArgumentList`)
  - `MidiaScraper.Tests/YtDlpProgressParserTests.cs`
- **Arquivos alterados:** nenhum arquivo de produção
- **Impactos possíveis:** nenhum em runtime do app (projeto de teste é isolado); `bin/`/`obj/` do novo projeto já são cobertos pelo `.gitignore` existente (padrões `bin/`/`obj/` sem prefixo de caminho casam em qualquer profundidade), não precisa editar `.gitignore`.
- **Depende de:** Passo 1.3 completo (só há o que testar depois que a lógica sai do code-behind).

### Passo 1.5 — Migrar parsing de progresso para `--progress-template` estruturado 🟡

- **Arquivos alterados:** `Services/YtDlp/YtDlpArgumentBuilder.cs` (adicionar flag `--progress-template` com formato fixo, ex. delimitado por um caractere improvável de aparecer em nomes de arquivo), `Services/YtDlp/YtDlpProgressParser.cs` (trocar o regex de texto livre por parsing do formato estruturado), `Models/DownloadProgressInfo.cs` (adicionar campo `Eta`)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** muda o formato exato das linhas de progresso que chegam via stdout — o log de console (`LogTextBox`) pode passar a exibir menos "ruído" bruto do yt-dlp nessas linhas específicas (já que agora são parseadas de forma estruturada em vez de logadas cruas). Validar visualmente que o console continua útil, não vazio. Habilita o campo ETA para a Fase 2 (Passo 2.6).
- **Depende de:** Passo 1.4 (ter testes já escritos para `YtDlpProgressParser` reduz o risco de regressão ao trocar a estratégia de parsing).

**Checkpoint de fim de Fase 1 — ✅ concluído em 2026-09-13:**
- Testado manualmente: download real (YouTube), cancelamento, fechamento da janela durante download — nenhum `yt-dlp.exe`/`ffmpeg.exe` órfão no Gerenciador de Tarefas.
- `dotnet test` — 28/28 testes passando (`MidiaScraper.Tests`).
- Nota de execução: a solução foi gerada como `MidiaScraper.slnx` (formato novo do SDK .NET 10 instalado), não `.sln` — equivalente funcionalmente ao que este documento previa.
- **Bug real encontrado e corrigido durante a validação do Passo 1.5** (fora do escopo original, mas necessário para não regredir comportamento visível): o `--progress-template` expõe o percentual bruto do yt-dlp a cada tick, e em downloads fragmentados (DASH/HLS) o tamanho total é reestimado a cada fragmento, fazendo o percentual oscilar para baixo antes de subir de novo. O reporter textual padrão do yt-dlp escondia isso por sobrescrever a mesma linha no terminal; nossa barra animada expunha cada oscilação. Corrigido travando a exibição para nunca regredir (`MainWindow.xaml.cs`, campo `_maxProgressPercent` + `RenderProgressInfo`), mantendo velocidade/tamanho exibidos sem alteração.

---

## Fase 2 — UX

### Passo 2.1 — Remover `Topmost` agressivo 🟢

- **Arquivos alterados:** `MainWindow.xaml` (remover `Topmost="True"`), `MainWindow.xaml.cs` (`MainWindow_Loaded`: remover `await Task.Delay(500)` + `Topmost = false`, manter apenas `Activate()`)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** nenhum funcional; verificar apenas que a janela ainda aparece em primeiro plano ao abrir em um cenário normal (múltiplas janelas abertas).
- **Depende de:** nada (independente de tudo na Fase 1).

### Passo 2.2 — Acessibilidade: `AutomationProperties.Name` em controles com ícone 🟡

- **Arquivos alterados:** `MainWindow.xaml` (`PasteButton`, `DownloadButton`, `FolderButton`, `StopButton`, `OpenFolderButton`, `ClearLogButton`, `StatusDot`)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** nenhum visual; puramente aditivo (atributos XAML novos).
- **Depende de:** nada.

### Passo 2.3 — Validar/ajustar contraste de texto secundário 🟡

- **Arquivos alterados:** `MainWindow.xaml` (possível ajuste dos hex `#475569`/`#64748B` usados em `ProgressStatus`, `DownloadCountText`, `OutputFolderText` e rótulos)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** passo com uma etapa manual **antes** da edição — rodar as combinações de cor atuais em uma ferramenta de contraste (ex. WebAIM) e só então decidir se os tons precisam mudar; não é só "trocar código", é validar primeiro.
- **Depende de:** nada.

### Passo 2.4 — Estado vazio mais intencional 🟢

- **Arquivos alterados:** `MainWindow.xaml` (texto/visibilidade inicial da seção de progresso), `MainWindow.xaml.cs` (lógica de mostrar/ocultar a seção conforme o estado: nunca usado vs. download ativo vs. concluído)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** baixo; cuidado para não esconder informação que o usuário espera ver sempre disponível (ex. contador de downloads no rodapé deve continuar visível).
- **Depende de:** nada.

### Passo 2.5 — Banner de erro dedicado, separado do console técnico 🟡

- **Arquivos alterados:** `MainWindow.xaml` (novo `Border`/`TextBlock` de banner de erro, oculto por padrão, posicionado entre o cartão de progresso e o console), `MainWindow.xaml.cs` (nos pontos onde hoje só se loga erro — branch de exceção em `StartDownloadAsync`/callback de erro do `IMediaDownloader` — também popular e exibir o banner)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** precisa decidir a regra de quando o banner desaparece (ex.: ao iniciar um novo download, ou manualmente pelo usuário) para não "grudar" um erro antigo na tela indefinidamente.
- **Depende de:** Passo 1.3d (o `IMediaDownloader` já precisa existir para ter um único ponto de captura de erro a conectar ao banner).

### Passo 2.6 — Exibir ETA no card de progresso 🟡

- **Arquivos alterados:** `MainWindow.xaml` (novo `TextBlock` de ETA, ou concatenar ao `ProgressStatus` existente), `MainWindow.xaml.cs` (ler `DownloadProgressInfo.Eta`)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** nenhum além de layout — texto adicional na linha de status de progresso.
- **Depende de:** Passo 1.5 (campo `Eta` só existe depois da migração para `--progress-template`).

**Checkpoint de fim de Fase 2 — implementação concluída em 2026-09-13, aguardando validação visual/Narrator do usuário:**
- 2.1 — `Topmost` e o delay de 500ms removidos; `Activate()` sozinho na inicialização.
- 2.2 — `AutomationProperties.Name` em todos os botões com ícone (`PasteButton`, `DownloadButton`, `FolderButton`, `StopButton`, `OpenFolderButton`, `ClearLogButton`); `StatusDot` recebe um nome dinâmico (`Status: {texto}`) atualizado a cada chamada de `SetStatus`, já que é um indicador só de cor.
- 2.3 — validado com a fórmula de contraste da WCAG 2.1: `#475569` sobre `#16161F` dava ~2.37:1 e `#64748B` ~3.78:1, ambos abaixo do mínimo de 4.5:1 para texto pequeno. Consolidados para `#94A3B8` (~7:1), tom já usado em outros lugares do app.
- 2.4 — card de progresso some até o primeiro download; nesse meio-tempo, um estado vazio explícito ("Cole uma URL acima e clique em Baixar para começar") ocupa o espaço.
- 2.5 — banner de erro dedicado (vermelho, com botão de fechar), acionado quando `IMediaDownloader` retorna código de saída ≠ 0 ou lança uma exceção inesperada; não aparece para cancelamento pelo usuário. Desaparece automaticamente ao iniciar um novo download, ou manualmente pelo X.
- 2.6 — ETA exibido ao lado do texto de status do progresso (`ProgressEta`), populado a partir do campo `DownloadProgressInfo.Eta` (Passo 1.5); oculto quando o valor é vazio/"Unknown"/"NA".
- `dotnet test`: 28/28 passando; build limpo.
- **Pendente do usuário:** revisão visual da janela e teste com o Narrator do Windows nos controles principais (a IA não consegue interagir com a GUI diretamente).

---

## Fase 3 — Funcionalidades

> Esta fase é onde a UI deixa de ser "1 download por vez" para virar "lista/fila gerenciada". É o ponto natural para decidir a questão de MVVM (ver decisão pendente).

### Passo 3.1 — Preview de metadados antes do download 🟠

- **Arquivos novos:** `Services/YtDlp/IMediaMetadataProvider.cs`, `Services/YtDlp/YtDlpMetadataProvider.cs` (roda `yt-dlp --dump-json --flat-playlist --simulate`, parseia com `System.Text.Json`), `Models/MediaMetadata.cs` (`Title`, `ThumbnailUrl`, `DurationSeconds`, `IsPlaylist`, `Entries: IReadOnlyList<MediaEntry>`)
- **Arquivos alterados:** `MainWindow.xaml` (novo cartão de preview: thumbnail via `Image` com `Source` de URI http(s) — suportado nativamente pelo WPF, sem dependência nova —, título, duração), `MainWindow.xaml.cs` (chamar o provider antes de habilitar o botão "Baixar" definitivo; estado de carregamento enquanto aguarda)
- **Impactos possíveis:** adiciona uma chamada de rede/processo extra antes de cada download (latência adicional de ~1–3s); precisa de um indicador de carregamento para não parecer travado; precisa tratar falha de obtenção de metadados sem bloquear o fluxo por completo (fallback: permitir baixar mesmo sem preview, com aviso).
- **Depende de:** Fase 1 completa (usa a mesma base de `Services/YtDlp`).

### Passo 3.2 — Lista de mídias com seleção individual (playlists) 🟠

- **Arquivos novos:** `ViewModels/DownloadItemViewModel.cs` (`Title`, `ThumbnailUrl`, `IsSelected`, `Status`, `ProgressPercent`) — implementação de notificação de mudança conforme a decisão pendente de MVVM
- **Arquivos alterados:** `MainWindow.xaml` (substituir/estender o cartão de preview por um `ItemsControl`/`ListBox` com checkbox por item, exibido apenas quando `MediaMetadata.IsPlaylist` for verdadeiro), `MainWindow.xaml.cs` (popular a coleção a partir de `MediaMetadata.Entries`; "selecionar todos/nenhum"; ao clicar em "Baixar", iterar apenas os itens marcados)
- **Impactos possíveis:** maior mudança visual da Fase 3 na área principal da janela. **Importante preservar o caminho rápido do caso comum**: quando a URL não é playlist (a maioria dos usos hoje), a lista não deve aparecer — deve continuar sendo "1 clique para baixar", sem etapas extras impostas ao caso simples.
- **Depende de:** Passo 3.1.

### Passo 3.3 — Fila de downloads / múltiplas URLs em lote 🟠

- **Arquivos novos:** `Services/Downloads/DownloadQueue.cs` (processa uma `ObservableCollection<DownloadItemViewModel>` sequencialmente, chamando `IMediaDownloader` por item), `ViewModels/MainViewModel.cs` (se ainda não formalizado no Passo 3.2 — estado agregado: pendentes/ativos/concluídos)
- **Arquivos alterados:** `MainWindow.xaml` (view da fila: itens pendente/ativo/concluído, progresso por item em vez de uma única barra global), `MainWindow.xaml.cs` (delega a `DownloadQueue`; handlers de botão ficam mais finos)
- **Impactos possíveis:** é o passo que mais muda o **modelo de interação** do app (de single-shot para fila). ⚠️ **Decisão de UX a confirmar antes de implementar:** o botão "Parar" deve cancelar só o item atual, ou a fila inteira? Recomendação: cancelar apenas o item ativo por padrão, com uma ação separada e explícita para "limpar/cancelar fila inteira" — mas isso deve ser confirmado com o usuário no momento da implementação, não assumido agora.
- **Depende de:** Passo 3.2 (reaproveita `DownloadItemViewModel`).

### Passo 3.4 — Histórico persistente de downloads 🟡

- **Arquivos novos:** `Models/DownloadHistoryEntry.cs` (`Url`, `Title`, `CompletedAt`, `FilePath`, `Status`), `Services/Downloads/DownloadHistoryStore.cs` (lê/grava JSON em `%AppData%\MidiaScraper\history.json` via `System.Text.Json`)
- **Arquivos alterados:** `MainWindow.xaml` (novo painel/aba de histórico), `ViewModels/MainViewModel.cs` (gravar uma entrada ao concluir cada item da fila)
- **Impactos possíveis:** primeiro estado persistido em disco fora da pasta de downloads do usuário — tratar ausência do arquivo (primeira execução) e conteúdo corrompido (JSON inválido) sem crashar o app, apenas iniciando com histórico vazio e logando o problema.
- **Depende de:** Passo 3.3 (o ponto de conclusão de item na fila é o gatilho natural para gravar histórico).

### Passo 3.5 — Reutilização de URLs recentes 🟡

- **Arquivos alterados:** `MainWindow.xaml` (dropdown/sugestão no campo de URL), `MainWindow.xaml.cs`
- **Arquivos novos:** nenhum — reaproveita `DownloadHistoryStore` (Passo 3.4) como fonte, em vez de criar um segundo arquivo de persistência só para URLs recentes.
- **Impactos possíveis:** baixo, aditivo.
- **Depende de:** Passo 3.4.

### Passo 3.6 — Configurações persistidas 🟡

- **Arquivos novos:** `Models/AppSettings.cs` (`OutputFolder`, `DefaultFormat`, `DefaultSubtitles`, `DefaultPlaylist`, `MaxConcurrentDownloads` — este último usado só na Fase 4), `Services/Settings/SettingsStore.cs` (JSON em `%AppData%\MidiaScraper\settings.json`)
- **Arquivos alterados:** `MainWindow.xaml.cs` (carregar configurações no `MainWindow_Loaded` em vez dos valores hardcoded atuais; salvar ao trocar pasta/formato ou ao fechar)
- **Impactos possíveis:** muda o comportamento de primeira execução vs. execuções seguintes (hoje `_outputFolder` sempre reinicia em `Downloads`; passa a lembrar a última pasta usada) — é a mudança de comportamento pretendida, mas vale documentar no changelog/PR.
- **Depende de:** nada estrutural — pode ser feito em paralelo a 3.1–3.5 se for conveniente adiantar.

### Passo 3.7 — Retry automático com backoff 🟡

- **Arquivos alterados:** `Services/YtDlp/YtDlpMediaDownloader.cs` (laço de retry com backoff exponencial ao redor da tentativa de download)
- **Arquivos novos:** nenhum (implementação manual, sem adicionar Polly — mantém a superfície de dependências pequena para um único ponto de uso)
- **Impactos possíveis:** precisa distinguir falha de rede/timeout (deve tentar de novo) de falha de URL inválida/conteúdo não encontrado (não deve tentar de novo) — essa classificação de erro é nova e precisa ser explicitada nos códigos de saída/mensagens do yt-dlp. Sem isso, o retry pode mascarar um erro definitivo atrás de tentativas inúteis. Também deve informar visualmente "Tentativa 2 de 3..." para não parecer travado.
- **Depende de:** Fase 1 completa.

### Passo 3.8 — Quick wins: abrir arquivo recém-baixado + limite de velocidade 🟡

- **Arquivos alterados:** `Services/YtDlp/YtDlpMediaDownloader.cs` (capturar o caminho final do arquivo a partir das linhas `[download] Destination:`/`[Merger]`, incluir no resultado), `Models/DownloadOptions.cs` (campo opcional `RateLimit`), `Services/YtDlp/YtDlpArgumentBuilder.cs` (adicionar `--limit-rate` quando definido), `MainWindow.xaml` (botão "Abrir arquivo" pós-conclusão; campo/preset de limite de velocidade), `MainWindow.xaml.cs`
- **Arquivos novos:** nenhum
- **Impactos possíveis:** baixo, aditivo.
- **Depende de:** Fase 1 completa; pode ser feito a qualquer momento da Fase 3.

**Checkpoint de fim de Fase 3:** testar um fluxo completo com uma playlist real pequena (2–3 itens): preview → seleção → fila → conclusão → aparece no histórico → aparece nas URLs recentes.

---

## Fase 4 — Performance e Escala

### Passo 4.1 — Observabilidade / logs persistidos (Serilog) 🟡

- **Arquivos alterados:** `MidiaScraper.csproj` (adicionar `Serilog` + `Serilog.Sinks.File`), `App.xaml.cs` (bootstrap do logger no startup), pontos em `Services/*` e `MainWindow.xaml.cs` que hoje só chamam `AppendLog` passam a também logar estruturado
- **Arquivos novos:** `Services/Logging/LoggingSetup.cs` (configuração central do Serilog)
- **Impactos possíveis:** **primeira dependência NuGet externa do projeto** — hoje o `CODE_REVIEW.md` registra "zero dependências" como ponto positivo; essa troca é deliberada e deve ser comunicada como tal. Definir política de retenção de arquivo de log (ex.: 7 dias ou tamanho máximo) para não crescer indefinidamente em `%AppData%`.
- **Depende de:** Fase 1 completa (idealmente feito primeiro nesta fase, para já ajudar a diagnosticar os dois passos seguintes, que são mais arriscados).

### Passo 4.2 — Download paralelo configurável 🟡

- **Arquivos alterados:** `Services/Downloads/DownloadQueue.cs` (controle de concorrência via `SemaphoreSlim(maxConcurrent)`), `MainWindow.xaml` (controle de "downloads simultâneos" na UI), `Models/AppSettings.cs` (campo já estava reservado no Passo 3.6)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** passo de maior risco da Fase 4 — múltiplos processos `yt-dlp`/`ffmpeg` simultâneos precisam ter logs/progresso isolados por item (só é seguro porque a Fase 3 já entregou a UI baseada em lista, com progresso por item), e há risco de contenção de banda/disco entre os itens simultâneos. Testar explicitamente com uma playlist real e concorrência > 1 para confirmar que não há mistura de saída entre processos.
- **Depende de:** Passo 4.1 (logs ajudam a diagnosticar problemas de concorrência) e Fase 3 completa.

### Passo 4.3 — Detecção de arquivos duplicados 🟡

- **Arquivos alterados:** `Services/Downloads/DownloadHistoryStore.cs` (adicionar consulta por identificador de mídia), ponto de enfileiramento em `ViewModels/MainViewModel.cs` (checar antes de adicionar à fila)
- **Arquivos novos:** nenhum
- **Impactos possíveis:** baixo; usa o campo `id` já disponível via `MediaMetadata` (Passo 3.1) como chave estável, em vez de comparar por nome de arquivo (frágil).
- **Depende de:** Passo 3.1 (metadados) e Passo 3.4 (histórico).

**Checkpoint de fim de Fase 4:** baixar uma playlist real com concorrência 3, observar uso de CPU/rede, confirmar nenhum processo órfão ao final.

---

## Fase 5 — Evolução

### Passo 5.1 — Scraping genérico para sites não suportados pelo yt-dlp 🟠 (estratégica)

- **Arquivos novos:**
  - `Services/Scraping/IMediaProvider.cs` (contrato: `bool CanHandle(string url)`, `Task<MediaMetadata> ExtractAsync(string url, CancellationToken ct)`)
  - `Services/Scraping/YtDlpMediaProvider.cs` (adapta `YtDlpMediaDownloader`/`YtDlpMetadataProvider` existentes ao novo contrato)
  - `Services/Scraping/GenericHtmlMediaProvider.cs` (busca a página via `HttpClient`, extrai `<img>`/`<video>`/`<source>` com AngleSharp)
  - `Services/Scraping/MediaProviderSelector.cs` (tenta `YtDlpMediaProvider`; se não suportado, cai para `GenericHtmlMediaProvider`)
- **Arquivos alterados:** `MidiaScraper.csproj` (adicionar `AngleSharp`), `ViewModels/MainViewModel.cs` (chamar o selector em vez de ir direto ao yt-dlp)
- **Impactos possíveis:** maior mudança de escopo do roadmap inteiro — muda a proposta de valor do produto. Considerações de segurança próprias desse novo caminho: aplicar a mesma restrição de *scheme* `http`/`https` (Passo 1.1) também aqui, usar timeout e limite de tamanho de resposta ao buscar HTML de terceiros, e não seguir redirects para faixas de IP privadas/loopback se este código algum dia rodar em qualquer contexto multi-usuário (hoje é um app desktop de usuário único, então o risco é predominantemente teórico, mas a mitigação é barata o suficiente para incluir).
- **Depende de:** Fase 1 completa (arquitetura), idealmente Fase 3 completa (para reaproveitar a UI de preview/lista com o novo tipo de fonte).

### Passo 5.2 — Suporte a páginas dinâmicas via navegador headless 🟢 (validar demanda antes)

- **Arquivos novos:** `Services/Scraping/HeadlessBrowserMediaProvider.cs`
- **Arquivos alterados:** `MidiaScraper.csproj` (adicionar `Microsoft.Playwright` + passo de instalação dos binários do navegador no processo de build/primeira execução), `MediaProviderSelector.cs` (mais um fallback)
- **Impactos possíveis:** dependência pesada (binários de navegador, ~centenas de MB, download extra na primeira execução/CI), aumenta significativamente o tempo de build/setup. **Não iniciar sem confirmar demanda real primeiro** — é a única recomendação de todo o roadmap com essa ressalva explícita.
- **Depende de:** Passo 5.1 (mesma base arquitetural de providers).

### Passo 5.3 — Renomeação automática customizável + exportação de lista sem baixar 🟢

- **Arquivos alterados:** `Services/YtDlp/YtDlpArgumentBuilder.cs` (template de saída customizável a partir de `AppSettings`), `Models/AppSettings.cs` (campo `OutputTemplate`), `MainWindow.xaml` (campo de configuração)
- **Arquivos novos:** `Services/Export/MediaListExporter.cs` (exporta `MediaMetadata.Entries` para CSV/JSON)
- **Impactos possíveis:** baixo, funcionalidades de conveniência independentes entre si.
- **Depende de:** Passo 3.6 (configurações) e Passo 3.1 (metadados) respectivamente.

### Passo 5.4 — Suporte multiplataforma — **não planejado em detalhe**

Conforme já registrado no roadmap: exigiria reescrever a camada de UI (Avalonia/MAUI) em um projeto essencialmente novo, fora do escopo de um plano incremental sobre a base WPF atual. Não recomendado sem uma justificativa de negócio concreta — não há passos de arquivo detalhados aqui de propósito.

---

## Ordem Segura Consolidada (visão linear)

| Ordem | Passo | Fase | Prioridade |
|---|---|---|---|
| 1 | 1.1 Corrigir injeção de argumentos + scheme da URL | 1 | 🔴 |
| 2 | 1.2 Cancelar processo ao fechar a janela | 1 | 🟠 |
| 3 | 1.3 Extrair arquitetura (Services/Models) | 1 | 🟠 |
| 4 | 1.4 Projeto de testes + suíte unitária | 1 | 🟠 |
| 5 | 1.5 Migrar progresso para `--progress-template` | 1 | 🟡 |
| 6 | 2.1 Remover `Topmost` agressivo | 2 | 🟢 |
| 7 | 2.2 `AutomationProperties.Name` | 2 | 🟡 |
| 8 | 2.3 Validar/ajustar contraste | 2 | 🟡 |
| 9 | 2.4 Estado vazio intencional | 2 | 🟢 |
| 10 | 2.5 Banner de erro dedicado | 2 | 🟡 |
| 11 | 2.6 Exibir ETA | 2 | 🟡 |
| 12 | 3.1 Preview de metadados | 3 | 🟠 |
| 13 | 3.2 Lista de mídias com seleção | 3 | 🟠 |
| 14 | 3.3 Fila de downloads | 3 | 🟠 |
| 15 | 3.4 Histórico persistente | 3 | 🟡 |
| 16 | 3.5 URLs recentes | 3 | 🟡 |
| 17 | 3.6 Configurações persistidas | 3 | 🟡 |
| 18 | 3.7 Retry com backoff | 3 | 🟡 |
| 19 | 3.8 Abrir arquivo + limite de velocidade | 3 | 🟡 |
| 20 | 4.1 Logs persistidos (Serilog) | 4 | 🟡 |
| 21 | 4.2 Download paralelo configurável | 4 | 🟡 |
| 22 | 4.3 Detecção de duplicados | 4 | 🟡 |
| 23 | 5.1 Scraping genérico (HTML estático) | 5 | 🟠 |
| 24 | 5.2 Navegador headless (validar demanda) | 5 | 🟢 |
| 25 | 5.3 Template de renomeação + exportação | 5 | 🟢 |

Passos 3.6, 3.8 e 4.1 têm baixa dependência do restante e podem ser adiantados dentro de suas fases se for útil entregar vitórias visíveis mais cedo — a ordem acima é a mais segura, não a única ordem tecnicamente possível.

---

## Decisões (resolvidas em 2026-09-13)

### ✅ 1. Verificação de integridade do yt-dlp.exe baixado (Passo 1.3a)

**Decidido: opção (a) — hash SHA-256** contra o arquivo `SHA2-256SUMS` publicado no mesmo release do GitHub. Sem dependência nova (`System.Security.Cryptography`). Verificação GPG (opção b) fica descartada por ora — pode ser revisitada depois se o nível de risco aceito mudar.

### ✅ 2. Biblioteca de MVVM

**Decidido: `CommunityToolkit.Mvvm`**, a partir do Passo 3.2. Será a única dependência nova de produção antes da Fase 4 (Serilog).

### ✅ 3. Semântica de cancelamento na fila (Passo 3.3)

**Decidido: "Parar" cancela apenas o item ativo.** Os demais itens da fila permanecem aguardando.

---

## Regras seguidas neste plano

- Nenhum código foi alterado.
- Cada passo é dimensionado para ser testável isoladamente antes do próximo.
- Toda extração de arquitetura preserva o comportamento visível existente — mudanças de comportamento só acontecem nos passos que são explicitamente sobre funcionalidade nova (Fases 2 em diante).
- Novas dependências externas foram minimizadas e, quando propostas, sinalizadas explicitamente como uma mudança de trade-off (não são neutras).
- As três decisões acima estão em aberto deliberadamente — este plano não assume respostas em nome do usuário.

**Próximo passo:** aguardando aprovação para iniciar a implementação a partir do Passo 1.1, e decisão sobre os itens ⚠️ 1 e ⚠️ 2 antes dos Passos 1.3a e 3.2, respectivamente.
