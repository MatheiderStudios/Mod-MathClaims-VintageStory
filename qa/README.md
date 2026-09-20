# Evidências de QA

## 2026-09-19 — V1 local

- `56-before-admin-qa.png`: cliente conectado à instância de teste antes da
  reinicialização de QA.
- `57-admin-gui.png`: abertura do mapa vanilla durante a sessão de teste.
- `61-after-server-restart.png`: desconexão limpa esperada após a reinicialização
  controlada para troca da DLL.

Validações automatizadas da build final:

```text
dotnet build -c Release MathClaims.csproj  -> 0 warning(s), 0 error(s)
dotnet run -c Release --project tests/MathClaims.Core.Tests.csproj -> PASS: 48 checks
```

Os 48 checks incluem geometria 16×16, adjacência, limites, ponte recusada,
split, transferência/merge, categorias de permissão entre jogadores, expiração,
seleção Shift/arraste, cache de exploração e a identificação restrita de casas
NPC/story vanilla.

O servidor local ficou escutando em `127.0.0.1:42429` com `maxclients=2`.
Uma prova multiplayer de rede com dois jogadores autenticados ainda requer uma
segunda conta Vintage Story; não foram usados UID ou credenciais fictícios.
