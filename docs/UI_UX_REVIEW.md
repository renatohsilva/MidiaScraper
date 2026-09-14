# UI/UX Review — MídiaScraper

> Análise crítica da interface atual. Nenhuma alteração de código foi feita nesta etapa.

## Contexto

A UI atual (`MainWindow.xaml`) é uma janela única, tema escuro, com quatro seções empilhadas verticalmente: cartão de URL/opções, cartão de progresso, console de log, e rodapé com contador/ações. Visualmente é coerente (paleta consistente, cantos arredondados, gradiente de destaque), mas a estrutura da tela revela uma lacuna funcional importante: **a interface foi desenhada para "baixar 1 vídeo", não para "explorar e escolher mídias de uma página"**, que é o que a descrição do produto promete.

---

## Problemas Concretos Identificados

### 1. Não existe nenhuma lista de mídias encontradas

**Onde:** ausente — não há `ListView`/`ItemsControl` em `MainWindow.xaml`.

**Problema:** o fluxo descrito no `CLAUDE.md` é *URL → identifica mídias → usuário escolhe/baixa*. A UI atual pula direto de "URL" para "download completo": o usuário clica em "Baixar" e o yt-dlp resolve e baixa o que encontrar, sem nunca mostrar ao usuário **o que foi encontrado** antes de baixar. Se a URL for uma playlist e a checkbox "Baixar playlist inteira" estiver marcada, o usuário não vê a lista de vídeos antes de consumir tempo/banda baixando todos.

**Como melhorar:** introduzir uma etapa intermediária — após submeter a URL, mostrar uma lista (mesmo que simples: título + thumbnail + duração) dos itens identificados, com checkbox por item, antes de iniciar o download. Isso também resolve a ausência de "seleção individual" e "preview antes do download" pedidas no `CLAUDE.md`.

### 2. Nenhum preview de mídia

**Problema:** não há thumbnail, nem nome de arquivo esperado, nem duração exibidos antes do clique em "Baixar" — o usuário decide "às cegas". yt-dlp oferece `--dump-json`/`--simulate` que retornam metadados sem baixar o conteúdo; essa informação hoje não é usada.

**Como melhorar:** ao colar/confirmar uma URL, disparar uma chamada "leve" (`--dump-json`, sem download) para popular um cartão de preview (thumbnail, título, duração) antes de liberar o botão "Baixar" definitivo.

### 3. Uma única barra de progresso global, sem granularidade por item

**Onde:** `ProgressFill`/`ProgressPercent`/`ProgressStatus`, `MainWindow.xaml:344-381`.

**Problema:** consequência direta do item 1 — como não há lista de itens, só existe um progresso "do download atual". Em uma playlist, o usuário não sabe "estou no vídeo 3 de 20", apenas vê o progresso do arquivo sendo baixado no momento (e o log de texto, que é verboso).

**Como melhorar:** condicionado à introdução da lista de itens (item 1): progresso por item na própria linha da lista, e um progresso agregado (ex.: "7 de 20 concluídos") no cabeçalho.

### 4. ETA descartado, apesar de disponível

**Onde:** `ProgressRegex`, `MainWindow.xaml.cs:372-373`, captura apenas percentual, tamanho e velocidade.

**Problema:** a saída padrão do yt-dlp (`[download] 45.2% of 120.5MiB at 3.20MiB/s ETA 00:32`) normalmente inclui um campo de tempo restante, mas o regex atual não o captura — a informação existe na saída do processo e é jogada fora.

**Como melhorar:** ajustar a extração (idealmente via `--progress-template`, ver `CODE_REVIEW.md` item 6) para expor também o ETA, e mostrar "tempo restante estimado" na seção de progresso — item explicitamente pedido no `CLAUDE.md` e tecnicamente barato de entregar.

### 5. Console de log técnico como principal fonte de feedback de erro

**Onde:** `LogTextBox`, `MainWindow.xaml:413`; todo erro/aviso é escrito ali via `AppendLog` (`MainWindow.xaml.cs:462-466`).

**Problema:** mensagens de erro (`"❌ Erro inesperado: ..."`, `"⚠️ {stderr}"`) ficam misturadas no mesmo fluxo que linhas informativas e de progresso brutas do yt-dlp (`[youtube] Extracting URL...`, etc.). Para um usuário não técnico, é difícil localizar "o que deu errado" em meio a um console rolando texto. Não existe um banner de erro dedicado nem qualquer diferenciação visual (cor, ícone destacado, posição) para uma falha real versus uma linha informativa comum — tudo tem a mesma fonte monoespaçada azul-clara.

**Como melhorar:** manter o console como log técnico "avançado" (talvez colapsável/opcional), mas adicionar uma área de status primária e visualmente distinta para erros (ex.: banner vermelho acima do console, com a mensagem resumida), já que `SetStatus`/`StatusBadge` existem mas mostram apenas uma palavra curta ("Erro"), sem detalhe.

### 6. Nenhuma confirmação antes de baixar uma playlist inteira

**Onde:** `PlaylistCheck`, `MainWindow.xaml:326-328`.

**Problema:** marcar "Baixar playlist inteira" e clicar em "Baixar" inicia imediatamente o download de todos os itens, sem indicar quantos são, quanto espaço/tempo isso pode consumir, ou pedir confirmação. Combinado com a ausência de lista de itens (item 1), o usuário só descobre o tamanho real do trabalho observando o log rolar.

**Como melhorar:** ao detectar uma playlist (via metadata leve, item 2), mostrar a contagem de itens e pedir confirmação explícita antes de iniciar um download potencialmente longo.

### 7. Sem histórico nem reutilização de URLs recentes

**Onde:** ausente — `UrlTextBox` é um `TextBox` simples, sem `ComboBox`/sugestões, e nada é persistido entre sessões.

**Problema:** cada sessão começa do zero; não há como reabrir/reprocessar uma URL usada anteriormente, nem visualizar o que já foi baixado nesta ou em sessões passadas — apenas um contador efêmero (`DownloadCountText`) que zera ao reiniciar o app.

**Como melhorar:** persistir um histórico simples (URL, título, data, pasta de destino) em arquivo local, exibido em um painel/lista acessível (ex.: aba ou painel lateral), com opção de reenviar uma URL do histórico para o campo principal.

### 8. Estado inicial ("empty state") pouco intencional

**Onde:** `ProgressStatus` mostra `"Aguardando URL..."` por padrão (`MainWindow.xaml:378-379`), dentro do próprio cartão de progresso.

**Problema:** não há uma distinção clara entre "nunca usei o app" e "acabei de concluir/cancelar um download" — o texto de status é reaproveitado para os dois casos, e a seção de progresso permanece sempre visível mesmo quando não há nada em andamento, ocupando espaço fixo da tela mesmo no estado ocioso.

**Como melhorar:** um estado vazio mais explícito (ex.: ilustração/texto orientando "Cole uma URL para começar") na área principal, com a seção de progresso aparecendo apenas quando há um download ativo ou recém-concluído.

### 9. Janela força-se à frente na inicialização (`Topmost`)

**Onde:** `MainWindow.xaml:9` (`Topmost="True"` no XML) e `MainWindow.xaml.cs:31-36`:
```csharp
await Task.Delay(500);
Topmost = false;
Activate();
```

**Problema:** por 500ms a janela é marcada como "sempre no topo" de todas as outras janelas do sistema, mesmo as de outros aplicativos, antes de voltar ao normal. Esse é um padrão agressivo — se o usuário estiver interagindo com outra janela no momento em que o app termina de carregar, o MídiaScraper pode roubar o foco/visibilidade momentaneamente sem ação do usuário.

**Como melhorar:** usar `Activate()`/`WindowState` normalmente, sem `Topmost`, que é o padrão esperado de uma janela desktop comum; se o objetivo é apenas garantir que a janela apareça na frente ao abrir, `Activate()` sozinho já resolve isso na maioria dos casos.

### 10. Ícones somente em emoji, sem texto alternativo

**Onde:** botões e badges usam emoji como único indicador visual — `"📋"` (Colar), `"⬇"`/`"⏹"` (Baixar/Parar), `"📁"`/`"📂"` (Pasta), `"⏹"` (Parar) — `MainWindow.xaml:274-455`.

**Problema:** os emojis renderizam de forma inconsistente entre versões do Windows/fontes instaladas, e não têm `AutomationProperties.Name` associado — leitores de tela não anunciam a função do botão além do texto visível ao lado (que existe na maioria, mas não em todos os casos, como o botão "Limpar" do log que usa só texto, ok, mas o ícone do `StatusDot` e da bolinha animada não tem nenhuma descrição textual para tecnologia assistiva).

**Como melhorar:** definir `AutomationProperties.Name` nos controles interativos, e considerar um conjunto de ícones vetoriais (Segoe Fluent Icons ou SVG) em vez de depender de emoji para consistência visual entre máquinas.

### 11. Contraste de texto potencialmente abaixo do recomendado

**Onde:** diversos `TextBlock` usam `Foreground="#475569"` ou `Foreground="#64748B"` sobre fundo `#0F0F13`/`#16161F` (ex.: `ProgressStatus`, `DownloadCountText`, `OutputFolderText`, rótulos "URL DO VÍDEO"/"PROGRESSO").

**Problema:** `#475569` sobre `#0F0F13` fica próximo do limite recomendado pela WCAG 2.1 AA para texto pequeno (razão de contraste ≥ 4.5:1); não foi possível validar automaticamente nesta auditoria (auditoria estática, sem execução da UI), mas é um ponto que merece verificação com uma ferramenta de contraste antes de assumir que está adequado, especialmente para texto de 11-12px como `ProgressStatus`.

**Como melhorar:** validar com um contrast checker (ex.: WebAIM) as combinações de cor de texto secundário sobre os fundos escuros usados, e ajustar os tons se necessário.

### 12. Sem indicação de qual pasta será usada, além de um único botão global

**Onde:** `FolderButtonText`/`OutputFolderText`, `MainWindow.xaml:330-338, 430-431`.

**Problema:** a pasta de destino é única e global para todos os downloads da sessão — não há como, por exemplo, direcionar um download específico para uma pasta diferente sem trocar a pasta padrão antes. Combinado com a ausência de fila (ver `CODE_REVIEW.md` item 4), isso é uma limitação menor hoje, mas se a fila for implementada, vira uma lacuna relevante de fluxo.

**Como melhorar:** tratado junto com a introdução de fila/lista de downloads — permitir pasta por item, com a pasta global como padrão.

---

## O que já funciona bem (não mudar sem motivo)

- Feedback textual do status geral (badge "Pronto"/"Erro"/"Concluído" com ponto colorido pulsante) é um padrão de UX reconhecível e funcional — manter.
- Atalho de teclado Enter no campo de URL para disparar o download (`UrlTextBox_KeyDown`) é um detalhe de usabilidade correto e deve ser preservado em qualquer redesenho.
- O botão "Baixar" vira "Parar" durante o download (mesma posição, texto/ícone dinâmico) — bom padrão de affordance, evita um segundo botão competindo por atenção.
- Paleta de cores é consistente entre os elementos (roxo/índigo como destaque, verde para sucesso, vermelho para erro/parar) — a linguagem visual está coerente, o problema é de informação exibida, não de estilo.

---

## Síntese

A maior parte dos problemas de UX não são de "visual" (o tema escuro/gradientes está bem executado para o que existe), mas de **informação ausente**: não há lista, não há preview, não há histórico, não há ETA — todos consequência direta de a aplicação tratar cada download como um evento único e opaco, em vez de um processo com etapas visíveis (identificar → selecionar → acompanhar → concluir) que o próprio `CLAUDE.md` descreve como o fluxo desejado.
