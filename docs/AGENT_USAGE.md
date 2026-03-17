# 🤖 Guia de Uso: Agentes de IA no VS Code (GeneXus MCP)

Este guia explica como integrar este projeto de MCP (Model Context Protocol) diretamente no seu fluxo de trabalho no VS Code, permitindo que agentes de IA ajudem na criação e manutenção de código GeneXus.

## 1. Instalação da Extensão Nexus-IDE

A extensão **Nexus-IDE** é fundamental. Ela fornece a interface visual e o "File System" para o VS Code entender os objetos GeneXus.

1. Localize o arquivo `nexus-ide-1.0.5.vsix` na raiz do projeto.
2. No VS Code, abra a aba **Extensions** (`Ctrl+Shift+X`).
3. Clique nos três pontos (`...`) no canto superior direito e selecione **Install from VSIX...**.
4. Selecione o arquivo e reinicie o VS Code se solicitado.

## 2. Configurando um Agente de IA no VS Code

Para usar a inteligência do MCP dentro do VS Code (além do Claude Desktop), você tem duas opções principais:

### Opção A: Cline (Recomendado)
A extensão **Cline** (antigo Claude Dev) é uma das melhores para trabalhar com MCP no VS Code.

1. Instale a extensão **Cline** no VS Code.
2. Nas configurações do Cline, procure a seção **MCP Servers**.
3. Você pode editar o arquivo de configuração e adicionar a entrada neste padrão:

```json
{
  "mcpServers": {
    "genexus-mcp": {
      "command": "C:\\Users\\mukas\\.gemini\\antigravity\\scratch\\Genexus18MCP\\publish\\start_mcp.bat",
      "args": [],
      "alwaysAllow": []
    }
  }
}
```

> [!IMPORTANT]
> Certifique-se de que o caminho em `command` aponta exatamente para onde você clonou o projeto.

### Opção B: Cursor
Se você utiliza o editor **Cursor**, ele tem suporte nativo a MCP.

1. Vá em **Settings** -> **General** -> **MCP**.
2. Clique em **+ Add New MCP Server**.
3. Escolha o tipo **Command**, dê o nome `GeneXus` e no comando cole:
   `C:\Users\mukas\.gemini\antigravity\scratch\Genexus18MCP\publish\start_mcp.bat`

---

## 3. Fluxo de Trabalho de Manutenção

Com o agente configurado, você pode fazer solicitações como:

- **Análise**: *"Analise a Procedure 'MinhaProc' e verifique se ela segue as boas práticas de GeneXus."*
- **Edição**: *"Adicione um parâmetro de entrada na Procedure 'CalcularTotal' e atualize a lógica para considerar o desconto."*
- **Navegação**: *"Onde o SDT 'Fatura' está sendo utilizado?"* (O agente usará `genexus_query`).

### Dicas de Elite:
- **`genexus_patch`**: O agente usará esta ferramenta para fazer edições cirúrgicas, preservando sua indentação.
- **`genexus_inject_context`**: Se você for fazer uma mudança complexa, peça: *"Injete o contexto recursivo da Procedure X antes de começar"*. Isso garante que o agente entenda todas as dependências (SDTs, Domínios, etc.).

## 4. Compilação (Obrigatório)

Após o agente fazer alterações no código C# (se estiver mexendo no MCP) ou nos objetos da KB, lembre-se sempre de rodar:
```powershell
.\build.ps1
```
Isso garante que o backend e as ferramentas estejam sempre sincronizados com as mudanças.
