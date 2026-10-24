# Plano de implementação

Cada fase termina com o programa compilando. Uma função só conta como pronta quando o comportamento existe e foi exercitado.

## Fase 1 — Investigação e planejamento

- Documentar o NGENUITY com as evidências já coletadas pelo REA.
- Registrar a escolha WPF em .NET 10.
- Este plano.

## Fase 2 — Fundação

- Solução `NinoBind.sln`.
- Biblioteca `NinoBind.Core` com documento versão 1, perfil Default e leitura JSON que não sobrescreve arquivo inválido.
- Aplicativo WPF com navegação das seis seções, tema escuro próprio e visão geral lendo o documento real.
- Log em `%LocalAppData%\NinoBind\logs`.
- Testes do retrato da visão geral e da persistência.

As seções Dispositivos, Bindings, Macros, Perfis e Configurações aparecem na navegação e dizem o que ainda não fazem. Não há botão que simule remapeamento.

## Fase 3 — Dispositivos e entradas

- Enumerar teclados e mouses por Raw Input e HID.
- Mostrar nome, fabricante e identificador só quando a API os fornecer.
- Capturar key down, key up, mouse down e mouse up.
- Enviar um evento de teste com `SendInput` e impedir o eco.
- Registrar na interface quando a origem física não puder ser separada.

## Fase 4 — Bindings

- Criar, editar, duplicar, excluir, ativar e desativar.
- Tipos: tecla, combinação, botão do mouse, macro, texto, desativar entrada e nenhuma ação.
- Lista Dispositivo, Entrada, Ação, Perfil, Status.
- Conflito no mesmo escopo e perfil.

## Fase 5 — Macros

- Modelo de passos, editor, reordenação, atrasos e repetições com limite.
- Gravação explícita, com indicador visível.
- Cancelamento e Emergency Stop fora da janela em foco.
- Testes de sequência, repetição e cancelamento.

## Fase 6 — Perfis

- CRUD, duplicar, copiar bindings, importar e exportar.
- Troca do perfil ativo com soltura das teclas sintetizadas.
- Migração do esquema quando a versão mudar.

## Fase 7 — Uso diário

- Bandeja, pausa do motor, início com o Windows só com consentimento.
- Atalho de Emergency Stop.
- Mensagens de erro e navegação por teclado.

## Fase 8 — Distribuição

- Publicação, instalador, ícone, instalação e desinstalação testadas.
- Roteiro manual em Windows 10 e 11.
- O suporte universal só será declarado para o que esse roteiro cobrir.

## Situação

As fases 1 a 8 têm código, testes automáticos e verificação local. O que falta é executar o roteiro manual de `docs/distribuicao.md` em Windows 10 e 11, inclusive com leitor de tela, e assinar o executável antes de uma distribuição pública.
