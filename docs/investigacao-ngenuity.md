# Investigação do HyperX NGENUITY

Referência de experiência para o NinoBind. O aplicativo original não foi modificado nem executado. A inspeção foi estática, com a CLI `rea-agents` 6.5.0, porque o servidor MCP do REA não está registrado neste Cursor.

Instalação: `C:\Users\Adriano\AppData\Local\NGENUITY`, versão 3.1.0, pacote ativo em `current`.

## 1. Tecnologias e confiança

| Afirmação | Confiança | Base |
|---|---|---|
| Interface Avalonia, não Electron e não HTML | Alta | Referências de assembly e recurso `!AvaloniaResources` |
| .NET 10, CIL, x64, IL-only | Alta | REA `inspect-managed-artifact` |
| Tema Fluent e Skia | Alta | Referências `Avalonia.Themes.Fluent`, SkiaSharp |
| SVG via `Svg.Controls.Avalonia` | Alta | Referência e 155 SVG no índice de recursos |
| MVVM com CommunityToolkit.Mvvm, DryIoc e Splat | Alta | Referências de assembly e pares View/ViewModel |
| Empacotamento Velopack | Alta | `Update.exe`, `packages\NGENUITY-3.1.0-full.nupkg`, referência `Velopack` |
| Medidas de espaçamento, duração de animação e árvore visual completa | Não estabelecido | O XAML-fonte não está no binário e o processo não foi observado |

Evidência do assembly principal `NGENUITY.dll`: `ev_e2bc121bba08e1e86ead0051133131b552752f933809d041c8918eaa2823b61b`, SHA-256 `03515f57445c3c1ebb20c793c3a69fad72e455a54fff842356fede5e49e4a4d9`. Classificação REA: managed, modern-dotnet, cil. Alvo `.NETCoreApp,Version=v10.0`.

## 2. Estrutura geral

| Papel | Caminho |
|---|---|
| Inicializador | `C:\Users\Adriano\AppData\Local\NGENUITY\NGENUITY.exe` |
| Host da versão atual | `...\current\NGENUITY.exe` |
| Aplicação e telas | `...\current\NGENUITY.dll` |
| Controles reutilizáveis | `...\current\NGENUITY.Controls.dll` 1.2.4.0 |
| Imagens de produto | `...\current\NGENUITY.Assets.dll` |
| Plataforma | `NGENUITY.Common.dll`, `NGENUITY.Platform.Common.dll`, `NGENUITY.Platform.Windows.dll` |
| Idiomas | `current\<cultura>\NGENUITY.resources.dll` |

`Assets\Services` e `Assets\Drivers` são áudio, iluminação e firmware. Ficam fora do que o NinoBind precisa.

Não há arquivos `.html`, `.css` ou `.js` na árvore instalada. Os trechos no formato CSS são estilos inline de SVG dentro do XAML compilado.

## 3. Telas relevantes

O mapa `/!AvaloniaResourceXamlInfo`, dentro de `!AvaloniaResources`, liga 132 classes a caminhos lógicos. O markup original não está embutido. Cada tela vira CIL em `!XamlIlPopulate`.

| Área | Caminho lógico | Classe |
|---|---|---|
| Aplicação | `/App.axaml` | `HXN.App` |
| Janela | `/Views/MainWindow.axaml` | `HXN.Views.MainWindow` |
| Shell | `/Views/NavigationShellView.axaml` | `HXN.Views.NavigationShellView` |
| Início | `/Views/General/HomeView.axaml` | `HXN.Views.General.HomeView` |
| Dispositivo | `/Views/DeviceContentView.axaml` | `HXN.Views.DeviceContentView` |
| Ajustes | `/Views/General/SettingsView.axaml` | `HXN.Views.General.SettingsView` |

As outras telas cobrem áudio, luz, teclas, calibração, efeitos e produtos específicos. `NGENUITY.Controls.dll` (evidência `ev_dd87fdba5bcbe67bfc1ffc274da65d9f9c0f06b0458cbf9bb613fe60c5dca547`) acrescenta 80 controles em pastas como `/Buttons`, `/Sliders`, `/Dialogs`, `/Colors` e `/Status`.

`HXN.ViewLocator.Build` troca o sufixo `ViewModel` por `View`. A literal de falha é `Not Found: `.

## 4. Componentes visuais observados

Observados por literais `ldstr` de `!XamlIlPopulate` e por nomes de métodos. A leitura das tabelas encontrou os mesmos 1.636 tipos e 14.196 métodos contados pelo REA. O REA não expandiu os corpos: evidência `ev_6bf4ecfffddd8403de5bdf60c4b3be9354200808c3a6239564e79f5e5c2844db`, limite de representação de 64 MB.

Janela `MainWindow`:

- Título `HyperX NGENUITY` e ícone `/Assets/ngenuity_icon.ico`.
- Conversores `WallPaperInfoConverter`, `BoolToBlurConverter` e `WindowStateToIconConverter`.
- Botões de legenda com `icon-caption-minimize.svg` e `icon-caption-close.svg`.
- CSS de ícone com opacidade 0,36 em repouso e 0,9 no hover, em variantes clara e escura.
- View model com `ContentViewModel`, `OverlayViewModel` e comandos `Minimize`, `Maximize` e `Close`.

Shell `NavigationShellView`:

- Partes `Root` e `VerticalRoot`.
- Logo `avares://NGENUITY.Assets/Logos/logo-setting-hyperx-gray.png`.
- Ícones `icon-general-settings.svg` e `icon-device-config-audios.svg`.
- Estado visual `locked`.
- Navegação montada em código: Home, dispositivo virtual, microfone, headset e coleções de dispositivos, apps, geral, perfis e Instant Pair.

Início `HomeView`:

- `HomeRootGrid`, lista `PART_HomeCardItemsControl`, cartão `HomeCardRoot`.
- Estados `preview`, `preview_large`, `details` e `details_large`.
- Bateria `critical`, `low`, `medium` e `unknown`.
- Fontes citadas: `RBNo31` e `Segoe UI`.

Recursos gráficos em `NGENUITY.dll`: 155 SVG, 2 PNG, 1 ICO e arquivos de idioma `.resx`. O ícone do executável está no recurso PE de `current\NGENUITY.exe` (evidência `ev_69ad6de63a50160707a7383993b0a13484a1c4a5d45dd70c83a6349ab0380389`), de 16 a 256 px.

`NGENUITY.Assets.dll` tem 260 MB e foi recusado pelo REA, cujo snapshot máximo é 128 MB. Uma leitura só do índice, no mesmo formato de versão 2 verificado nas outras DLLs, lista 307 PNG de produtos, papéis de parede e logos. O primeiro item começa com a assinatura PNG `89504e47`. Esses arquivos não serão reutilizados.

## 5. Padrões de interação

- A janela só desenha a moldura. O miolo é uma shell com navegação lateral persistente.
- `OnActivated` da shell cita `HomeViewModel`: a abertura cai numa visão geral, não num editor.
- A troca de página usa view models (`ContentViewModel` e `OverlayViewModel`) e um localizador View/ViewModel.
- Existem mensagens explícitas de navegação: conteúdo principal, overlay, cabeçalho, status e escurecimento da shell.
- Cartões representam dispositivos. Selecionar um item troca o conteúdo (`OnSelectedItemChanged`).
- Estados de controle aparecem como recursos nomeados: repouso, pointerover, pressed e disabled.
- Ícones SVG mudam o traço ou o preenchimento conforme o estado, em vez de trocar a imagem inteira.
- Textos de interface vêm de chaves de recurso (`DevicesTitle`, `NoDevicesDetectedTitle`), com satélites por cultura.

Não foi medida a duração de transições. O tema Fluent está referenciado. Isso não quantifica espaçamento nem curva de animação.

## 6. Limitações

- O MCP do REA não está registrado. A CLI fez a inspeção estática.
- `inspect-managed-members` não admitiu `NGENUITY.dll` por orçamento de 64 MB.
- `NGENUITY.Assets.dll` não coube no snapshot de 128 MB.
- O XAML original não está no pacote. A árvore visual além dos nomes e URIs permanece desconhecida.
- O programa não foi executado. Hover, foco, animação e fluxo real de clique não foram observados.
- O assinante de cada `Navigate*Message` não apareceu como método com nome próprio.

## 7. Ideias de UX aplicáveis ao NinoBind

São decisões de produto inspiradas na organização observada, com identidade e implementação próprias.

- Abrir numa visão geral do estado real: perfil ativo, bindings, macros e motor.
- Manter uma navegação lateral curta e estável.
- Separar a moldura da janela do conteúdo.
- Representar dispositivo, entrada e ação na mesma linha, sem entrar num editor por fabricante.
- Dar estado visual imediato a item selecionado, hover, desativado e erro.
- Usar ícones simples de um único traço, com a cor mudando no estado.
- Mostrar vazio de forma direta quando não houver dispositivos, bindings ou macros.
- Tratar overlay como camada rara, para captura e gravação, não como navegação habitual.

## 8. O que simplificar

O NGENUITY organiza dezenas de telas por produto, áudio, luz, firmware e calibração. O NinoBind fica em seis seções: Visão geral, Dispositivos, Bindings, Macros, Perfis e Configurações.

Ficam de fora: efeitos de luz, equalizador, papéis de parede, instalação de driver, atualização de firmware e um controle visual por modelo. Um binding é uma linha. Uma macro é uma sequência editável. Um perfil é um conjunto dessas duas coisas.
