# Reconcile — referência da esfera de neon

Fonte: [imports/referencia-esfera-neon.png](imports/referencia-esfera-neon.png), enviada pelo Denis em 30/10/2026. Uso: a passagem do anel do chão até o Aplicando, na criação da memória.

## O que a referência propunha

- Uma esfera feita de vários anéis de neon, em tubo de **2 linhas**.
- Anéis **azuis em pé**, girando num sentido.
- Anéis **rosa deitados ou inclinados**, girando no sentido contrário.
- **Setas** curvas mostrando o sentido de cada giro (azul de um lado, rosa do outro).
- Neon forte, com muito brilho.

## O que foi adotado

Opção 1 da esfera de 1 linha, **Pião e cambalhota** *(Denis, 30/10)*. Detalhes em `DESIGN.md › Movimento` e no [mock](mockups/criacao-memoria-ia.html).

- **1 linha**, não tubo: traço 2,2, com brilho leve (drop-shadow de 2 e 6 px).
- **Um anel de cada cor**: um azul (`{colors.brand}`) e um rosa (`{colors.accent-pink}`).
- **Giro em sentidos contrários**: o azul em pé, no eixo vertical; o rosa deitado a 64°, no eixo horizontal.
- **Uma volta**, com curva suave (seno), em 1,6 s. Abre do anel do chão e fecha no plano, no spinner do Aplicando.

## O que ficou de fora

- **Tubo de 2 linhas** (opções 2A a 2E).
- **Mais anéis**: 2+2 e 3+3 (opções 1B, 1C, 2B, 2C).
- **Neon forte** (opções 1D e 2D).
- **Setas de giro** (opção 2E). O sentido contrário aparece só no movimento.
- **Duas voltas** rápidas (pico de cerca de 1.320° por segundo). Trocadas por uma volta suave (cerca de 350° por segundo).
- As outras 9 animações da base de 1 linha (Giroscópio, Contratempo, Diagonais, Moeda bamba, Respiração, Profundidade, Espiral, Ritmos diferentes, Órbita).

Histórico das opções: [.working/memoria-esfera-neon-opcoes-1.html](.working/memoria-esfera-neon-opcoes-1.html) e [.working/memoria-esfera-neon-opcoes-2.html](.working/memoria-esfera-neon-opcoes-2.html).
