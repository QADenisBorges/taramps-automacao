# O que o Agente precisa do app

A criação pela porta (CAP-18 do app, histórias 9d e 10d) já existe. Esta spec acrescenta três coisas do lado do app, com as mesmas travas (`porta-de-comandos.md`), sem a palavra "IA" na tela e com o visual do UX de 30/10. O UX não desenhou nenhuma delas (questão 17 do UX de 30/10): o que está aqui é [ASSUMPTION] até a conferência no aparelho.

## Pedido de criação

- O toque em "+ Nova memória" vira um evento na porta. O app mostra o anel duplo e espera o Agente abrir a criação.
- Sem resposta em 15 s: "Não deu para começar agora. Confira se o computador está ligado e no mesmo Wi-Fi." e volta à Roda.
- Com a chave "Controle pela rede" desligada, o "+ Nova memória" mostra "Ligue o Controle pela rede em Aparelho." e leva a Aparelho.

## Escuta "1 ou 2"

- Um tipo novo de pergunta, só no refino: a variável, a versão 1 e a versão 2 (cada uma, uma lista de ajustes dentro da cerca).
- A tela mostra dois botões grandes, "1" e "2". Tocar troca o som ao vivo, com cópia antes e releitura. O app iguala o volume das duas versões pelo master. Embaixo: "Prefiro o 1", "Prefiro o 2" e "Tanto faz".
- A escolha volta pela porta. O som fica na versão escolhida, e "Tanto faz" volta à 1.
- O ✕ volta à cópia de antes da criação, como nas outras perguntas.

## Proposta de ficha

- Ao salvar um equipamento cujo fabricante e modelo não estão na base, o app manda o pedido de ficha pela porta.
- O Agente devolve a proposta: cada dado com a origem e o endereço, e o que não fechou marcado.
- Uma folha do app mostra os dados para confirmar, um a um ou todos. Só o toque do usuário salva, na ficha e na base de modelos, com a origem "oficial" ou "outra fonte". O que ele recusa fica em branco.
- Sem resposta em 60 s, a ficha fica como está, sem aviso de erro.
