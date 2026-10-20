# Sessão completa — Forja de ideias: controle do Taramps pelo PC

**Data:** 14/10/2026 · **Participante:** Denis · **Skill:** `bmad-forge-idea` (objetivo: esclarecer) · **Resultado:** ideia endurecida, descoberta iniciada

Arquivos desta sessão:
- `forged-idea.md`: decisões em formato curto, entrada para `bmad-spec` / `bmad-build`
- `forge-report.html`: relatório visual com o selo
- `.memlog.md`: registro bruto, item por item
- `prints/`: telas do celular enviadas na conversa
- `../../../descoberta/`: scripts e passo a passo da descoberta do protocolo

---

## 1. Ponto de partida

**Ideia do Denis:** o projeto de som (home-theater 2.1) usa o processador **Taramps PRO 2.4BT + AUDIO**. Hoje ele é configurado pelo app, passo a passo, e qualquer ajuste ou pedido novo obriga a refazer tudo à mão.

**Roteiro imaginado:**
1. **Protótipo 1:** automatizar a integração com o processador.
2. **Protótipo 2:** processamento de áudio no computador, com a IA captando o som e equalizando.
3. **Sonho distante:** um software vendável (para ou da Taramps). Os protótipos já bastam.

**Fatos conferidos no repositório Sound** (`componentes/processadores.md`, `projetos/home-theater-2.1/montagem-2.1.md`):
- O processador não tem display nem botões: tudo passa pelo app, por BLE 5.4.
- Tem 3 memórias no aparelho e 15 no celular, exportáveis por WhatsApp.
- Cadeia de som: notebook/celular → processador → SD800.4 → JBL 4TRMS80 (L/R) + sub Protech 8" em ponte.

## 2. Perguntas e respostas

### 2.1 O que "automatizar" quer dizer?
Três leituras possíveis:

| Opção | Como funciona | Situação |
|---|---|---|
| **A** | Gerar o arquivo de preset e importar no app | rejeitada |
| **B** | Um robô toca na tela do app | rejeitada |
| **C** | O PC fala direto com o processador | **escolhida** |

**Resposta do Denis:** *o PC controlando ao vivo.* A IA é **assistente**, não piloto automático.

Visão das **3 ferramentas**:
1. Consultor-som / repositório Sound: decide os valores e guarda a configuração, a metodologia de verificação e a segurança.
2. Acesso ao processador (controle).
3. Áudio: medição e processamento no PC.

O software genérico e seguro, que funciona para qualquer projeto, fica para o futuro. Agora, só o protótipo.

**Achado (busca na web):** não existe documentação pública do protocolo BLE da Taramps nem projeto de engenharia reversa conhecido. O site oficial diz que o compartilhamento de presets é criptografado. A viabilidade do protótipo depende de descobrir esse protocolo.

### 2.2 Android ou iPhone?
**Android (Samsung).** Isso permite gravar o Bluetooth pelo próprio sistema (registro HCI) e ler o código do app, sem comprar sniffer.

**Decisão:** o protótipo 1 tem duas etapas:
1. **Prova de viabilidade:** o PC muda um parâmetro.
2. **Ferramenta:** controle completo, com travas de segurança.

**Achado:** esta sessão roda na nuvem, que não tem Bluetooth. O controle precisa rodar no **notebook do Denis**.

### 2.3 O que é "deu certo"?
**Resposta: nível 2.**
- O PC **lê** a configuração atual. A leitura é obrigatória.
- O PC **muda** crossover, nível por saída, atraso, fase, EQ paramétrico e EQ gráfico, e troca a memória.
- **Fora:** limitador, gerador, senha, voltímetro e Audio BT.

### 2.4 Até onde a IA age sozinha?
Regras de segurança tiradas do `montagem-2.1.md`:
- O passa-alta das saídas 1/2 (BW 12 dB em 120 Hz) nunca pode sumir, senão a JBL recebe grave cheio.
- As saídas 3 e 4 ficam sempre idênticas, por causa da ponte.
- Depois do ganho calibrado, níveis e EQ só para baixo.
- Uma escrita interrompida deixa o aparelho em estado desconhecido.

**Resposta:** "Gostei da ideia, vamos fazer a descoberta e o que surgir de ideias que agreguem a gente vai aplicando conforme a segurança." Ficou assim:
- **Na descoberta:** toda escrita no processador passa pela aprovação do Denis.
- **Na ferramenta pronta:** a IA só baixa nível e EQ sozinha, dentro de uma faixa definida. Crossover, fase, atraso, troca de memória e qualquer subida precisam de OK.
- **Sempre valem, no código e não no prompt:** as travas acima, uma cópia da configuração antes de cada mudança e o "desfazer".
- **Regra da descoberta:** só reenviar comandos vistos saindo do app, com volume baixo. Nunca enviar bytes desconhecidos.

### 2.5 Personas que testaram a ideia
| Persona | Papel |
|---|---|
| 📋 John | PM |
| 🏗️ Winston | arquiteto |
| 💻 Amelia | dev |
| 🔒 Vex | segurança |
| 🔧 Rogério | instalador de som, voz externa |
| 🔬 Tiago | engenharia reversa de BLE, voz externa |
| 🧰 Marcos | técnico de bancada, voz externa |
| 🎚️ Bruna | calibração de salas, voz externa |

## 3. Depois da forja: começo da descoberta

1. **Opções do desenvolvedor:** o Denis abriu por engano as configurações de desenvolvedor do **TalkBack** (`prints/01-...`). As do Android aparecem depois de 7 toques em *Configurações → Sobre o telefone → Informações do software → Número de compilação*.
2. **Mais automação no notebook:** o Denis pediu para automatizar mais pelo notebook. O notebook **não enxerga** a conversa entre o celular e o processador, mas consegue fazer três coisas:
   - ler o código do app (APK, com jadx);
   - mapear o processador por BLE, só leitura (bleak);
   - pilotar o celular por USB (adb).

   Prioridade: **APK + mapa GATT**. A captura HCI fica como conferência.
3. **App real:** **"Pro 2.4BT+Audio"** (`prints/02-...`). Não é o "Taramps PRO" (`com.taramps.pro1`) que aparece na loja. A rede da nuvem bloqueia lojas de apps, então o APK sai do celular.
4. **Notebook:** Windows, com Bluetooth.
5. **Scripts criados em `descoberta/`** (commit `92a8fd3`):

   | Script | O que faz |
   |---|---|
   | `extrair_apk.ps1` | copia o APK do celular por adb |
   | `mapear_gatt.py` | lista serviços e características BLE e lê o que for legível; `--ouvir N` assina notificações |
   | `puxar_log_bt.ps1` | puxa o `btsnoop_hci.log` e apaga o relatório de bugs, que tem dados pessoais |
   | `README.md` | passo a passo no Windows |

   `apk/` e `capturas/` ficam fora do Git. O script Python foi testado com um aparelho simulado. Os de PowerShell não foram testados (não há PowerShell na nuvem).

## 4. Próximos passos (no notebook)
1. Clonar o repositório e trocar para o branch `ccr-8957b24e-mdvz7x`.
2. Seguir `descoberta/README.md`: instalar Python e adb, ligar a depuração USB, extrair o APK e rodar `py mapear_gatt.py` com o app fechado no celular.
3. Abrir o Claude Code na pasta `taramps-automacao` e colar:

   > Estou no notebook seguindo `descoberta/README.md` para descobrir o protocolo BLE do Taramps PRO 2.4BT + AUDIO. O contexto e as regras de segurança estão em `_bmad-output/forge/automacao-app-taramps-dsp/forged-idea.md` e o histórico em `sessao-completa.md`. Me guie passo a passo a partir da etapa 1 e rode os scripts comigo. Nada de escrever no processador sem a minha aprovação.

4. **Marco da etapa 1:** o PC muda o volume master. **Marco do protótipo 1:** nível 2 (seção 2.3).

## 5. Riscos em aberto
- O protocolo pode ser criptografado ou atrelado à senha de 4 dígitos (o padrão é 1234 e deve ser trocado).
- Provavelmente só uma conexão de controle por vez: o PC e o app se revezam.
- Escrita interrompida: a ferramenta precisa reler o estado e restaurar a cópia.

## Fontes
- [Taramps – PRO 2.4 BT](https://www.taramps.com.br/en/produto/pro-2-4s-bt/)
- [Manual PRO 2.4BT (ESP)](https://www.taramps.com.br/wp-content/uploads/2025/06/MN_PRO-2.4BT_ESP.pdf)
- [Reverse Engineering BLE Devices](https://reverse-engineering-ble-devices.readthedocs.io/en/latest/protocol_reveng/00_protocol_reveng.html)
- [Similarweb – com.taramps.pro1](https://www.similarweb.com/app/google/com.taramps.pro1/)
- [App Store – pro24bt-audio](https://apps.apple.com/app/id6768624444)
