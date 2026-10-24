# Uso diário (fase 7)

O ícone da bandeja fica visível enquanto o programa roda. O menu tem **Abrir NinoBind**, **Ativar/Desativar motor**, **Parar macros** e **Sair**. O mesmo botão fica no rodapé do menu lateral. Depois de cada troca, o botão e o Scroll Lock ficam bloqueados por 2 segundos; Ctrl + Alt + Pause continua parando o motor na hora. O ícone é desenhado em tempo de execução com o logotipo próprio do NinoBind.

Três opções em **Configurações**, todas desligadas por padrão:

- **Iniciar com o Windows** grava o valor `NinoBind` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` só quando marcada, com o comando `"<exe>" --tray`. Desmarcar apaga apenas esse valor. Não usa tarefa agendada, serviço nem privilégio de administrador. Se o valor apontar para outro executável, a tela mostra o caminho em vez de sobrescrever sozinha.
- **Fechar para a bandeja** esconde a janela ao fechar. **Sair** no menu do ícone encerra de verdade.
- **Ativar o motor ao abrir** liga o motor logo após criar a janela.

Com `--tray`, a janela é criada sem aparecer, para que os hooks e o Emergency Stop funcionem. Só roda uma instância por sessão; abrir de novo traz a janela existente para a frente.

`Ctrl + 1` a `Ctrl + 6` trocam de seção. Listas, filtros e combos têm nome acessível, e o status do rodapé é anunciado por leitores de tela. Erros não tratados mostram a mensagem, registram tipo e texto do erro no log (nunca teclas) e fecham o programa, soltando antes as entradas sintetizadas.

Não verificado: leitura real com Narrador ou NVDA, alto contraste e escala de 150% ou mais.
