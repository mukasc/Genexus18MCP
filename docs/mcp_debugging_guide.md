# GeneXus MCP Server — Guia de Debugging e Configuração

> **Fonte da Verdade** — Documentação completa do processo de debugging e correções aplicadas para fazer o MCP Server funcionar com o Antigravity IDE (e qualquer cliente MCP stdio).
>
> Data: 2026-02-17 | Status: **Funcionando** ✅

---

## 1. Arquitetura Final

```
┌─────────────────────────────────────────────────────┐
│                   Antigravity IDE                    │
│  (Cliente MCP — Go-based, timeout ~10-30s)          │
└──────────┬──────────────────────────────┬────────────┘
           │ stdin (JSON-RPC requests)    │ stdout (JSON-RPC responses)
           ▼                              ▲
┌──────────────────────────────────────────────────────┐
│              GxMcp.Gateway.exe (.NET 8)              │
│                                                      │
│  ┌─────────────────────────────────────────────┐     │
│  │ RunStdioLoop() — Thread principal           │     │
│  │  • Lê stdin, roteia via McpRouter           │     │
│  │  • Responde EXCLUSIVAMENTE via _mcpOut      │     │
│  └─────────────────────────────────────────────┘     │
│                                                      │
│  ┌─────────────────────────────────────────────┐     │
│  │ InitializeBackgroundServices() — Background │     │
│  │  • Carrega config.json                      │     │
│  │  • Inicia GxMcp.Worker.exe (non-fatal)      │     │
│  │  • HTTP Server desabilitado em modo MCP     │     │
│  └─────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────┘
           │ stdin/stdout (JSON-RPC interno)
           ▼
┌──────────────────────────────────────────────────────┐
│          GxMcp.Worker.exe (.NET Framework 4.8)       │
│  • Carrega DLLs Artech.* do GeneXus 18              │
│  • Executa operações na KB (Build, Sync, Read, etc.) │
└──────────────────────────────────────────────────────┘
```

---

## 2. Problemas Encontrados e Soluções

### 2.1. `dotnet run` Causa Timeout (Context Deadline Exceeded)

**Problema:** Usar `dotnet run` no `mcp_config.json` causa latência de inicialização (~2-5s) e possível poluição do stdout com mensagens do MSBuild ("Building...", "Starting...").

**Solução:** Publicar como executável nativo e apontar direto para o `.exe`.

```powershell
# Comando de publish
dotnet publish C:\Projetos\GenexusMCP\src\GxMcp.Gateway\GxMcp.Gateway.csproj `
  -c Release -r win-x64 --self-contained false `
  -o C:\Projetos\GenexusMCP\publish
```

```json
// mcp_config.json correto (em %USERPROFILE%\.gemini\antigravity\mcp_config.json)
{
  "mcpServers": {
    "genexus": {
      "command": "C:\\Projetos\\GenexusMCP\\publish\\GxMcp.Gateway.exe",
      "args": [],
      "env": {}
    }
  }
}
```

**Tempo de resposta:** ~95ms (vs ~480ms com `dotnet run`)

---

### 2.2. stdout Pollution — O "Assassino Silencioso"

**Problema:** O protocolo MCP usa stdout **exclusivamente** para mensagens JSON-RPC. Qualquer texto extra (logs do ASP.NET, Kestrel, Console.WriteLine de debug) corrompe o protocolo e o cliente aborta.

**Solução:** Separar completamente o canal MCP do Console.Out.

```csharp
// Captura stdout exclusivamente para MCP
private static StreamWriter _mcpOut = null!;

static async Task Main(string[] args)
{
    // 1. Canal MCP exclusivo
    _mcpOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

    // 2. Console.Out → stderr (bibliotecas que chamem Console.WriteLine vão para stderr)
    var stderrWriter = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
    Console.SetOut(stderrWriter);

    // 3. Todas as respostas MCP usam APENAS _mcpOut.WriteLine()
    await RunStdioLoop();
}
```

**Regra de ouro:** NUNCA use `Console.WriteLine()` para respostas MCP. Use `_mcpOut.WriteLine()`.

**Teste ácido:** Rode o .exe manualmente. O cursor deve ficar piscando **sem nada** na tela.

```powershell
# Deve ficar em branco
& "C:\Projetos\GenexusMCP\publish\GxMcp.Gateway.exe"
# Ctrl+C para sair
```

---

### 2.3. JSON-RPC `id` Type Mismatch — O Bug Invisível (CAUSA RAIZ PRINCIPAL)

**Problema:** O cliente MCP envia `"id": 1` (número). O código C# fazia `request["id"]?.ToString()` que convertia para string. A resposta saía `"id": "1"` (string). Isso viola a spec JSON-RPC 2.0 e o Antigravity **rejeitava silenciosamente** a resposta, sem dar qualquer erro visível — apenas "context deadline exceeded".

**Como diagnosticamos:** O `mcp_debug.log` mostrou que o Antigravity enviava `initialize`, recebia a resposta, mas **nunca enviava `tools/list`**. Isso provou que a resposta era recebida mas considerada inválida.

**Solução:** Preservar o `JToken` original em vez de converter para string.

```csharp
// ERRADO — converte tipo
string id = request["id"]?.ToString();
var response = new { jsonrpc = "2.0", id = id, result = mcpResponse };
// Gera: {"id":"1",...} ← string, rejeitado pelo cliente

// CORRETO — preserva tipo original
var idToken = request["id"];
var response = new JObject
{
    ["jsonrpc"] = "2.0",
    ["id"] = idToken?.DeepClone(),
    ["result"] = JToken.FromObject(mcpResponse)
};
// Gera: {"id":1,...} ← número original preservado
```

**Impacto:** Esta foi a correção que resolveu o problema definitivamente.

---

### 2.4. Protocol Version Desatualizada

**Problema:** O Gateway respondia `protocolVersion: "2024-11-05"`, mas o Antigravity (lançado nov/2025) usa a spec `"2025-03-26"`.

**Solução:** Atualizar em `McpRouter.cs`:

```csharp
case "initialize":
    return new
    {
        protocolVersion = "2025-03-26", // era "2024-11-05"
        capabilities = new { tools = new { listChanged = true } },
        serverInfo = new { name = "genexus-mcp-server", version = "2.0.0" }
    };
```

---

### 2.5. Worker Ausente na Pasta de Publish

**Problema:** O `dotnet publish` do Gateway (.NET 8) não inclui o Worker (.NET Framework 4.8). O Gateway tentava encontrar o Worker, falhava, e escrevia erro no stderr.

**Solução:** Copiar manualmente os binários do Worker para a mesma pasta.

```powershell
Copy-Item -Path "C:\Projetos\GenexusMCP\src\GxMcp.Worker\bin\Debug\GxMcp.Worker.exe" `
  -Destination "C:\Projetos\GenexusMCP\publish\" -Force
Copy-Item -Path "C:\Projetos\GenexusMCP\src\GxMcp.Worker\bin\Debug\GxMcp.Worker.exe.config" `
  -Destination "C:\Projetos\GenexusMCP\publish\" -Force
```

**Nota:** As DLLs Artech.\* são carregadas de `C:\Program Files (x86)\GeneXus\GeneXus18\` (Private=False no .csproj), não precisam estar na pasta publish.

---

### 2.6. HTTP Server (Kestrel) Desabilitado em Modo MCP

**Problema:** O Gateway também subia um servidor HTTP na porta 5000. Se a porta estivesse ocupada ou se o Kestrel emitisse logs, poderia interferir no MCP.

**Solução:** O HTTP Server está **comentado** no modo MCP. Para usar com o Dashboard, descomentar em `InitializeBackgroundServices()`.

```csharp
// HTTP Server DISABLED for MCP-only mode — avoids port conflicts
// Uncomment when running with Dashboard:
// _ = Task.Run(async () => {
//     try { await StartHttpServer(config); }
//     catch { /* HTTP is optional */ }
// });
```

---

### 2.7. Worker Failure é Non-Fatal

**Problema:** Se o Worker falhasse ao iniciar (ex: DLLs Artech não encontradas), a exceção matava toda a `InitializeBackgroundServices()` e nenhum tool call funcionava.

**Solução:** Worker tem catch separado. O `initialize` e `tools/list` funcionam sem Worker — apenas `tools/call` precisa dele.

```csharp
try
{
    var worker = new WorkerProcess(config);
    worker.OnRpcResponse += HandleWorkerResponse;
    worker.Start();
    _worker = worker;
}
catch (Exception wex)
{
    // Worker failure is NOT fatal
    Log($"Worker start failed (non-fatal): {wex.Message}");
}
```

---

## 3. Estrutura da Pasta `publish/`

```
C:\Projetos\GenexusMCP\publish\
├── GxMcp.Gateway.exe           ← Executável principal (.NET 8)
├── GxMcp.Gateway.dll           ← Assembly
├── GxMcp.Gateway.deps.json     ← Dependências
├── GxMcp.Gateway.runtimeconfig.json
├── GxMcp.Gateway.pdb           ← Debug symbols
├── Newtonsoft.Json.dll          ← Dependência JSON
├── config.json                  ← Configuração do Gateway
├── web.config                   ← Config ASP.NET (ignorável em modo MCP)
├── GxMcp.Worker.exe             ← Worker (.NET Framework 4.8)
├── GxMcp.Worker.exe.config      ← Config do Worker
├── GxMcp.Worker.pdb             ← Debug symbols do Worker
└── mcp_debug.log                ← Log de diagnóstico (criado em runtime)
```

---

## 4. Fluxo MCP Completo (Handshake)

```
Antigravity                        Gateway
    │                                  │
    │──── initialize ─────────────────►│  (id: 1)
    │                                  │
    │◄─── initialize response ────────│  (protocolVersion: "2025-03-26")
    │                                  │
    │──── notifications/initialized ──►│  (sem resposta esperada)
    │                                  │
    │──── tools/list ─────────────────►│  (id: 2)
    │                                  │
    │◄─── tools/list response ────────│  (genexus_build, genexus_read_object)
    │                                  │
    │    [Pronto — tools aparecem]     │
    │                                  │
    │──── tools/call ─────────────────►│  (quando o usuário pede algo)
    │                                  │
    │         Gateway ──► Worker ──► GeneXus KB
    │                                  │
    │◄─── tools/call response ────────│
```

**Tempo total do handshake:** ~113ms

---

## 5. Tabela Resumo de Correções

| #   | Problema                   | Sintoma                                      | Correção                                    | Arquivo           |
| --- | -------------------------- | -------------------------------------------- | ------------------------------------------- | ----------------- |
| 1   | `dotnet run` no mcp_config | Timeout na inicialização                     | Publish como .exe                           | `mcp_config.json` |
| 2   | stdout pollution           | Protocolo corrompido                         | `_mcpOut` exclusivo, `Console.Out` → stderr | `Program.cs`      |
| 3   | `id` type mismatch         | Antigravity rejeita resposta silenciosamente | `JToken` preservando tipo                   | `Program.cs`      |
| 4   | protocolVersion velha      | Cliente pode rejeitar                        | `2024-11-05` → `2025-03-26`                 | `McpRouter.cs`    |
| 5   | Worker ausente no publish  | stderr "not found"                           | Copiar binários manualmente                 | Deploy script     |
| 6   | HTTP server conflito porta | Processo trava/crash                         | Desabilitado em modo MCP                    | `Program.cs`      |
| 7   | Worker crash bloqueia init | Nenhum tool call funciona                    | Catch separado, non-fatal                   | `Program.cs`      |

---

## 6. Comandos de Referência

### Rebuild e Publish do Gateway

```powershell
# Matar processos existentes
Get-Process -Name "GxMcp.Gateway" -ErrorAction SilentlyContinue | Stop-Process -Force

# Rebuild e publish
dotnet publish C:\Projetos\GenexusMCP\src\GxMcp.Gateway\GxMcp.Gateway.csproj `
  -c Release -r win-x64 --self-contained false `
  -o C:\Projetos\GenexusMCP\publish

# Copiar Worker (se recompilado)
Copy-Item "C:\Projetos\GenexusMCP\src\GxMcp.Worker\bin\Debug\GxMcp.Worker.exe" `
  "C:\Projetos\GenexusMCP\publish\" -Force
Copy-Item "C:\Projetos\GenexusMCP\src\GxMcp.Worker\bin\Debug\GxMcp.Worker.exe.config" `
  "C:\Projetos\GenexusMCP\publish\" -Force
```

### Teste Manual do Handshake

```powershell
$initMsg = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}'
echo $initMsg | & "C:\Projetos\GenexusMCP\publish\GxMcp.Gateway.exe" 2>$null
# Deve retornar JSON com "id":1 (número, não string)
```

### Teste Ácido (Zero stdout)

```powershell
$proc = Start-Process -FilePath "C:\Projetos\GenexusMCP\publish\GxMcp.Gateway.exe" `
  -RedirectStandardOutput "stdout_test.txt" -RedirectStandardError "stderr_test.txt" `
  -PassThru -NoNewWindow
Start-Sleep -Seconds 5
$proc | Stop-Process -Force
Get-Content "stdout_test.txt"   # DEVE ser vazio
Get-Content "stderr_test.txt"   # Pode ter logs, OK
```

### Ver Log de Diagnóstico

```powershell
Get-Content "C:\Projetos\GenexusMCP\publish\mcp_debug.log"
```

---

## 7. Lições Aprendidas

1. **O MCP é implacável com stdout.** Qualquer byte não-JSON no stdout mata a conexão.
2. **JSON-RPC `id` deve preservar o tipo original.** `1` ≠ `"1"`. Sempre use `JToken`, nunca `ToString()`.
3. **Debug com log em arquivo é essencial.** Sem o `mcp_debug.log`, nunca teríamos encontrado o bug do `id`.
4. **Background services devem ser 100% independentes.** O handshake MCP não pode depender de Worker, DB, ou qualquer recurso externo.
5. **`protocolVersion` importa.** Clientes modernos podem rejeitar versões antigas da spec.
6. **`dotnet run` ≠ produção.** Para MCP stdio, sempre publique como .exe nativo.

---

_Última atualização: 2026-02-17 22:35 (BRT)_
_Status: Funcionando — genexus 2/2 tools no Antigravity_
