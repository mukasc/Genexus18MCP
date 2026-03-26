# Proposta Técnica: AI Agents + GeneXus (GeneXus 18 MCP)

Este documento detalha o funcionamento, a segurança e a eficiência de recursos do projeto **GeneXus 18 MCP**, visando sua adoção como piloto no ambiente corporativo.

## 1. Visão Geral: O que é o GeneXus 18 MCP?

O projeto é uma ponte de comunicação de alta performance que permite que **Agentes de IA** (como Gemini, Claude ou assistentes no VS Code) interajam de forma nativa e segura com a **Knowledge Base (KB)** do GeneXus 18. 

Diferente de abordagens simples de "copiar e colar", esta ferramenta utiliza o **SDK Nativo da GeneXus**, permitindo análise semântica profunda, refatoração segura e automação de tarefas complexas diretamente nos objetos da KB (Source, Rules, Events, SDTs, etc.).

## 2. Arquitetura Técnica

A solução utiliza uma arquitetura desacoplada para garantir máxima estabilidade e compatibilidade:

*   **Gateway (Coordenador - .NET 8 Core):** Atua como o cérebro da comunicação. É extremamente leve, lida com o protocolo MCP e gerencia a configuração em tempo real (Hot Reload).
*   **Worker (Executor - .NET 4.8 STA):** Um processo especializado que carrega as DLLs originais da GeneXus. Ele roda no modo *Single-Threaded Apartment (STA)*, o mesmo modo que o IDE da GeneXus utiliza, garantindo 100% de fidelidade e estabilidade ao manipular a KB.
*   **Nexus-IDE:** Uma extensão para VS Code que transforma o editor em um "Mini GeneXus IDE", permitindo que o desenvolvedor tenha o poder da IA e a visualização da KB em um único lugar.

## 3. Segurança (Data Privacy & Integrity)

A segurança foi a prioridade número um no design da ferramenta:

*   **Residência de Dados (Local-First):** O servidor MCP roda **localmente** na máquina do desenvolvedor ou em um servidor on-premise da empresa. O código da KB **não é armazenado** em servidores externos pelo projeto.
*   **Isolamento de Processos:** O Worker (que toca na KB) é isolado do Gateway. A IA nunca tem acesso direto ao sistema de arquivos da KB; ela solicita ações que são validadas e higienizadas pelo servidor antes de serem executadas.
*   **Sanitização de Inputs:** Todas as entradas (nomes de objetos, parâmetros) passam pelo `SanitizationHelper`, prevenindo ataques de injeção ou execuções maliciosas.
*   **Controle de Acesso:** O sistema suporta `ApiKey` para garantir que apenas agentes/usuários autorizados possam se conectar ao Gateway.
*   **Tráfego Protegido:** O transporte de código entre as camadas utiliza um pipeline Base64, garantindo integridade dos dados e proteção contra corrupção de caracteres especiais ou acentuação (comum em código GeneXus).

## 4. Consumo de Recursos (Cloud & Infra)

O impacto na infraestrutura é mínimo, tornando-o ideal para ambientes corporativos:

*   **Processamento Sob Demanda:** O servidor consome CPU e RAM apenas durante as requisições. Em repouso, o consumo é insignificante.
*   **Indexing Otimizado:** A indexação da KB (para buscas rápidas da IA) ocorre em background e de forma incremental. Ela não exige servidores de banco de dados pesados; utiliza cache local otimizado.
*   **Zero Custo de Nuvem Adicional:** Como a ferramenta roda na infraestrutura existente (Workstation ou Servidor Local), não há custos de compute em nuvem (AWS/Azure/GCP). O único tráfego externo é a comunicação com o modelo de IA (API de tokens), que é controlada e monitorada.
*   **Sem Dependências Pesadas:** Não é necessário instalar instâncias completas de SQL Server ou IIS para o MCP funcionar; ele é um binário autônomo.

## 5. Por que usar como Piloto?

1.  **Aumento Crítico de Produtividade:** Redução dramática no tempo de criação de Procedures, Data Providers e testes unitários.
2.  **Qualidade de Código:** A IA pode realizar "Linter" preventivo, verificando se o código segue as diretrizes da empresa antes mesmo de salvar.
3.  **Modernização Sem Risco:** Permite usar as ferramentas mais modernas do mercado (VS Code, Agentes IA) sem abandonar a solidez do ecossistema GeneXus 18.
4.  **Treinamento Acelerado:** Facilita a entrada de novos desenvolvedores no projeto, pois a IA ajuda a navegar e explicar a arquitetura da KB atual.

---
**Status da Ferramenta:** Estável, pronta para implantação em ambiente de desenvolvimento controlado.
