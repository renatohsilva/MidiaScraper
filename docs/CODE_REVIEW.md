# Code Review — MídiaScraper

> Auditoria de código e arquitetura. Nenhuma alteração de código foi feita nesta etapa.

## Resumo Executivo

O MídiaScraper, apesar do nome e da descrição do projeto ("acessa a URL, identifica mídias, faz download"), **não é hoje um scraper próprio**: é uma GUI em WPF (.NET 9) que monta uma linha de comando e delega 100% do trabalho de extração/download a um processo externo, o `yt-dlp.exe`. Todo o código-fonte do produto está em **5 arquivos** e a lógica de negócio inteira (descoberta do yt-dlp, montagem de argumentos, execução do processo, parsing de progresso, estado de UI) vive em uma única classe code-behind: `MainWindow.xaml.cs` (484 linhas).

Isso tem duas consequências diretas:
1. **Arquitetura**: não há separação de responsabilidades, não há testes, e a evolução para "diferentes tipos de sites e diferentes estratégias de scraping" pedida no `CLAUDE.md` **não é viável sem refatoração estrutural** — hoje não existe nenhuma abstração de "provedor de mídia" ou "estratégia de extração"; tudo está amarrado ao yt-dlp via string de argumentos.
2. **Segurança**: a forma como a URL do usuário é injetada na linha de comando do yt-dlp tem uma vulnerabilidade real de **injeção de argumentos**, com caminho de exploração plausível (colar uma URL maliciosa copiada de outro lugar). Ver 🔴 Crítico abaixo.

Fora isso, o código é enxuto, usa `async/await` corretamente na maior parte, tem cancelamento funcional para o download em si, e a UI tem feedback razoável (log, barra de progresso, status). O principal risco não é "código ruim" no sentido de bagunça — é a ausência quase total de estrutura para um produto que pretende crescer.

---

## Pontos Positivos

- **Async/await usado corretamente** no fluxo principal: `RunYtDlpAsync` não bloqueia a UI thread, e `Dispatcher.Invoke` é usado corretamente para atualizar a UI a partir dos callbacks de I/O do processo (`MainWindow.xaml.cs:321-333`).
- **Cancelamento funcional**: `CancellationTokenSource` é propagado até `WaitForExitAsync(ct)` e um `ct.Register` mata o processo (`Kill(true)`, incluindo filhos) quando cancelado (`MainWindow.xaml.cs:340-351`). Isso é a implementação correta para matar árvores de processo no Windows.
- **Descoberta gradual do yt-dlp** (PATH → pasta do exe → download automático) é uma estratégia de fallback sensata para não exigir instalação manual (`EnsureYtDlpAsync`, `MainWindow.xaml.cs:52-90`).
- **Nullable reference types habilitado** no `.csproj` (`<Nullable>enable</Nullable>`) — boa prática moderna, ainda que pouco explorada no código atual.
- **UI com feedback em tempo real**: log de console, barra de progresso animada e badge de status dão uma sensação de "app vivo" durante o download, o que é importante para uma tarefa potencialmente longa.
- **Sem dependências externas via NuGet** além do SDK — não há risco de dependências desatualizadas/vulneráveis porque, hoje, não há nenhuma.

---

## Problemas Críticos 🔴

### 1. Injeção de argumentos no comando do yt-dlp via URL não sanitizada

**Onde:** `BuildYtDlpArgs`, `MainWindow.xaml.cs:279-303`, especialmente a linha final:
```csharp
return $"{formatArg} {subtitleArg} {playlistArg} {output} --newline --progress \"{url}\"";
```
e o uso em `RunYtDlpAsync`:
```csharp
var psi = new ProcessStartInfo
{
    FileName = _ytdlpPath,
    Arguments = args, // string única, não escapada
    ...
};
```

**Problema:** `ProcessStartInfo.Arguments` recebe uma **string de linha de comando bruta**, que o Windows tokeniza usando as regras do `CommandLineToArgvW`. A URL do usuário é interpolada dentro de aspas (`\"{url}\"`) sem qualquer *escaping*. Se a URL contiver um caractere `"`, ela encerra a string antecipadamente e tudo depois passa a ser interpretado como **novos argumentos do yt-dlp**.

**Impacto:** o yt-dlp aceita flags como `--exec <comando>` (executa um comando de shell após o download) e `-o` (sobrescreve o template de saída, permitindo escrever fora da pasta pretendida). Uma URL como:
```
https://example.com/video" --exec "calc.exe" -o "x
```
resultaria em execução de comando arbitrário no contexto do usuário assim que o download terminasse. O vetor de entrada mais realista é o próprio fluxo da UI: o botão **"Colar"** (`PasteButton_Click`, `MainWindow.xaml.cs:156-164`) copia texto da área de transferência direto para o campo de URL, e o usuário pode colar algo copiado de uma fonte não confiável (chat, fórum, "link de vídeo" compartilhado por terceiros) sem inspecionar o conteúdo antes de clicar em "Baixar".

A única validação existente é `Uri.TryCreate(url, UriKind.Absolute, out _)` (`MainWindow.xaml.cs:187`), que **não impede aspas** nem restringe o *scheme* (aceita `file://`, `ftp://`, etc., além de `http(s)://`).

**Recomendação:** trocar `ProcessStartInfo.Arguments` (string) por `ProcessStartInfo.ArgumentList` (`IList<string>`), disponível desde .NET Core, passando cada argumento (formato, legendas, playlist, output, URL) como um item separado da lista. O runtime cuida do *escaping* corretamente e a classe inteira de injeção deixa de existir. Complementarmente, validar que o *scheme* da URL é `http` ou `https` antes de prosseguir.

**Prioridade:** Máxima — corrigir antes de qualquer outra mudança de código.

---

## Problemas de Arquitetura 🟠

### 2. Toda a lógica de negócio vive em code-behind de uma única `Window`

**Onde:** `MainWindow.xaml.cs` inteiro.

**Problema:** a classe `MainWindow` acumula, ao mesmo tempo: descoberta e download do yt-dlp, montagem de argumentos de linha de comando, execução e monitoramento de processo, parsing de saída via regex, gerenciamento de estado (`_isDownloading`, `_completedDownloads`, `_outputFolder`), *e* manipulação direta de controles de UI (texto, cores, animações). Não há `ViewModel`, não há camada de serviço, não há interface alguma (`I...`) no projeto.

**Impacto:** 
- Impossível escrever testes unitários para a lógica de montagem de argumentos ou parsing de progresso sem instanciar uma `Window` WPF real.
- Qualquer mudança de estratégia de scraping (ex.: extrair imagens de uma página HTML genérica, em vez de delegar ao yt-dlp) exige mexer na mesma classe que desenha a UI, aumentando o acoplamento em vez de reduzi-lo.
- A meta declarada no `CLAUDE.md` — suportar "diferentes tipos de sites e diferentes estratégias de scraping" — **não tem onde "encaixar"** hoje: não existe um ponto de extensão (`IMediaExtractor`, `IDownloadStrategy` ou equivalente).

**Recomendação:** extrair ao menos três responsabilidades para classes próprias, independentes de WPF:
- `IYtDlpLocator` / `YtDlpBootstrapper` — localizar/baixar o executável.
- `IMediaDownloader` (implementado hoje por `YtDlpDownloader`) — recebe URL + opções, expõe eventos/`IProgress<T>` de progresso, encapsula `BuildYtDlpArgs` + `RunYtDlpAsync` + `ProcessYtDlpLine`.
- Um `ViewModel` (mesmo que light, sem framework MVVM completo) que a `MainWindow` consome via *data binding*, em vez de manipular controles diretamente em cada handler.

Isso não exige reescrever a UI — é uma refatoração incremental, e é o **pré-requisito real** para qualquer uma das funcionalidades futuras (fila de downloads, múltiplas estratégias, testes automatizados).

### 3. Nenhum tratamento de fechamento da janela durante um download em andamento

**Onde:** não existe *override* de `OnClosing` nem handler do evento `Closing` em `MainWindow.xaml.cs` ou `App.xaml.cs`.

**Problema:** se o usuário fechar a janela enquanto `_ytdlpProcess` está rodando, o processo (`yt-dlp.exe`, e potencialmente `ffmpeg.exe` que ele invoca internamente para merge de áudio/vídeo) **continua rodando em segundo plano**, órfão, sem que o `CancellationTokenSource` seja cancelado.

**Impacto:** processos zumbis consumindo CPU/disco/rede indefinidamente, sem qualquer forma de o usuário percebê-los ou pará-los pela UI (já que a janela que tinha o botão "Parar" foi fechada).

**Recomendação:** adicionar um handler de `Closing` que chama `StopDownload()` (cancela o `_cts`) antes de permitir o fechamento, ou que aguarda a finalização do processo.

**Prioridade:** Alta — é um bug de recurso real, não uma questão de estilo.

---

## Problemas de Performance 🟡

### 4. Download único, sequencial, sem fila

**Onde:** todo o fluxo gira em torno de uma única URL por vez (`StartDownloadAsync(string url)`), com a UI desabilitando o campo de URL durante o download (`SetDownloadingState`, `MainWindow.xaml.cs:429-436`).

**Problema:** não há concorrência nem fila — o usuário precisa esperar um download terminar (ou cancelar) antes de iniciar outro. Isso não é um bug, é uma limitação de escopo, mas relevante para qualquer cenário de "baixar várias mídias de uma vez", que a descrição do produto sugere.

**Impacto:** apenas de produtividade do usuário, não é um problema de correção. Tratado como *feature gap* (ver `FEATURE_RECOMMENDATIONS.md`), não como defeito.

### 5. Parsing de progresso depende de um formato de saída textual frágil

**Onde:** `ProgressRegex`, `MainWindow.xaml.cs:372-373`:
```csharp
new(@"\[download\]\s+([\d.]+)%\s+of\s+~?\s*([\d.]+\w+)\s+at\s+([\d.]+\s*\w+/s)", RegexOptions.Compiled);
```

**Problema:** o regex assume um formato específico de saída de texto do yt-dlp (em inglês, com determinada pontuação). Se uma atualização do yt-dlp mudar o formato da linha de progresso, a extração de percentual/velocidade simplesmente para de funcionar — silenciosamente, sem erro, caindo no *catch-all* (`ProcessYtDlpLine`, linha 417-418) que apenas loga a linha crua.

**Impacto:** degradação graciosa (a aplicação não quebra), mas a barra de progresso e o texto de status ficam sem atualização, sem qualquer aviso ao usuário de que o parsing falhou.

**Recomendação de longo prazo:** usar `yt-dlp --progress-template` com um formato JSON ou delimitado fixo, que é uma interface de saída suportada e estável, em vez de fazer *screen-scraping* da saída textual padrão.

---

## Problemas de Segurança 🟠

### 6. yt-dlp.exe é baixado da internet e executado sem verificação de integridade

**Onde:** `DownloadYtDlpAsync`, `MainWindow.xaml.cs:92-108`.
```csharp
const string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
...
var bytes = await http.GetByteArrayAsync(url);
await File.WriteAllBytesAsync(savePath, bytes);
```

**Problema:** o binário é baixado via HTTPS (protege o transporte) mas **não há verificação de hash/assinatura** contra o valor publicado pelo projeto yt-dlp antes de gravar e, em seguida, executar o arquivo repetidamente como processo filho com os privilégios do usuário.

**Impacto:** compromisso de cadeia de suprimentos (conta do GitHub comprometida, mirror malicioso, etc.) resultaria em execução de código arbitrário na máquina do usuário, sem qualquer barreira adicional.

**Recomendação:** validar o hash SHA-256 do binário baixado contra o publicado nos release notes/checksums do yt-dlp (o projeto publica `SHA2-256SUMS`), ou, no mínimo, verificar a assinatura Authenticode do executável antes de confiar nele.

**Prioridade:** Alta, mas menor que o item 1 — aqui o atacante precisa comprometer a distribuição oficial do yt-dlp; no item 1, basta uma URL maliciosa colada pelo próprio usuário.

### 7. Sem restrição de *scheme* de URL

**Onde:** `DownloadButton_Click`, `MainWindow.xaml.cs:187`:
```csharp
if (!Uri.TryCreate(url, UriKind.Absolute, out _))
```

**Problema:** aceita qualquer URI absoluto (`file://`, `ftp://`, etc.), não apenas `http`/`https`. Combinado com o item 1 (injeção de argumentos), amplia a superfície de entrada aceita sem necessidade.

**Recomendação:** validar explicitamente `uri.Scheme is "http" or "https"`.

---

## Problemas de Qualidade de Código 🟡

- **Mistura de concerns em um único arquivo grande**: já coberto no item 2 (arquitetura), mas vale registrar como problema de legibilidade — para entender o fluxo de download é preciso ler a classe inteira, não há um "ponto de entrada" isolado.
- **Strings mágicas repetidas**: prefixos de log com emoji (`"✅ "`, `"❌ "`, `"⚠️  "`) espalhados por múltiplos métodos sem constante central; não é um bug, mas dificulta mudança de tom/idioma da UI no futuro.
- **`FormatCombo_SelectionChanged` vazio** (`MainWindow.xaml.cs:231-234`), com comentário indicando intenção futura não implementada — código morto, mas de custo mínimo.
- **Duplicação pequena de código de cor**: `SetStatus` cria `SolidColorBrush` inline repetidamente (`MainWindow.xaml.cs:456-459`) em vez de reutilizar os `StaticResource` já definidos no XAML (`GreenGradient`/`RedGradient` existem, mas para gradiente, não brush sólido) — oportunidade pequena de reuso, não um defeito.

Esses pontos são de **estilo/manutenibilidade**, não bugs — reforçando a diferença exigida entre problema real e preferência.

---

## Problemas de Testes 🟠

**Situação atual:** não existe nenhum projeto de teste no repositório (nenhum diretório `*.Tests`, nenhuma referência a `xunit`/`nunit`/`MSTest` no `.csproj`).

**Por que isso é esperado dado o estado atual:** como toda a lógica está em code-behind de uma `Window` WPF, testar `BuildYtDlpArgs` ou `ProcessYtDlpLine` isoladamente exigiria instanciar a janela inteira (inviável em CI sem um ambiente gráfico, e frágil mesmo com um).

**Recomendação:** condicionada à extração descrita no item 2 — uma vez que `BuildYtDlpArgs`/parsing de progresso morem em uma classe sem dependência de `System.Windows`, testes unitários simples (xUnit) tornam-se triviais de escrever, inclusive um teste de regressão específico para o caso de injeção do item 1.

---

## Avaliação Arquitetural: Suporte a Múltiplas Estratégias de Scraping

O `CLAUDE.md` pergunta explicitamente se a arquitetura atual permitiria evoluir para suportar diferentes tipos de sites/estratégias. **Resposta: não, na forma atual.**

Hoje existe uma única estratégia implícita — "delegar tudo ao yt-dlp" — hard-coded dentro de `MainWindow`. yt-dlp já suporta centenas de extratores (YouTube, Twitter/X, Instagram, TikTok, etc.), então para *sites que o yt-dlp já suporta*, a arquitetura atual "funciona" por procuração. O gap real aparece para:
- **Sites genéricos não suportados pelo yt-dlp** (ex.: uma galeria de imagens qualquer), que exigiriam um scraper HTML próprio (ex.: `HtmlAgilityPack`/`AngleSharp`) — não há nenhum ponto de extensão para isso hoje.
- **Sites com conteúdo carregado via JavaScript**, que exigiriam um navegador headless (ex.: Playwright) — idem, sem abstração.

Nenhuma decisão arquitetural atual precisa ser "desfeita" para viabilizar isso — é puramente aditivo (introduzir uma interface `IMediaProvider` com `YtDlpMediaProvider` como primeira implementação), mas precisa ser feito **antes** de qualquer segunda estratégia ser adicionada, ou o código-behind vai crescer de forma insustentável.

---

## Resumo por Problema → Impacto → Recomendação → Prioridade

| # | Problema | Impacto | Recomendação | Prioridade |
|---|----------|---------|---------------|------------|
| 1 | Injeção de argumentos via URL não escapada (`MainWindow.xaml.cs:279-317`) | Execução de comando arbitrário a partir de uma URL colada | Trocar `Arguments` (string) por `ArgumentList` | 🔴 Crítica |
| 2 | Lógica de negócio 100% em code-behind, sem abstrações | Bloqueia testes e evolução para múltiplas estratégias de scraping | Extrair `IMediaDownloader`/`YtDlpBootstrapper`/ViewModel | 🟠 Alta |
| 3 | Sem handler de `Closing` para cancelar download em andamento | Processos `yt-dlp`/`ffmpeg` órfãos após fechar o app | Cancelar `_cts` e aguardar/matar processo no `Closing` | 🟠 Alta |
| 4 | yt-dlp.exe baixado sem checagem de hash/assinatura | Risco de cadeia de suprimentos | Validar SHA-256 contra checksums oficiais | 🟠 Alta |
| 5 | Sem restrição de *scheme* de URL | Amplia superfície de entrada aceita | Restringir a `http`/`https` | 🟡 Média |
| 6 | Parsing de progresso via regex frágil, dependente do texto do yt-dlp | Barra de progresso pode parar de atualizar silenciosamente após update do yt-dlp | Migrar para `--progress-template` estruturado | 🟡 Média |
| 7 | Download único sem fila/concorrência | Limitação de produtividade, não defeito | Ver `FEATURE_RECOMMENDATIONS.md` | 🟡 Média (feature) |
| 8 | Zero testes automatizados | Regressões não são detectadas | Extrair lógica testável (dep. do item 2) + suíte xUnit | 🟠 Alta |
| 9 | Código morto (`FormatCombo_SelectionChanged`) e pequenas duplicações | Ruído de manutenção mínimo | Limpeza pontual | 🟢 Baixa |
