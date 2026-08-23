# MonoCraft

Este é um clone básico do Minecraft desenvolvido utilizando o framework MonoGame.

## Como rodar o jogo

Para compilar e iniciar o projeto no desktop, execute o seguinte comando no terminal:

```bash
dotnet run
```

Para compilar e lançar diretamente no emulador Android conectado, utilize o comando:

```bash
dotnet build -t:Run -f net10.0-android MonoCraft.Android/MonoCraft.Android.csproj
```

Para compilar a versão para Android e gerar o APK (salvo na pasta `releases/android/`), utilize o script de build:

```bash
./scripts/build-android.sh
```

Você também pode adicionar a flag `--debug` para compilar em modo de depuração ou `--sign` para assinar o APK com a keystore do projeto:

```bash
./scripts/build-android.sh --sign
```

## Controles e Comandos

### Movimentação e Câmera

- **W, A, S, D**: Movimentar para frente, esquerda, trás e direita.
- **Espaço**: Pular (modo normal) ou Subir (modo de voo).
- **Shift Esquerdo**: Descer (apenas no modo de voo).
- **Control Esquerdo (Ctrl)**: Correr (Sprint).
- **F**: Alternar modo de voo (Ativa/Desativa).
- **Mouse**: Controla a câmera (olhar ao redor).

### Interação com o Mundo

- **Clique Esquerdo do Mouse**: Quebrar bloco.
- **Clique Direito do Mouse**: Colocar bloco selecionado.
- **E**: Abre ou fecha o Menu de Inventário (Sobrevivência).
  - No menu de inventário, os blocos exibidos são apenas aqueles que você já coletou e possui em estoque.
  - Clique com o botão esquerdo para arrastar e soltar os blocos na sua barra de atalhos.
- **Teclas de 1 a 9**: Selecionar os _slots_ da barra de atalhos (hotbar).

### Controles de Gamepad / Mobile Touchpad

Se você estiver jogando no Android ou com um controle no PC:

- **Analógico Esquerdo / Direito**: Mover e olhar ao redor.
- **Clique do Analógico Direito (R3)**: Alternar modo de voo (Ativa/Desativa).
- **A**: Pular (ou subir no modo de voo).
- **Gatilho Esquerdo (LT)**: Colocar bloco (Place).
- **Gatilho Direito (RT)**: Quebrar bloco (Break).
- **Y**: Abre ou fecha o Menu de Inventário (INV).
- **Bumpers (LB/RB) ou D-Pad (Esquerda/Direita)**: Navegar entre os itens da barra de atalhos.

### Janela e Sistema

- **F2**: Tira uma captura de tela (salva como um arquivo .png na pasta principal do jogo).
- **Tab**: Alterna a captura do mouse (libera ou prende a mira na janela).
- **Esc (Escape)**: Abre o menu de confirmação de saída (QUIT? Y/N), ou fecha menus abertos.
- **Y / N**: Confirma (Y) ou cancela (N) a saída no menu de confirmação.
- **Botão Verde da Janela (macOS)**: Alterna o modo tela cheia de forma nativa.

## Licença

Este projeto está licenciado sob a [Licença MIT](LICENSE) - veja o arquivo para mais detalhes.
