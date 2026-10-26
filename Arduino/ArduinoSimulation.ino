/*
 * ============================================================================
 *  ArduinoSimulation.ino
 * ----------------------------------------------------------------------------
 *  Firmware de TESTE para o TCC "Sistema de Detecção e Seleção de Torres".
 *
 *  Objetivo: permitir validar a comunicação serial PC <-> Arduino ANTES da
 *  montagem completa do hardware de sensores. Este sketch NÃO lê sensores
 *  reais — ele gera alvos fictícios (ângulo/distância) e os envia
 *  periodicamente pela Serial no mesmo protocolo que o firmware definitivo
 *  deverá usar, além de interpretar e responder aos comandos enviados pelo
 *  aplicativo C# (RadarTorres.App).
 *
 *  Protocolo (ver Docs/Tecnica/COMUNICACAO_ARDUINO.md para a especificação completa):
 *
 *   Arduino -> PC:
 *     TARGET;ID=<int>;ANGLE=<0-360>;DIST=<metros>
 *     STATUS;SYSTEM=ONLINE|OFFLINE
 *     ACK;CMD=<comando recebido>
 *     ERROR;REASON=<motivo>
 *
 *   PC -> Arduino:
 *     SYSTEM;ON
 *     SYSTEM;OFF
 *     MODE;DETECTION
 *     MODE;AUTO
 *     SET;MIN_DISTANCE=<metros>
 *     SET;MAX_DISTANCE=<metros>
 *     FIRE;TOWER=<id>;TARGET=<id>
 *     GENOMA;RESET
 *     GENOMA;INDICE=<0-73>;VALOR=<float>
 *     GENOMA;COMMIT
 *     REDE;ENTRADAS=<d>;<mesmo_quad>;<quad_adj>;<angulo>;<vel>;<fator_camada>
 *       Rede neural embutida (só diagnóstico, nunca aciona nada — ver
 *       Docs/Documentos_Entregaveis/Artigo/Como_a_IA_funciona_no_RadarTorres.MD,
 *       Parte 3, e o mesmo bloco de comentário em RadarTorres_Firmware.ino).
 *       Arduino -> PC: STATUS;REDE_AJUSTE=<float>;REDE_CONFIANCA=<float>
 *
 *  Baud Rate padrão: 9600 (configurável no app).
 *  Terminador de linha: '\n' (LF) — compatível com Serial.println() do Arduino
 *  e com SerialPort.NewLine="\n" usado no lado C#.
 * ============================================================================
 */

const long BAUD_RATE = 9600;
const uint8_t SIMULATED_TARGET_COUNT = 3;
const unsigned long TARGET_SEND_INTERVAL_MS = 800;
const float MAX_DISTANCE_METERS = 6.0;

// --------------------------------------------------------------------------
// Estado de um alvo fictício simulado internamente pelo firmware.
// --------------------------------------------------------------------------
struct SimulatedTarget {
  int id;
  float angle;     // 0-360 graus
  float distance;  // metros
};

SimulatedTarget targets[SIMULATED_TARGET_COUNT];

bool systemOn = false;
bool autoMode = false;
float minDistance = 1.5;
float maxDistance = MAX_DISTANCE_METERS;
unsigned long lastSendMillis = 0;

String inputBuffer = "";

// ============================================================================
// REDE NEURAL EMBUTIDA (somente diagnóstico — NUNCA aciona nada sozinha)
// ----------------------------------------------------------------------------
// Espelha RedeNeural.cs (lado PC). Ver o mesmo bloco de comentário, completo,
// em Arduino/RadarTorres_Firmware/RadarTorres_Firmware.ino — implementação
// idêntica aqui, pra poder testar o protocolo sem hardware real.
//
// *** INVARIANTE DE SEGURANÇA: as funções desta seção só calculam e devolvem
//     números pela serial — nenhuma delas aciona nada (não há atuador real
//     neste firmware de simulação, mas a regra vale igual: nunca ligar isto
//     a FIRE/AIM/qualquer efeito). ***
// ============================================================================
const uint8_t REDE_QTD_ENTRADAS = 6;
const uint8_t REDE_QTD_OCULTOS = 8;
const uint8_t REDE_QTD_SAIDAS = 2;
const uint8_t REDE_QTD_GENES = 74; // 6*8+8 + 8*2+2 -- ver RedeNeural.TamanhoGenoma (lado PC)
const float REDE_GENE_VALOR_MAXIMO_ABS = 1000.0; // trava de sanidade, nao e limite de treino

float redeGenomaEmUso[REDE_QTD_GENES];
float redeGenomaRecebendo[REDE_QTD_GENES];
bool redeIndiceRecebido[REDE_QTD_GENES];

// --------------------------------------------------------------------------
// setup()
// --------------------------------------------------------------------------
void setup() {
  Serial.begin(BAUD_RATE);
  randomSeed(analogRead(A0));

  for (uint8_t i = 0; i < SIMULATED_TARGET_COUNT; i++) {
    targets[i].id = i + 1;
    targets[i].angle = random(0, 360);
    targets[i].distance = randomFloat(0.5, maxDistance * 0.8);
  }

  inicializarRedeEmbutida();

  Serial.println("STATUS;SYSTEM=ONLINE");
}

void inicializarRedeEmbutida() {
  for (uint8_t i = 0; i < REDE_QTD_GENES; i++) {
    redeGenomaEmUso[i] = 0.0;
    redeGenomaRecebendo[i] = 0.0;
    redeIndiceRecebido[i] = false;
  }
}

// Extrai o valor de uma chave dentro de uma linha "TIPO;CHAVE=VAL;...",
// retornando "" se a chave não existir.
String extrairCampo(const String &linha, const String &chave) {
  String busca = ";" + chave + "=";
  int idx = linha.indexOf(busca);
  if (idx < 0) return "";

  int inicioValor = idx + busca.length();
  int fim = linha.indexOf(';', inicioValor);
  if (fim < 0) fim = linha.length();
  return linha.substring(inicioValor, fim);
}

// Fiel a RedeNeural.Ativar (lado PC) — ver comentário completo da mesma
// função em RadarTorres_Firmware.ino.
void calcularSaidaRedeEmbutida(const float entradas[REDE_QTD_ENTRADAS], float saida[REDE_QTD_SAIDAS]) {
  float ocultos[REDE_QTD_OCULTOS];

  for (uint8_t s = 0; s < REDE_QTD_OCULTOS; s++) {
    float soma = redeGenomaEmUso[48 + s];
    for (uint8_t e = 0; e < REDE_QTD_ENTRADAS; e++) {
      soma += entradas[e] * redeGenomaEmUso[e * REDE_QTD_OCULTOS + s];
    }
    ocultos[s] = tanh(soma);
  }

  for (uint8_t s = 0; s < REDE_QTD_SAIDAS; s++) {
    float soma = redeGenomaEmUso[72 + s];
    for (uint8_t e = 0; e < REDE_QTD_OCULTOS; e++) {
      soma += ocultos[e] * redeGenomaEmUso[56 + e * REDE_QTD_SAIDAS + s];
    }
    saida[s] = tanh(soma);
  }
}

void tratarComandoGenoma(const String &linha) {
  if (linha == "GENOMA;RESET") {
    for (uint8_t i = 0; i < REDE_QTD_GENES; i++) {
      redeGenomaRecebendo[i] = 0.0;
      redeIndiceRecebido[i] = false;
    }
    Serial.println("ACK;CMD=GENOMA;RESET");
    return;
  }

  if (linha == "GENOMA;COMMIT") {
    for (uint8_t i = 0; i < REDE_QTD_GENES; i++) {
      if (!redeIndiceRecebido[i]) {
        Serial.println("ERROR;REASON=GENOMA INCOMPLETO, FALTA INDICE " + String(i) + " - COMMIT RECUSADO");
        return;
      }
    }
    for (uint8_t i = 0; i < REDE_QTD_GENES; i++) {
      redeGenomaEmUso[i] = redeGenomaRecebendo[i];
    }
    Serial.println("ACK;CMD=GENOMA;COMMIT;STATUS=OK");
    return;
  }

  String sIndice = extrairCampo(linha, "INDICE");
  String sValor = extrairCampo(linha, "VALOR");
  if (sIndice.length() == 0 || sValor.length() == 0) {
    Serial.println("ERROR;REASON=CAMPOS AUSENTES EM GENOMA: " + linha);
    return;
  }

  int indice = sIndice.toInt();
  if (indice < 0 || indice >= REDE_QTD_GENES) {
    Serial.println("ERROR;REASON=INDICE FORA DA FAIXA 0-73 EM GENOMA: " + linha);
    return;
  }

  float valor = constrain(sValor.toFloat(), -REDE_GENE_VALOR_MAXIMO_ABS, REDE_GENE_VALOR_MAXIMO_ABS);
  redeGenomaRecebendo[indice] = valor;
  redeIndiceRecebido[indice] = true;
  Serial.println("ACK;CMD=GENOMA;INDICE=" + String(indice));
}

void tratarComandoRedeEntradas(const String &linha) {
  String sValores = linha.substring(String("REDE;ENTRADAS=").length());
  float entradas[REDE_QTD_ENTRADAS];
  uint8_t lidos = 0;
  int inicio = 0;
  while (lidos < REDE_QTD_ENTRADAS) {
    int fim = sValores.indexOf(';', inicio);
    String campo = (fim < 0) ? sValores.substring(inicio) : sValores.substring(inicio, fim);
    entradas[lidos] = campo.toFloat();
    lidos++;
    if (fim < 0) break;
    inicio = fim + 1;
  }

  if (lidos != REDE_QTD_ENTRADAS) {
    Serial.println("ERROR;REASON=REDE;ENTRADAS PRECISA DE EXATAMENTE 6 VALORES: " + linha);
    return;
  }

  float saida[REDE_QTD_SAIDAS];
  calcularSaidaRedeEmbutida(entradas, saida);

  Serial.print("STATUS;REDE_AJUSTE=");
  Serial.print(saida[0], 6);
  Serial.print(";REDE_CONFIANCA=");
  Serial.println(saida[1], 6);
}

// --------------------------------------------------------------------------
// loop()
// --------------------------------------------------------------------------
void loop() {
  readSerialCommands();

  if (systemOn && millis() - lastSendMillis >= TARGET_SEND_INTERVAL_MS) {
    lastSendMillis = millis();
    updateAndSendTargets();
  }
}

// --------------------------------------------------------------------------
// Lê comandos vindos do PC pela Serial, byte a byte, até encontrar '\n'.
// --------------------------------------------------------------------------
void readSerialCommands() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\n') {
      inputBuffer.trim();
      if (inputBuffer.length() > 0) {
        handleCommand(inputBuffer);
      }
      inputBuffer = "";
    } else if (c != '\r') {
      inputBuffer += c;
    }
  }
}

// --------------------------------------------------------------------------
// Interpreta uma linha de comando recebida do aplicativo C#.
// --------------------------------------------------------------------------
void handleCommand(const String &line) {
  if (line == "SYSTEM;ON") {
    systemOn = true;
    Serial.println("ACK;CMD=SYSTEM;ON");
    Serial.println("STATUS;SYSTEM=ONLINE");
  }
  else if (line == "SYSTEM;OFF") {
    systemOn = false;
    Serial.println("ACK;CMD=SYSTEM;OFF");
    Serial.println("STATUS;SYSTEM=OFFLINE");
  }
  else if (line == "MODE;DETECTION") {
    autoMode = false;
    Serial.println("ACK;CMD=MODE;DETECTION");
  }
  else if (line == "MODE;AUTO") {
    autoMode = true;
    Serial.println("ACK;CMD=MODE;AUTO");
  }
  else if (line.startsWith("SET;MIN_DISTANCE=")) {
    minDistance = line.substring(String("SET;MIN_DISTANCE=").length()).toFloat();
    Serial.println("ACK;CMD=" + line);
  }
  else if (line.startsWith("SET;MAX_DISTANCE=")) {
    maxDistance = line.substring(String("SET;MAX_DISTANCE=").length()).toFloat();
    Serial.println("ACK;CMD=" + line);
  }
  else if (line.startsWith("FIRE;TOWER=")) {
    // Formato esperado: FIRE;TOWER=<id>;TARGET=<id>
    Serial.println("ACK;CMD=" + line);
    // Em hardware real, aqui seria acionado o indicador/laser de baixa potência
    // correspondente à torre informada. Neste simulador, apenas confirmamos.
  }
  else if (line.startsWith("GENOMA;")) {
    tratarComandoGenoma(line);
  }
  else if (line.startsWith("REDE;ENTRADAS=")) {
    tratarComandoRedeEntradas(line);
  }
  else {
    Serial.println("ERROR;REASON=COMANDO NAO RECONHECIDO: " + line);
  }
}

// --------------------------------------------------------------------------
// Move os alvos fictícios de forma pseudo-aleatória e envia as leituras.
// --------------------------------------------------------------------------
void updateAndSendTargets() {
  for (uint8_t i = 0; i < SIMULATED_TARGET_COUNT; i++) {
    targets[i].angle += randomFloat(-6.0, 6.0);
    if (targets[i].angle < 0) targets[i].angle += 360.0;
    if (targets[i].angle >= 360.0) targets[i].angle -= 360.0;

    targets[i].distance += randomFloat(-0.3, 0.3);
    if (targets[i].distance < 0.3) targets[i].distance = 0.3;
    if (targets[i].distance > maxDistance) targets[i].distance = maxDistance;

    sendTargetMessage(targets[i]);
  }
}

void sendTargetMessage(const SimulatedTarget &t) {
  Serial.print("TARGET;ID=");
  Serial.print(t.id);
  Serial.print(";ANGLE=");
  Serial.print(t.angle, 1);
  Serial.print(";DIST=");
  Serial.println(t.distance, 2);
}

// --------------------------------------------------------------------------
// Utilitário: número float pseudo-aleatório dentro de um intervalo.
// --------------------------------------------------------------------------
float randomFloat(float minValue, float maxValue) {
  return minValue + (maxValue - minValue) * (random(0, 10001) / 10000.0);
}
