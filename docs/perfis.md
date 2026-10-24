# Perfis (fase 6)

Os nomes de perfil são únicos, sem diferenciar maiúsculas, com até 60 caracteres. O perfil ativo e o último perfil não podem ser excluídos. Excluir um perfil remove os bindings dele; as macros continuam salvas.

Duplicar copia os bindings com novos identificadores e mantém as mesmas macros.

Trocar o perfil ativo para as macros em execução e solta as teclas e botões sintetizados antes de aplicar o novo perfil.

Cada alteração é validada e gravada. Se a validação ou a gravação falha, o documento em memória volta ao estado anterior. Um `config.json` inválido continua sem ser sobrescrito.

## Exportar e importar

A exportação gera `*.ninobind.json` com `format` `ninobind-profile`, a versão do esquema, o perfil, os bindings e as macros que eles usam. O arquivo inclui os textos dos bindings, então não deve ser compartilhado se tiver algo pessoal.

A importação aceita até 1 MB e exige `format` e `schemaVersion` explícitos. Perfil, bindings e macros recebem novos identificadores; nome repetido ganha um número. O perfil importado não é ativado. Se qualquer item for inválido, nada é importado. O arquivo não executa nada ao ser lido.

## Migração de esquema

O esquema continua na versão 1: os campos das fases 4 a 6 são aditivos e têm valor padrão. Arquivos com versão diferente são recusados e preservados. A migração automática fica para quando existir uma versão 2.
