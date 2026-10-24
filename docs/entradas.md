# Entradas

## Fase 3

A lista de teclados e mouses vem de `GetRawInputDeviceList`. O nome e o fabricante vêm de `HidD_GetProductString` e `HidD_GetManufacturerString` quando o dispositivo abre. Se a abertura falha, a interface mostra o VID e o PID lidos do caminho, sem traduzir isso para uma marca.

A observação só começa no botão **Observar entradas**. Os eventos mostrados na tela não são escritos no log.

O aparelho de cada evento é o handle do Raw Input. O hook de baixo nível não separa o hardware. Ele só reconhece a tecla de teste F13, marcada com `dwExtraInfo` `0x4E424249`, para não tratá-la como entrada física.

`SendInput` não entra em programas que rejeitam entrada sintetizada. Esta fase não contorna isso e não exige administrador.

## Fase 4

O motor aplica somente bindings do perfil ativo com dispositivo vazio (**Qualquer dispositivo**). Um binding preso a um aparelho fica salvo com status **Não aplicado**, porque o hook não identifica o hardware.

Se um binding global e um específico cobrem a mesma entrada no mesmo perfil, os dois ficam em **Conflito** e a tecla original segue para o aplicativo em foco. O mesmo vale para duas ações globais na mesma entrada.

Ação **Nenhuma** não engole a tecla. **Desativar entrada** engole a tecla e não envia substituto.

Ao pausar o motor ou fechar a janela, as teclas e os botões sintetizados que ainda estavam pressionados são soltos. A captura do editor engole só a próxima tecla ou botão físico, para preencher o formulário.

A observação e o motor compartilham os mesmos hooks. Desligar uma das duas não remove o hook enquanto a outra ainda precisa dele. O log registra apenas `engine on`, `engine off` e a quantidade de bindings.

## Fase 5

Uma macro tem passos de tecla (pressionar ou soltar), botão do mouse, atraso e texto. Limites: 500 passos, atraso de 0 a 60000 ms, 1 a 100 repetições e, no modo **Repetir enquanto segurar**, no máximo 1000 voltas. Os campos novos são aditivos; o esquema continua na versão 1.

A gravação só começa no botão **Gravar** e mostra uma faixa vermelha enquanto dura. Ela usa os eventos de teclado da janela do editor e os cliques na área marcada. Teclas digitadas em outros programas não entram na macro, e nada da gravação vai para o log.

Ao terminar, ser cancelada ou parada, a macro solta toda tecla e botão que deixou pressionado. Um binding de macro engole a tecla de origem; se a macro estiver vazia, o status fica **Macro vazia** e a tecla segue normalmente. Uma origem roda uma macro por vez, com no máximo 8 macros simultâneas.

**Emergency Stop:** `Ctrl + Alt + Pause`, registrado com `RegisterHotKey`, funciona com outra janela em foco. Ele cancela as macros, solta as entradas sintetizadas e pausa o motor. A tecla Pause não pode ser usada como entrada de binding. Se outro programa já tiver esse atalho, o rodapé avisa que ele está indisponível. Se Ctrl ou Alt estiverem remapeados para outra ação, o atalho pode não chegar ao Windows; o botão **Parar todas** continua disponível.

**Motor:** `Scroll Lock`, também com `RegisterHotKey`, liga e desliga o motor com outra janela em foco. A tecla não pode ser usada como entrada de binding. Depois de cada troca, o atalho e o botão do menu lateral ficam bloqueados por 2 segundos.
