# Segurança no Nexus IDE (Genexus18MCP)

Este documento detalha as implementações de segurança introduzidas para garantir o uso seguro do Nexus IDE em ambientes remotos e corporativos.

## 🛡️ Principais Features de Segurança

### 1. Autenticação via API Key (Gateway HTTP)
O Gateway HTTP agora exige uma chave de autenticação para todas as requisições via `/api/command`.
- **Header**: `X-API-KEY`
- **Configuração**: Adicione `"ApiKey": "sua-chave"` no `config.json` dentro da seção `"Server"`.
- **Padrão**: O servidor agora faz bind apenas em `127.0.0.1` (localhost) por padrão.

### 2. Sanitização contra Injeção de XML (RCE)
Implementamos uma camada de sanitização rigorosa para nomes de objetos GeneXus e ambientes.
- **Helper**: `SanitizationHelper.cs` (Worker)
- **Impacto**: Impede que payloads maliciosos escapem do envelope XML do MSBuild e executem comandos arbitrários no sistema operacional.
- **Regra**: Apenas caracteres alfanuméricos, underscores (`_`) e hifens (`-`) são permitidos em alvos de Build e Test.

### 3. Hardening de Processos e DLLs
- **Process Spawning**: O Gateway utiliza `ArgumentList` para iniciar o Worker, eliminando riscos de injeção de argumentos via linha de comando.
- **Path Validation**: O Worker valida rigorosamente o caminho `GX_PROGRAM_DIR` antes de carregar qualquer assembly do SDK GeneXus.

## ⚙️ Como Configurar

### Gerando uma chave de API
No PowerShell:
```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
```

### Exemplo de config.json
```json
{
  "Server": {
    "HttpPort": 5000,
    "McpStdio": true,
    "ApiKey": "COLE_SUA_CHAVE_AQUI"
  }
}
```

## 🚀 Versão da Extensão
Estas melhorias estão disponíveis a partir da versão **1.0.6** do VSIX.
