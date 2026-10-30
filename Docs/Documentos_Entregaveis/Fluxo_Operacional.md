Funcionalidade: descreve o fluxo operacional do sistema, da leitura dos sensores à ação da torre.

# Fluxo Operacional

Fluxograma do ciclo completo de operação do RadarTorres, do início da varredura até o registro
em auditoria — uma visão de alto nível, complementar ao [[Diagrama_de_Implantacao]] (física) e ao
[[ALGORITMO_SELECAO_TORRE]] (matemática da seleção).

Fonte: [`fluxo_operacional.mmd`](Fluxo_Operacional/fluxo_operacional.mmd) (Mermaid), renderizado em
[`Fluxo_Operacional.png`](../../assets/Fluxo_Operacional.png).

```mermaid
flowchart TD
    INICIO(["Início"])

    INICIO --> SENSOR["Arduino varre o ambiente\ncom os 2 sensores ultrassônicos\n(ângulo + distância)"]
    INICIO --> CAM["Câmeras capturam frames\npor quadrante"]

    SENSOR -- "Serial USB (texto)" --> CONV["App converte leitura em\nposição cartesiana, determina\nquadrante e exibe no radar"]
    CAM --> VIS["Módulo de visão classifica\no alvo por cor, formato e tamanho"]

    CONV --> LOG1["Registra detecção"]
    VIS --> LOG1

    LOG1 --> SEL["App seleciona a torre mais\nadequada: mesmo quadrante primeiro,\ndepois adjacente, depois sobressalente"]

    SEL --> CMD["Comando enviado ao Arduino"]
    CMD --> CHK{"Distância mínima de\nsegurança e zona morta\nliberam o disparo?"}

    CHK -- "Não" --> BLOQ["Acionamento bloqueado\ne motivo registrado no\nconsole de eventos"]
    CHK -- "Sim" --> ACAO["Torre aciona o alvo\n(laser/LED demonstrativo)"]

    ACAO --> AUD["Ação registrada em auditoria"]
    BLOQ --> AUD

    AUD --> FIM(["Fim do ciclo\n(volta a nova leitura)"])
    FIM -.-> INICIO
```

## Notas

- Este fluxo descreve o caminho **feliz + bloqueio por segurança**; não cobre o controlador
  neuroevolutivo (ver [[CONTROLADOR_NEUROEVOLUTIVO]]) nem as 5 regras heurísticas desligáveis —
  ambos atuam *dentro* da etapa "App seleciona a torre mais adequada".
- "Zona morta" refere-se a `IZonaMortaService`, que pode bloquear o disparo mesmo com alvo
  detectado e torre selecionada.

## Ver também

- [[Diagrama_de_Implantacao]] — topologia física PC ↔ Arduino.
- [[ALGORITMO_SELECAO_TORRE]] — critério de seleção de torre em detalhe.
- [[CONTROLADOR_NEUROEVOLUTIVO]] — controlador híbrido que decide dentro da etapa de seleção.
- [[index|Índice]] (raiz do vault), [[Artigo_index|Artigo]].
