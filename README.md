# PC Tuner

Programa para Windows que deixa o PC e a internet mais rápidos com ajustes que **dá para ligar e desligar**. Cada botão mostra o estado real do sistema, e toda mudança fica registrada num histórico.

Feito em C# com .NET 8 (WPF).

## O que ele faz

| Tela | O que tem |
|---|---|
| **Visão geral** | Processador, RAM, placa de vídeo, discos e bateria. Avisa quando o disco está quase cheio. |
| **Otimizações** | Ajustes com liga/desliga, cada um com o impacto esperado e os efeitos colaterais. |
| **Rede** | Teste de ping, velocidade e **bufferbloat** (o quanto o ping sobe quando a conexão está ocupada), com nota de A+ a F. |
| **Inicialização** | Liga e desliga programas que abrem com o Windows, do mesmo jeito que o Gerenciador de Tarefas. |
| **Limpeza** | Apaga cache e arquivos temporários (sempre pede confirmação). |
| **Espaço em disco** | Mede as pastas que mais crescem (Docker, emuladores, Ollama, Spotify...) e explica como liberar espaço. Não apaga nada. |

### Otimizações disponíveis

| Ajuste | Impacto |
|---|---|
| Wi-Fi ajustado para jogos (roaming mínimo, 5 GHz, sem economia de energia nas antenas) | Alto |
| Exclusões no Defender para pastas de desenvolvimento Android (builds mais rápidos) | Alto |
| Plano de energia Alto desempenho | Médio |
| Desligar hibernação (libera vários GB no C:) | Espaço |
| Desligar o gerenciamento de banda do Killer | Teste |
| Não enviar atualizações do Windows para outros PCs (P2P) | Baixo |
| Remover limite de rede durante áudio e vídeo | Baixo |
| Desligar telemetria do Windows e do Killer | Baixo |

Ajustes que dependem de hardware (Wi-Fi Intel/Killer, serviços Killer) só aparecem quando o PC tem esse hardware.

**O que o PC Tuner não faz de propósito:** "limpar RAM", desligar SysMain ou a busca do Windows, mexer em TCP pelo registro e outros truques populares sem ganho mensurável, que podem quebrar outras coisas.

## Como usar

### Baixar pronto

Na página de [Releases](../../releases), baixe `PcTuner-win-x64.zip`, extraia e abra o `PcTuner.exe`. Ele pede permissão de administrador, porque mexe em serviços, energia e na placa de rede.

### Compilar

Requisitos: Windows 10/11 e [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test
dotnet run --project src/PcTuner
```

Para gerar um `.exe` único que não precisa do .NET instalado:

```powershell
dotnet publish src/PcTuner -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Histórico e segurança

- Toda mudança vai para `%LOCALAPPDATA%\PcTuner\history.log`.
- Desligar um ajuste volta ao padrão do Windows.
- A limpeza só apaga cache e temporários que o sistema recria. Na pasta de temporários, só o que tem mais de 1 dia.
- O teste de rede usa os servidores públicos da Cloudflare (`speed.cloudflare.com` e `1.1.1.1`). Nenhum outro dado sai do PC.

## Estrutura

```
src/PcTuner/
  Core/        lógica: ajustes, inicialização, limpeza, teste de rede, informações do sistema
  ViewModels/  estado das listas da interface
  Views/       telas
tests/PcTuner.Tests/   testes da lógica (estatísticas de ping, tamanhos, inicialização)
```

## Próximas ideias

- Temperaturas e uso de CPU/GPU em tempo real
- Histórico dos testes de rede com gráfico
- Tema escuro
- Perfis ("Jogo", "Bateria") que ligam vários ajustes de uma vez

## Licença

[MIT](LICENSE)
