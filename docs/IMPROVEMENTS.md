# GeneXus 18 MCP - Relatório de Melhorias e Correções

Este documento consolida todas as evoluções técnicas, correções de bugs e melhorias de interface implementadas no GeneXus 18 MCP durante esta sessão de desenvolvimento.

## 🚀 Infraestrutura e Conectividade

*   **Resiliência de Rede (127.0.0.1)**: Migração de todas as chamadas internas para `127.0.0.1` em vez de `localhost`. Isso resolve problemas de resolução de nome e latência em ambientes com configurações complexas de proxy ou IPv6.
*   **Pipeline de Comunicação**: O método `callGateway` na extensão VS Code foi refatorado para encapsular comandos automaticamente, tornando a comunicação com o Worker mais transparente e menos propensa a erros de formatação.

## 💾 Persistência e Confiabilidade (Salve/Escrita)

*   **Estratégia de Salvamento Multi-Camada (Auto-Healing)**: Implementado um fluxo de persistência robusto em `WriteService.cs`:
    1.  Tentativa primária via `part.Save()`.
    2.  Tentativa secundária via `obj.EnsureSave()` em caso de falha silenciosa.
    3.  Fallback final via `obj.Save()`.
*   **Redução de Ruído do SDK**: Adicionado tratamento para o erro genérico "Erro" do SDK da GeneXus. O sistema agora verifica se a operação foi bem-sucedida em alto nível, evitando que a IDE reporte falhas falsas.
*   **Integridade de Código (Base64)**: Introduzida sinalização explícita `isBase64` no transporte de código-fonte, garantindo que caracteres especiais e acentuação sejam preservados sem corrupção durante o salvamento.
*   **Fila de Segundo Plano (BackgroundQueue)**: Operações pesadas como abertura de KB e Indexação agora são gerenciadas por uma fila thread-safe, evitando travamentos e condições de corrida.

## 🔍 UI e Descoberta (Nexus-IDE)

*   **Busca Avançada (Advanced Search)**: Nova interface Webview que permite buscas complexas filtrando por:
    *   Tipo de Objeto (Procedure, Transaction, etc.).
    *   Data de Modificação (Filtros `after:` e `before:`).
    *   Conteúdo, Nome e Descrição.
*   **Hierarquia de Referências (References View)**: Nova visualização que mostra um grafo bidirecional de chamadas:
    *   **Called By**: Quais objetos utilizam o objeto atual.
    *   **Calls**: Quais objetos são chamados pelo objeto atual.
*   **Comando de Indexação Manual**: Adicionado o comando `nexus-ide.forceIndexing` para que o usuário possa disparar o processamento de busca manualmente quando necessário.

## 🛠️ Estabilidade Geral

*   **Refatoração do SearchService**: Motor de busca mais inteligente com suporte a metadados e filtragem granular.
*   **Resiliência no FileSystem**: Melhorado o processamento de diretórios (`readDirectory`) para lidar com diferentes formatos de resposta do Gateway de forma segura.

---
*Gerado automaticamente pelo Antigravity em 15/03/2026*
