Quero que você faça uma análise completa deste projeto, que é um \*\*Media Scraper\*\*.



O funcionamento atual é simples:



\* O usuário informa a URL de um site.

\* A aplicação acessa essa URL.

\* Identifica mídias disponíveis.

\* Faz o download das mídias encontradas.



Neste momento, \*\*não quero que você saia alterando o código imediatamente\*\*.



Quero primeiro que você faça uma auditoria completa do projeto e identifique oportunidades de melhoria em três grandes áreas:



\## 1. Código e arquitetura



Analise cuidadosamente:



\* Estrutura do projeto

\* Arquitetura e separação de responsabilidades

\* Organização das classes/módulos

\* Legibilidade e manutenibilidade

\* Código duplicado

\* Complexidade desnecessária

\* Tratamento de erros e exceções

\* Async/await e concorrência

\* Gerenciamento de memória

\* Uso de CPU

\* Gerenciamento de streams e conexões HTTP

\* Cancelamento de operações

\* Timeouts

\* Retry e backoff

\* Downloads simultâneos

\* Controle de concorrência

\* Uso de recursos

\* Possíveis memory leaks

\* Segurança

\* Validação e sanitização de URLs

\* SSRF e outros riscos relacionados ao acesso de URLs fornecidas pelo usuário

\* Logs

\* Configuração

\* Testabilidade

\* Cobertura de testes

\* Dependências e bibliotecas utilizadas

\* Possíveis dependências desnecessárias ou desatualizadas



Analise também se a arquitetura atual permitiria evoluir facilmente o scraper para suportar diferentes tipos de sites e diferentes estratégias de scraping.



\---



\## 2. Interface e experiência do usuário



Faça uma análise crítica da UI/UX atual.



Considere:



\* Layout

\* Hierarquia visual

\* Responsividade

\* Feedback durante o download

\* Estados de loading

\* Estados de erro

\* Estados vazios

\* Progresso dos downloads

\* Informações apresentadas ao usuário

\* Organização das mídias encontradas

\* Preview das mídias

\* Seleção de arquivos

\* Download individual

\* Download em lote

\* Facilidade para copiar/adicionar URLs

\* Histórico

\* Feedback de sucesso

\* Mensagens de erro

\* Acessibilidade

\* Experiência em desktop

\* Experiência em mobile

\* Consistência visual

\* Microinterações e animações, quando fizerem sentido



Não quero apenas sugestões genéricas como "melhorar o design".



Quero que você identifique problemas concretos na interface atual e explique \*\*como a experiência poderia ser melhorada\*\*.



\---



\## 3. Funcionalidades



Analise o que a aplicação já oferece e pense em funcionalidades que poderiam tornar o produto significativamente melhor.



Considere, entre outras:



\* Download de múltiplas mídias

\* Download em lote

\* Seleção individual das mídias encontradas

\* Preview antes do download

\* Barra de progresso

\* Velocidade de download

\* Tamanho do arquivo

\* Tempo estimado restante

\* Cancelamento

\* Retry de downloads que falharam

\* Histórico de downloads

\* Reutilização de URLs recentes

\* Organização por pastas

\* Renomeação automática de arquivos

\* Detecção de arquivos duplicados

\* Download apenas de determinados tipos de mídia

\* Filtros

\* Ordenação

\* Download paralelo configurável

\* Limite de velocidade

\* Configurações avançadas

\* Exportação/lista das mídias encontradas

\* Melhor tratamento de sites dinâmicos

\* Suporte a páginas que carregam conteúdo via JavaScript

\* Cache quando fizer sentido

\* Observabilidade

\* Métricas

\* Logs para diagnóstico



Não presuma que todas essas funcionalidades devem ser implementadas. Avalie quais realmente fazem sentido para este projeto.



\---



\# Como quero que você faça a análise



Antes de sugerir mudanças:



1\. Explore toda a estrutura do projeto.

2\. Leia os principais arquivos de código.

3\. Identifique a stack utilizada.

4\. Entenda o fluxo completo:

&#x20;  URL → scraping → identificação das mídias → processamento → download → armazenamento → UI.

5\. Identifique os principais componentes e suas responsabilidades.

6\. Identifique gargalos e pontos frágeis.

7\. Analise como as diferentes partes da aplicação se comunicam.

8\. Verifique se existem problemas arquiteturais que podem dificultar futuras funcionalidades.



Não faça alterações no código nesta etapa.



\---



\# Resultado esperado



Depois da análise, crie:



\### `docs/CODE\_REVIEW.md`



Com:



\* Resumo executivo

\* Pontos positivos

\* Problemas encontrados

\* Problemas críticos

\* Problemas de arquitetura

\* Problemas de performance

\* Problemas de segurança

\* Problemas de qualidade de código

\* Problemas de testes

\* Recomendações

\* Exemplos concretos encontrados no código

\* Melhorias sugeridas



Para cada problema, classifique:



\* 🔴 Crítico

\* 🟠 Alto

\* 🟡 Médio

\* 🟢 Baixo



E informe:



\*\*Problema → Impacto → Recomendação → Prioridade\*\*



\---



\### `docs/UI\_UX\_REVIEW.md`



Documente:



\* Problemas atuais da interface

\* Problemas de UX

\* Oportunidades de melhoria

\* Sugestões de layout

\* Melhorias de fluxo

\* Novas interações

\* Melhorias para loading/progresso/erros

\* Sugestões de funcionalidades visuais



Sempre que possível, relacione a recomendação com um problema concreto encontrado na aplicação.



\---



\### `docs/FEATURE\_RECOMMENDATIONS.md`



Crie uma lista de funcionalidades recomendadas.



Para cada funcionalidade informe:



\* Nome

\* Descrição

\* Problema que resolve

\* Benefício para o usuário

\* Complexidade estimada: baixa / média / alta

\* Impacto: baixo / médio / alto

\* Dependências

\* Prioridade



Organize as funcionalidades em:



1\. Quick Wins

2\. Melhorias importantes

3\. Funcionalidades avançadas

4\. Funcionalidades futuras



\---



\### `docs/IMPROVEMENT\_ROADMAP.md`



Monte um roadmap baseado na análise.



Organize em fases:



\### Fase 1 — Correções e qualidade



Problemas que deveriam ser resolvidos primeiro.



\### Fase 2 — UX



Melhorias de interface e experiência.



\### Fase 3 — Funcionalidades



Novos recursos que agregam valor.



\### Fase 4 — Performance e escala



Melhorias para suportar maior volume de downloads e sites mais complexos.



\### Fase 5 — Evolução



Funcionalidades avançadas e melhorias arquiteturais.



Para cada item informe a justificativa e a prioridade.



\---



\# Regras importantes



\* Não altere código ainda.

\* Não implemente as sugestões nesta etapa.

\* Não faça mudanças apenas por preferência pessoal.

\* Baseie as recomendações no código real encontrado no projeto.

\* Não proponha uma reescrita completa sem justificar tecnicamente.

\* Preserve o que já funciona bem.

\* Diferencie claramente problemas reais de sugestões/opiniões.

\* Priorize melhorias pelo impacto x esforço.

\* Considere que o projeto deverá continuar evoluindo no futuro.

\* Se encontrar algo que pareça problemático, mostre exatamente onde e por quê.

\* Se uma decisão arquitetural atual for adequada, registre isso também.



Ao final, apresente um resumo com os \*\*10 principais problemas/melhorias que você recomenda atacar primeiro\*\*, ordenados por prioridade.



Depois de terminar toda a análise, \*\*pare e aguarde minha aprovação antes de modificar qualquer código\*\*.



