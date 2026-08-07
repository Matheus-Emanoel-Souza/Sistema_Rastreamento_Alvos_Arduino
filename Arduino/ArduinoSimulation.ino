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
 *  Protocolo (ver Documentation/COMUNICACAO_ARDUINO.md para a especificação completa):
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

  Serial.println("STATUS;SYSTEM=ONLINE");
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
