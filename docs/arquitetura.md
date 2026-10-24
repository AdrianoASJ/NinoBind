# Arquitetura do NinoBind

Decisão registrada antes da implementação. O NGENUITY usa Avalonia e .NET 10. Isso descreve o produto de referência. Não escolhe a tecnologia do NinoBind.

## Comparação

| Critério | WPF em .NET 10 | WinUI 3 | Tauri | Electron | Avalonia |
|---|---|---|---|---|---|
| Memória de um utilitário residente | Baixa para um app desktop | Baixa a média | Baixa na UI, mais um processo nativo | Alta | Média |
| Hooks, Raw Input e SendInput | No mesmo processo e no laço Win32 | Possível, com mais atrito de empacotamento | Exige backend Rust ou sidecar e IPC | Módulo nativo e processo separado | P/Invoke no backend Win32, indireto |
| Bandeja e início com o Windows | Maduros | Possíveis, mais código de empacotamento | Suportados | Suportados | Possíveis |
| Interface escura própria | Estilo XAML direto | Fluent pronto | Web | Web | XAML próprio |
| Teste do motor sem UI | Biblioteca de classe | Igual | Núcleo separado do webview | Igual, com mais cola | Igual |
| Distribuição inicial | Pasta publicada ou instalador depois | MSIX ou unpackaged ainda trabalhoso | Instalador próprio | Instalador pesado | Pasta publicada |

## Recomendação

C# com .NET 10 e WPF para a interface. O motor de bindings, macros, perfis e persistência fica numa biblioteca sem WPF.

Motivos:

- O computador já tem `Microsoft.WindowsDesktop.App` 10.0.13. O alvo `net10.0-windows` executa nesse runtime.
- Hook de baixo nível e Raw Input precisam do laço de mensagens Win32. WPF já o possui.
- A bandeja, o atalho de emergência e o envio com `SendInput` podem viver no processo da janela sem um segundo runtime.
- O motor fica testável com xUnit, sem abrir janela.
- O consumo fica abaixo de um shell Electron, alinhado ao pedido de um utilitário leve.
- A interface escura é desenhada no XAML do NinoBind. Não herda o tema Fluent do NGENUITY.

WinUI 3 perde nesta fase pelo empacotamento e pela bandeja. Tauri e Electron afastam o evento de teclado do código que decide o binding. Avalonia funcionaria, e é o que o NGENUITY usa, mas o NinoBind é só Windows e depende de APIs que o WPF alcança com menos camada.

## Módulos

| Projeto | Responsabilidade |
|---|---|
| `NinoBind.Core` | Documentos, perfis, bindings, macros, validação, persistência JSON, retrato da visão geral |
| `NinoBind.Input` | Captura, Raw Input, hooks, SendInput, bloqueio de eco. Entra na fase 3 |
| `NinoBind.App` | Janela, navegação, tema, bandeja e composição. Sem regra de remapeamento |
| `NinoBind.Core.Tests` | Testes do núcleo |

Fluxo previsto do motor, ainda não implementado:

```
Entrada do Windows
  -> Input Manager
  -> perfil e dispositivo
  -> Binding Engine
  -> Action Dispatcher
  -> execução
```

Eventos sintetizados pelo próprio processo não voltam para o motor. Key up e mouse up emitem a liberação correspondente. O Emergency Stop, nas fases seguintes, cancela macros e solta as entradas sintetizadas mesmo com a janela sem foco.

## Persistência

Arquivo versionado em `%LocalAppData%\NinoBind\config.json`. A versão de esquema começa em 1. Carga inválida não regrava um arquivo bom. Importação com falha, quando existir, preserva o documento atual.

O perfil inicial gravado se chama Default.

## Limites conhecidos antes de codificar o motor

- Um hook global não identifica com segurança o teclado ou o mouse físico de cada evento.
- Raw Input melhora a identificação em muitos dispositivos HID, e ainda assim falha quando o Windows agrega o dispositivo ou quando o evento chega só pelo hook.
- `SendInput` não entra em processos que rejeitam entrada sintetizada. O NinoBind não contorna essa rejeição.
- Privilégio de administrador não é requisito da função básica. Se um programa elevado não receber o hook, a interface dirá isso. O processo não se eleva sozinho.

## Ambiente desta máquina

Há runtimes .NET, inclusive Windows Desktop 10.0.13. No início da fase 1 não havia SDK em `C:\Program Files\dotnet\sdk`. O SDK 10.0.402 foi instalado para compilar a solução. O Visual Studio Community 18 também está presente.
