# Estabilização da Integração Nativa com SDK GeneXus 18

Este documento detalha as mudanças técnicas realizadas para permitir que o `GxMcp.Worker` interaja de forma nativa e estável com as Knowledge Bases (KBs) do GeneXus 18.

## Desafios Resolvidos

### 1. Compatibilidade de Arquitetura (Bitness)
O SDK do GeneXus 18 é composto por DLLs de 32 bits (x86). O Worker estava sendo compilado ou executado como AnyCPU/64 bits, o que causava erros de `BadImageFormatException` ou falhas silenciosas ao carregar dependências nativas.
- **Solução:** Forçado o `PlatformTarget` para `x86` no arquivo `.csproj` do Worker.

### 2. Bootstrapping do SDK
O GeneXus 18 requer uma sequência específica de inicialização que não está documentada para aplicações console standalone. Tentativas iniciais com `UIServices` e `ArtechServices` falharam por mudanças na versão 18.
- **Solução:** Identificada a classe `Artech.Core.Connector` no assembly `Connector.dll` como o ponto de entrada correto. A inicialização agora segue a ordem:
    1. `Artech.Core.Connector.Initialize()`
    2. `Artech.Core.Connector.StartBL()`
    3. `Artech.Architecture.UI.Framework.Services.UIServices.SetDisableUI(true)` (via Reflexão)

### 3. Resolução Dinâmica de Dependências
O SDK possui centenas de DLLs e pacotes que residem na pasta de instalação do GeneXus, mas não são copiados para a pasta `publish`.
- **Solução:** Implementado um manipulador para o evento `AppDomain.CurrentDomain.AssemblyResolve`. Este manipulador busca automaticamente as DLLs faltantes nos seguintes caminhos:
    - Pasta raiz do GeneXus (`gxPath`)
    - Pasta de Packages (`gxPath\Packages`)
    - Pasta de Patterns (`gxPath\Packages\Patterns`)

### 4. Configuração do Enterprise Library
O SDK do GeneXus utiliza o Microsoft Enterprise Library para log e tratamento de exceções. Sem a configuração correta no `App.config`, qualquer erro interno no SDK causava um `NullReferenceException` recursivo no próprio tratador de erros, mascarando a causa real.
- **Solução:** Adicionadas as seções `<exceptionHandling>`, `<loggingConfiguration>` e `<cachingConfiguration>` ao `GxMcp.Worker.exe.config`, permitindo que o SDK logue erros corretamente e que o Worker capture a stack trace real.

### 5. Centralização do Gerenciamento de KB (`KbService`)
A lógica de abrir a KB e inicializar o SDK estava duplicada e inconsistente.
- **Solução:** Criado o `KbService.cs` para gerenciar a instância única (`static`) da `KnowledgeBase`. Todos os outros serviços (`ObjectService`, `ListService`, etc.) agora dependem do `KbService` via injeção de dependência.

## Verificação de Funcionalidade
Após estas mudanças, as seguintes ferramentas MCP foram validadas com sucesso em KBs reais:
- `genexus_list_objects`: Retorna a lista de objetos filtrados.
- `genexus_read_object`: Retorna o XML completo do objeto, incluindo GUIDs de tipos e partes internas.

## Requisitos de Execução
Para que o sistema funcione, o caminho do GeneXus configurado no `config.json` deve estar correto e conter todas as DLLs originais da instalação.
