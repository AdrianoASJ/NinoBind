# NinoBind

Configurador universal de teclado e mouse para Windows 10 e 11 (x64).

A investigação do HyperX NGENUITY, a decisão de arquitetura, o plano e os limites de cada fase estão em `docs/`.

## Compilar

Requer o SDK do .NET 10.

```powershell
dotnet test NinoBind.sln
dotnet run --project src/NinoBind.App
```

## Publicar

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish.ps1
```

Gera em `artifacts\` um `.zip` com o executável autocontido, `install.ps1`, `uninstall.ps1` e `LEIA-ME.txt`. Veja `docs/distribuicao.md`.

## Dados

A configuração fica em `%LocalAppData%\NinoBind\config.json` e o log em `%LocalAppData%\NinoBind\logs\`. O log não registra teclas nem textos.

Emergency Stop: `Ctrl + Alt + Pause`. Motor: `Scroll Lock`.
