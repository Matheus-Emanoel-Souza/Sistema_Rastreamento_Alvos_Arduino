Funcionalidade: documenta a origem e o escopo do diagrama de implantação do RadarTorres gerado a partir do código real da branch `banco-dados`.

# Diagrama de Implantação — RadarTorres

- **Branch analisada:** `banco-dados`
- **Commit analisado:** `fea50b813534235344dbdcf5665c03c81c3a0a26`
- **Arquivos:** `Diagrama_Implantacao_RadarTorres.pdf` (A3 paisagem, 2 páginas, versão final usada na apresentação).

> **Nota:** o PDF atual foi substituído por uma versão com acabamento visual revisado. O arquivo `Diagrama_Implantacao_RadarTorres.tex` mantido neste diretório corresponde à versão anterior (gerada via LaTeX/TikZ) e **não é mais a fonte editável do PDF vigente**. Caso a fonte editável da versão atual seja disponibilizada, ela deve substituir este `.tex`.

## Principais arquivos do repositório usados para confirmar a arquitetura

- `src/RadarTorres.App/NeuralEvolution/TreinadorNeat.cs`, `RedeNeat.cs` — confirmam **SharpNEAT** (topologia variável, especiação), não a arquitetura antiga fixa 6→8→2 com algoritmo genético simples (`AlgoritmoGenetico.cs`/`RedeNeural.cs`, presentes no repositório mas fora de uso em produção).
- `src/RadarTorres.App/NeuralEvolution/ControladorHibrido*.cs` e `ControladorTreinadoProvider.cs` — camada heurística + rede NEAT carregada, V1 (7→2) e V2 (15→3).
- `src/RadarTorres.App/Services/SerialCommunicationService.cs` + `SerialProtocolParser.cs` e `Software/Integracoes/COMUNICACAO_ARDUINO.md` — protocolo serial texto `TIPO;CHAVE=VALOR`.
- `Arduino/RadarTorres_Firmware/RadarTorres_Firmware.ino` — 2x sensor ultrassônico HC-SR04, 4 torres com motor de passo (pan/tilt) + LED.
- `src/RadarTorres.App/ComputerVision/*` (CameraCaptureService, OnnxYoloDetectionService, `Models/YoloModel/yolo26n.onnx`) — visão computacional via câmeras USB + YOLO (ONNX Runtime).
- `src/RadarTorres.App/Data`, `Repositories/Sqlite*Repository.cs` — SQLite (`Microsoft.Data.Sqlite`), WAL + busy_timeout, banco em `%AppData%\RadarTorres\Data\radartorres.db`.
- `src/RadarTorres.App/Services/ObjetoDetectadoExportService.cs` — exportação CSV/XML/PDF (PdfSharp).
- `src/RadarTorres.App/Services/SimulationService.cs` — modo simulação, mesmo DTO `SensorReading` da leitura real.
- `installer/RadarTorres.iss` — instalador Inno Setup, instala em `{autopf}\RadarTorres`, não usa `RadarTorres.Launcher`.

## Observação sobre caminhos de treinamento vs. inferência

O treinamento evolutivo (NEAT) roda sob demanda na tela de Treinamento do Administrador e grava o genoma vencedor em JSON. A inferência em tempo real apenas carrega esse genoma já treinado (via `ControladorTreinadoProvider`) e não treina nada em tempo real — os dois fluxos só se encontram no arquivo JSON do genoma. Ver nota na página 2 do diagrama.
