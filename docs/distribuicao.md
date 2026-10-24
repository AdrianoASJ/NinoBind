# Distribuição (fase 8)

## Gerar o pacote

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish.ps1
```

Saída em `artifacts\` (ignorada pelo Git): a pasta `NinoBind-<versão>-win-x64`, o `.zip` e o `.zip.sha256`. O executável é autocontido (.NET 10, win-x64, arquivo único comprimido), então o usuário não precisa instalar o .NET.

O ícone `src\NinoBind.App\Assets\ninobind.ico` é gerado por `tools\make-icon.ps1`, com o desenho próprio do NinoBind em 16 a 256 px. O executável, a janela e a bandeja usam esse arquivo.

## Por que não MSI ou MSIX

A máquina de build não tem Inno Setup nem WiX, e o projeto não instala ferramentas por conta própria. MSIX exige um certificado confiável no computador de quem instala. O pacote atual usa scripts PowerShell por usuário, sem administrador. O executável e os scripts **não são assinados**, então o SmartScreen pode avisar.

## Instalação

`install.ps1` mostra as mudanças e pede confirmação (`-Yes` pula a pergunta). Ele grava:

- `%LOCALAPPDATA%\Programs\NinoBind\` com `NinoBind.exe`, `uninstall.ps1`, `LEIA-ME.txt` e `install-manifest.json`;
- o atalho `NinoBind.lnk` no menu Iniciar do usuário;
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\NinoBind`, para aparecer em Aplicativos instalados.

Ele não liga o início com o Windows e não mexe em `%LOCALAPPDATA%\NinoBind`. Rodar de novo atualiza os arquivos e remove os que saíram do pacote. Ele recusa a instalação se o NinoBind estiver rodando da pasta de destino.

## Desinstalação

`uninstall.ps1` remove só o que o manifesto lista, e só se o manifesto apontar para a própria pasta e para a chave esperada. O valor `NinoBind` de iniciar com o Windows só é apagado quando aponta para o executável dessa pasta. As configurações do usuário ficam, a não ser com `-RemoveUserData`. Ele recusa a desinstalação com o programa aberto.

## Verificação feita nesta máquina

Windows 11 (10.0.26200), x64, PowerShell 5.1, a partir do `.zip` publicado, com `-AppId NinoBind.InstallTest` numa pasta temporária:

| Passo | Resultado |
| --- | --- |
| Instalar | Arquivos, atalho e chave de desinstalação criados; versão 0.1.0, 73744 KB |
| Abrir o exe instalado | Processo vivo com janela |
| Desinstalar com o app aberto | Recusou; nada foi removido |
| Reinstalar por cima | Concluiu |
| Desinstalar | Pasta, atalho e chave removidos; `config.json` do usuário mantido; nenhum valor em `Run` |

Não foi testado: Windows 10, a entrada de desinstalação em Aplicativos instalados acionada pela interface, `-RemoveUserData`, máquina sem .NET instalado e o aviso do SmartScreen.

## Roteiro manual (Windows 10 e 11)

Marque cada item com a versão do Windows, o build e o resultado. O suporte só vale para o que este roteiro cobrir.

1. Baixar o `.zip`, conferir o SHA-256 e extrair.
2. Rodar `install.ps1`, ler o plano e confirmar. Abrir pelo menu Iniciar.
3. **Dispositivos:** a lista mostra cada teclado e mouse conectado; sem nome HID, aparece VID/PID. Ligar **Observar entradas**, pressionar teclas e botões, conferir os eventos e **Enviar F13 de teste** aparecendo como eco ignorado.
4. **Bindings:** criar F-tecla → Esc para Qualquer dispositivo, ativar o motor e conferir no Bloco de Notas. Repetir com atalho (Ctrl+S), botão do mouse 4/5, texto e Desativar entrada.
5. Criar dois bindings na mesma entrada e conferir **Conflito** e a tecla original passando.
6. **Macros:** gravar uma macro com teclas e cliques, editar um atraso, ligar a um binding, executar. Usar **Repetir enquanto segurar** e soltar a tecla. Disparar `Ctrl + Alt + Pause` com outra janela em foco e conferir que nada fica pressionado.
7. **Perfis:** criar, duplicar, ativar outro perfil com o motor ligado, exportar, excluir e importar de volta.
8. **Configurações:** marcar Iniciar com o Windows, reiniciar a sessão, conferir o ícone na bandeja; marcar Fechar para a bandeja; desmarcar tudo e conferir o registro.
9. Abrir o programa duas vezes: a segunda abertura traz a janela existente.
10. Teclado só: navegar com Tab e `Ctrl + 1…6`. Narrador ou NVDA lendo listas e o rodapé. Alto contraste e escala de 150%.
11. Desinstalar por Aplicativos instalados e conferir que pasta, atalho e chave sumiram e que `%LOCALAPPDATA%\NinoBind` ficou.
12. Repetir com um jogo ou programa que recuse entrada sintetizada e anotar como limitação, sem contornar.
