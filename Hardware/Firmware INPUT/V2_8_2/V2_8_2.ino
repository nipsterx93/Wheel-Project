// --------------------------------------------------------------------
// Firmware V2.8.2 (DEADZONE UPDATE)
// Hardware: Arduino Leonardo + MCP23017 (Addr 0x20)
// Base: V2.8.1
// --------------------------------------------------------------------

#include <Joystick.h>
#include <Wire.h>
#include <EncoderTool.h>
#include <EEPROM.h>
#include "PaddleClutch.h" 

using namespace EncoderTool;

Joystick_ Joystick(JOYSTICK_DEFAULT_REPORT_ID, JOYSTICK_TYPE_JOYSTICK, 80, 0, false, false, true, true, true, false, false, false, false, false);   

PaddleClutch PaddleClutchManager;
PolledEncoder enc1, enc2, enc3, enc4, encFunky1, encFunky2;

const int PIN_CLUTCH_L = A0;
const int PIN_CLUTCH_R = A1;
const int PIN_ROTARY_1 = A2; 
const int PIN_ROTARY_2 = A3; 
const int PIN_ROTARY_3 = A4; 
const int BTN_START_ENCODERS = 28; 
const int BTN_START_ROTARY   = 40; 

// Parametro Deadzone Frizioni: 0.02 = 2% di taglio a inizio e fine corsa
const float CLUTCH_DEADZONE_PCT = 0.02; 

const int ROTARY_POS_THRESHOLDS[] = { 16, 107, 200, 291, 383, 474, 566, 657, 749, 841, 932 };
int currentRotaryPos[3] = {1, 1, 1}; 
int stableRotaryPos[3] = {1, 1, 1};       
int candidateRotaryPos[3] = {1, 1, 1};    
unsigned long rotaryStabilityTimer[3] = {0, 0, 0}; 
const unsigned long ROTARY_STABILITY_MS = 100; 

const int ROTARY_POS_CLUTCH_MODE   = 9;  
const int ROTARY_POS_CLUTCH_CALIB  = 10; 
const int ROTARY_POS_GROSS_BITE    = 11; 
const int ROTARY_POS_PRECISE_BITE  = 12;

int encModeIndex[4] = {0, 0, 0, 0}; 
int lastSentNormalModePos = -1;

enum class OperationMode { NORMAL, CLUTCH_MODE_SET_PENDING, CLUTCH_MODE_SET_ACTIVE, CLUTCH_CALIBRATION_PENDING, CLUTCH_CALIBRATION_ACTIVE, GROSS_BITE_SET_PENDING, GROSS_BITE_SET_ACTIVE, PRECISE_BITE_SET_PENDING, PRECISE_BITE_SET_ACTIVE };
OperationMode currentMode = OperationMode::NORMAL;

unsigned long modeEntryTimestamp = 0;
const unsigned long MODE_ENTRY_DELAY_MS = 2000;
int lastRotaryPosForMode = 1;

bool isMessageDelayActive = false;
bool shouldReevaluateAfterDelay = false; 
unsigned long messageDelayTimer = 0;
const unsigned long MESSAGE_HOLD_TIME = 1500; 

uint16_t tempLeftMin, tempLeftMax, tempRightMin, tempRightMax;
int initialRawLeft = 0;  
int initialRawRight = 0; 
float tempBitePoint; 
unsigned long lastCalibPrint = 0; 

enum class PreciseBiteStep { TEN_PCT, ONE_PCT, POINT_ONE_PCT };
PreciseBiteStep currentBiteStep = PreciseBiteStep::TEN_PCT;
float biteStepValues[] = {10.0f, 1.0f, 0.1f};
bool tempReverseMode = false;

struct EepromSettings { uint16_t leftMin, leftMax, rightMin, rightMax; float bitePoint; bool reverseMode; bool leftInverted; bool rightInverted; uint8_t validMarker; };
EepromSettings settings;
const uint8_t EEPROM_MARKER = 0xB6; 

const char* SH_MODE_PREFIX = "WMODE:";
const char* SH_MSG_PREFIX  = "WMSG:";
const char* SH_VAL_PREFIX  = "WVAL:";
const char* SH_IDX_PREFIX  = "WIDX:"; 

unsigned long lastMatrixCheck = 0;
unsigned long lastRotaryCheck = 0;
const int INTERVAL_MATRIX = 15; 
const int INTERVAL_ROTARY = 20; 

const int MCP_ADDR = 0x20;
const byte MCP_IODIRA = 0x00; const byte MCP_IODIRB = 0x01;
const byte MCP_GPPUA  = 0x0C; const byte MCP_GPPUB  = 0x0D;
const byte MCP_GPIOA  = 0x12; const byte MCP_GPIOB  = 0x13;

const int NUM_ROWS = 4; const int NUM_COLS = 8; 
bool buttonState[NUM_ROWS][NUM_COLS]; 

int matrixMap[NUM_ROWS][NUM_COLS] = {
  {  0,    1,    2,    3,    4,    5,    12,   14  }, 
  {  6,    7,    8,    9,    10,   11,   13,   15  }, 
  {  16,   17,   18,   19,   20,   26,   28,   -1  }, 
  {  21,   22,   23,   24,   25,   27,   29,   -1  }  
};

struct EncoderState { int oldPos; bool cwActive; bool ccwActive; unsigned long cwTime; unsigned long ccwTime; };
EncoderState encStates[6];
const unsigned long ENC_PULSE_MS = 50;

// Prototipi
void mcpWrite(byte reg, byte val); byte mcpRead(byte reg); void scanMatrix();
void handleEncoderV16(PolledEncoder &enc, int idx, int btnStart, unsigned long now);
void handleRotaryInvertedStabilized(int pin, int idx, unsigned long now);
void loadSettings(); void saveSettings(); void revertToSavedSettings(); 
void sendSimHubMsg(const char* prefix, const char* msg); void sendSimHubMsg(const char* prefix, const __FlashStringHelper* msg);
void sendSimHubVal(const char* prefix, float val); void sendModeUpdate(int encIdx, int modeIdx); 
void sendNormalModeUpdate(int pos); float getCalibPercent(int normVal); void processSimHubCommand(); 
void triggerMessageDelay(const char* msg, bool reevaluate); void triggerMessageDelay(const __FlashStringHelper* msg, bool reevaluate);
int normalizeAxis(int raw, uint16_t minVal, uint16_t maxVal, bool inverted);

void setup() {
  Serial.begin(115200);
  Joystick.begin(false);
  Wire.begin(); Wire.setClock(400000);
  mcpWrite(MCP_IODIRA, 0x00); mcpWrite(MCP_IODIRB, 0xFF); 
  mcpWrite(MCP_GPPUB, 0xFF);  mcpWrite(MCP_GPIOA, 0xFF);  
  for(int i=0; i<6; i++) encStates[i] = {0, false, false, 0, 0};
  enc1.begin(0, 1);    enc2.begin(8, 9);    enc3.begin(7, 4);    enc4.begin(10, 11);  encFunky1.begin(14, 15); encFunky2.begin(16, 6);  
  
  loadSettings();
  
  PaddleClutchManager.setLeftPaddleRange(0, 1023);
  PaddleClutchManager.setRightPaddleRange(0, 1023);
  PaddleClutchManager.setBitePointValue(settings.bitePoint);
  PaddleClutchManager.setReverseMode(settings.reverseMode); 
  sendSimHubMsg(SH_MODE_PREFIX, F("NORMAL"));
  sendSimHubMsg(SH_MSG_PREFIX, F("READY"));
}

void loop() {
  unsigned long now = millis();
  int btnStartEnc1 = 40 + (encModeIndex[0] * 2);
  int btnStartEnc2 = 40 + (encModeIndex[1] * 2);
  int btnStartEnc3 = 40 + (encModeIndex[2] * 2); 
  int btnStartEnc4 = 40 + (encModeIndex[3] * 2); 

  if (isMessageDelayActive) {
      enc1.tick(); enc2.tick(); enc3.tick(); enc4.tick(); encFunky1.tick(); encFunky2.tick();
      handleEncoderV16(enc1, 0, btnStartEnc1, now);
      handleEncoderV16(enc2, 1, btnStartEnc2, now);
      handleEncoderV16(enc3, 2, btnStartEnc3, now);
      handleEncoderV16(enc4, 3, btnStartEnc4, now);
      handleEncoderV16(encFunky1, 4, BTN_START_ENCODERS + 8, now);
      handleEncoderV16(encFunky2, 5, BTN_START_ENCODERS + 10, now);
      if (now - lastRotaryCheck >= INTERVAL_ROTARY) {
          handleRotaryInvertedStabilized(PIN_ROTARY_1, 0, now); handleRotaryInvertedStabilized(PIN_ROTARY_2, 1, now); handleRotaryInvertedStabilized(PIN_ROTARY_3, 2, now); lastRotaryCheck = now;
      }
      if (now >= messageDelayTimer) {
          isMessageDelayActive = false; currentMode = OperationMode::NORMAL;
          if (shouldReevaluateAfterDelay) lastRotaryPosForMode = -1; else lastRotaryPosForMode = stableRotaryPos[0]; 
          sendNormalModeUpdate(stableRotaryPos[0]); sendSimHubMsg(SH_MSG_PREFIX, F("READY"));
      }
      Joystick.sendState(); return; 
  }

  if (Serial.available() > 0) processSimHubCommand();

  enc1.tick(); enc2.tick(); enc3.tick(); enc4.tick(); encFunky1.tick(); encFunky2.tick();

  if (currentMode == OperationMode::PRECISE_BITE_SET_ACTIVE) {
      if (enc1.valueChanged()) {
          int diff = enc1.getValue() - encStates[0].oldPos; encStates[0].oldPos = enc1.getValue();
          float step = biteStepValues[(int)currentBiteStep];
          float rawChange = (step / 100.0f) * 1023.0f; 
          
          if (diff > 0) tempBitePoint += rawChange; 
          else if (diff < 0) tempBitePoint -= rawChange;
          
          tempBitePoint = constrain(tempBitePoint, 0.0f, 1023.0f);
          sendSimHubVal(SH_VAL_PREFIX, getCalibPercent((int)tempBitePoint));
      }
      handleEncoderV16(enc2, 1, btnStartEnc2, now); handleEncoderV16(enc3, 2, btnStartEnc3, now); handleEncoderV16(enc4, 3, btnStartEnc4, now);
      handleEncoderV16(encFunky1, 4, BTN_START_ENCODERS + 8, now); handleEncoderV16(encFunky2, 5, BTN_START_ENCODERS + 10, now);
  } else {
      handleEncoderV16(enc1, 0, btnStartEnc1, now); handleEncoderV16(enc2, 1, btnStartEnc2, now); handleEncoderV16(enc3, 2, btnStartEnc3, now); handleEncoderV16(enc4, 3, btnStartEnc4, now);
      handleEncoderV16(encFunky1, 4, BTN_START_ENCODERS + 8, now); handleEncoderV16(encFunky2, 5, BTN_START_ENCODERS + 10, now);
  }

  if (now - lastMatrixCheck >= INTERVAL_MATRIX) { scanMatrix(); lastMatrixCheck = now; }

  if (now - lastRotaryCheck >= INTERVAL_ROTARY) {
    handleRotaryInvertedStabilized(PIN_ROTARY_1, 0, now); handleRotaryInvertedStabilized(PIN_ROTARY_2, 1, now); handleRotaryInvertedStabilized(PIN_ROTARY_3, 2, now);
    int modeEnc3 = stableRotaryPos[1] - 1; int modeEnc4 = stableRotaryPos[2] - 1;
    if (modeEnc3 != encModeIndex[2]) { encModeIndex[2] = modeEnc3; sendModeUpdate(2, encModeIndex[2]); }
    if (modeEnc4 != encModeIndex[3]) { encModeIndex[3] = modeEnc4; sendModeUpdate(3, encModeIndex[3]); }

    int rot1Pos = stableRotaryPos[0]; 
    bool rotaryHasMoved = (rot1Pos != lastRotaryPosForMode);

    if (rotaryHasMoved) {
        bool revertNeeded = false; const __FlashStringHelper* cancelMsg = F("CANCELLED");
        if (currentMode == OperationMode::CLUTCH_CALIBRATION_ACTIVE) { cancelMsg = F("CLUTCH CALIBRATION CANCELLED"); revertNeeded = true; }
        else if (currentMode == OperationMode::GROSS_BITE_SET_ACTIVE) { cancelMsg = F("GROSS BITE SET CANCELLED"); revertNeeded = true; }
        else if (currentMode == OperationMode::PRECISE_BITE_SET_ACTIVE) { cancelMsg = F("PRECISE BITE SET CANCELLED"); revertNeeded = true; }
        else if (currentMode == OperationMode::CLUTCH_MODE_SET_ACTIVE) { cancelMsg = F("CLUTCH MODE CANCELLED"); revertNeeded = true; }
        else if (currentMode != OperationMode::NORMAL) { currentMode = OperationMode::NORMAL; sendNormalModeUpdate(rot1Pos); sendSimHubMsg(SH_MSG_PREFIX, F("READY")); }
        if (revertNeeded) { revertToSavedSettings(); triggerMessageDelay(cancelMsg, true); } 
        else if (currentMode == OperationMode::NORMAL) { lastRotaryPosForMode = rot1Pos; modeEntryTimestamp = now; }
    }

    switch (currentMode) {
        case OperationMode::NORMAL:
            if (rot1Pos != lastSentNormalModePos) { lastSentNormalModePos = rot1Pos; sendNormalModeUpdate(rot1Pos); }
            if (rotaryHasMoved) { 
                if (rot1Pos == ROTARY_POS_CLUTCH_MODE) { currentMode = OperationMode::CLUTCH_MODE_SET_PENDING; sendSimHubMsg(SH_MODE_PREFIX, F("CLUTCH MODE PENDING")); sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH MODE PENDING")); } 
                else if (rot1Pos == ROTARY_POS_CLUTCH_CALIB) { currentMode = OperationMode::CLUTCH_CALIBRATION_PENDING; sendSimHubMsg(SH_MODE_PREFIX, F("CLUTCH CAL PENDING")); sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH CAL PENDING")); } 
                else if (rot1Pos == ROTARY_POS_GROSS_BITE) { currentMode = OperationMode::GROSS_BITE_SET_PENDING; sendSimHubMsg(SH_MODE_PREFIX, F("GROSS BITE PENDING")); sendSimHubMsg(SH_MSG_PREFIX, F("GROSS BITE PENDING")); } 
                else if (rot1Pos == ROTARY_POS_PRECISE_BITE) { currentMode = OperationMode::PRECISE_BITE_SET_PENDING; sendSimHubMsg(SH_MODE_PREFIX, F("PRECISE BITE PENDING")); sendSimHubMsg(SH_MSG_PREFIX, F("PRECISE BITE PENDING")); }
            }
            break;
        case OperationMode::CLUTCH_MODE_SET_PENDING:
            if (now - modeEntryTimestamp > MODE_ENTRY_DELAY_MS) {
                if (rot1Pos == ROTARY_POS_CLUTCH_MODE) { currentMode = OperationMode::CLUTCH_MODE_SET_ACTIVE; tempReverseMode = settings.reverseMode; sendSimHubMsg(SH_MODE_PREFIX, F("CLUTCH MODE ACTIVE")); if (tempReverseMode) sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH MODE REVERSE")); else sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH MODE NORMAL")); } 
                else { currentMode = OperationMode::NORMAL; sendSimHubMsg(SH_MSG_PREFIX, F("READY")); }
            }
            break;
        case OperationMode::CLUTCH_CALIBRATION_PENDING:
            if (now - modeEntryTimestamp > MODE_ENTRY_DELAY_MS) {
                if (rot1Pos == ROTARY_POS_CLUTCH_CALIB) { 
                    currentMode = OperationMode::CLUTCH_CALIBRATION_ACTIVE; 
                    tempLeftMin = 1023; tempLeftMax = 0; tempRightMin = 1023; tempRightMax = 0; 
                    initialRawLeft = analogRead(PIN_CLUTCH_L);
                    initialRawRight = analogRead(PIN_CLUTCH_R);
                    sendSimHubMsg(SH_MODE_PREFIX, F("CLUTCH CAL ACTIVE")); 
                    sendSimHubMsg(SH_MSG_PREFIX, F("MOVE PADDLES THEN PRESS SAVE")); 
                } 
                else { currentMode = OperationMode::NORMAL; sendSimHubMsg(SH_MSG_PREFIX, F("READY")); }
            }
            break;
        case OperationMode::GROSS_BITE_SET_PENDING:
             if (now - modeEntryTimestamp > MODE_ENTRY_DELAY_MS) {
                if (rot1Pos == ROTARY_POS_GROSS_BITE) { currentMode = OperationMode::GROSS_BITE_SET_ACTIVE; sendSimHubMsg(SH_MODE_PREFIX, F("GROSS BITE ACTIVE")); sendSimHubMsg(SH_MSG_PREFIX, F("MOVE MASTER TO BITE THEN PRESS SAVE TO STORE")); } 
                else { currentMode = OperationMode::NORMAL; sendSimHubMsg(SH_MSG_PREFIX, F("READY")); }
             }
             break;
        case OperationMode::PRECISE_BITE_SET_PENDING:
             if (now - modeEntryTimestamp > MODE_ENTRY_DELAY_MS) {
                if (rot1Pos == ROTARY_POS_PRECISE_BITE) { currentMode = OperationMode::PRECISE_BITE_SET_ACTIVE; tempBitePoint = settings.bitePoint; encStates[0].oldPos = enc1.getValue(); sendSimHubMsg(SH_MODE_PREFIX, F("PRECISE BITE ACTIVE")); char msgBuff[70]; int sInt = (int)biteStepValues[(int)currentBiteStep]; int sDec = (int)(biteStepValues[(int)currentBiteStep] * 10) % 10; sprintf_P(msgBuff, PSTR("ROTATE TO ADJUST THEN PRESS SAVE TO STORE, Step +/- %d.%d%%"), sInt, sDec); sendSimHubMsg(SH_MSG_PREFIX, msgBuff); } 
                else { currentMode = OperationMode::NORMAL; sendSimHubMsg(SH_MSG_PREFIX, F("READY")); }
             }
             break;
        case OperationMode::CLUTCH_CALIBRATION_ACTIVE: case OperationMode::GROSS_BITE_SET_ACTIVE: case OperationMode::PRECISE_BITE_SET_ACTIVE: case OperationMode::CLUTCH_MODE_SET_ACTIVE: break; 
    }
    lastRotaryCheck = now;
  }

  int rawLeft = analogRead(PIN_CLUTCH_L); 
  int rawRight = analogRead(PIN_CLUTCH_R);
  
  if (currentMode == OperationMode::CLUTCH_CALIBRATION_ACTIVE) { 
      if (rawLeft < tempLeftMin) tempLeftMin = rawLeft; 
      if (rawLeft > tempLeftMax) tempLeftMax = rawLeft; 
      if (rawRight < tempRightMin) tempRightMin = rawRight; 
      if (rawRight > tempRightMax) tempRightMax = rawRight; 
      
      if (now - lastCalibPrint > 250) { 
          char buff[80]; 
          if (settings.reverseMode) {
              sprintf_P(buff, PSTR("Slave Min:%d Max:%d | Master Min:%d Max:%d"), tempLeftMin, tempLeftMax, tempRightMin, tempRightMax);
          } else {
              sprintf_P(buff, PSTR("Master Min:%d Max:%d | Slave Min:%d Max:%d"), tempLeftMin, tempLeftMax, tempRightMin, tempRightMax);
          }
          sendSimHubMsg(SH_MSG_PREFIX, buff); 
          lastCalibPrint = now; 
      } 
  }
  
  int normLeft = normalizeAxis(rawLeft, settings.leftMin, settings.leftMax, settings.leftInverted);
  int normRight = normalizeAxis(rawRight, settings.rightMin, settings.rightMax, settings.rightInverted);

  if (currentMode == OperationMode::GROSS_BITE_SET_ACTIVE && !isMessageDelayActive) { 
      if (settings.reverseMode) sendSimHubVal(SH_VAL_PREFIX, getCalibPercent(normRight)); 
      else sendSimHubVal(SH_VAL_PREFIX, getCalibPercent(normLeft)); 
  }
  
  int clutchOut = PaddleClutchManager.getClutchOutput(normLeft, normRight); 
  Joystick.setRxAxis(normLeft); 
  Joystick.setRyAxis(normRight); 
  Joystick.setZAxis(clutchOut); 

  bool savePressed = buttonState[2][5]; bool cyclePressed = buttonState[3][5]; static bool lastSaveState = false; static bool lastCycleState = false;
  if (savePressed && !lastSaveState) {
      if (currentMode == OperationMode::CLUTCH_MODE_SET_ACTIVE) { settings.reverseMode = tempReverseMode; saveSettings(); PaddleClutchManager.setReverseMode(settings.reverseMode); triggerMessageDelay(F("CLUTCH MODE SAVED"), false); }
      else if (currentMode == OperationMode::CLUTCH_CALIBRATION_ACTIVE) { 
          settings.leftMin = tempLeftMin; 
          settings.leftMax = tempLeftMax; 
          settings.rightMin = tempRightMin; 
          settings.rightMax = tempRightMax; 
          
          settings.leftInverted = (initialRawLeft > (tempLeftMin + tempLeftMax) / 2);
          settings.rightInverted = (initialRawRight > (tempRightMin + tempRightMax) / 2);
          
          saveSettings(); 
          triggerMessageDelay(F("CLUTCH CALIBRATION SAVED"), false); 
      } 
      else if (currentMode == OperationMode::GROSS_BITE_SET_ACTIVE) { 
          if (settings.reverseMode) PaddleClutchManager.updateBitePoint(normRight, false); 
          else PaddleClutchManager.updateBitePoint(normLeft, true); 
          settings.bitePoint = PaddleClutchManager.getBitePointValue(); 
          saveSettings(); 
          triggerMessageDelay(F("GROSS BITE SAVED"), false); 
      }
      else if (currentMode == OperationMode::PRECISE_BITE_SET_ACTIVE) { 
          settings.bitePoint = tempBitePoint; 
          saveSettings(); 
          PaddleClutchManager.setBitePointValue(settings.bitePoint); 
          triggerMessageDelay(F("PRECISE BITE SAVED"), false); 
      }
  }
  lastSaveState = savePressed;
  if (cyclePressed && !lastCycleState) {
      if (currentMode == OperationMode::PRECISE_BITE_SET_ACTIVE) { int nextStep = (int)currentBiteStep + 1; if (nextStep > 2) nextStep = 0; currentBiteStep = (PreciseBiteStep)nextStep; char msgBuff[70]; int sInt = (int)biteStepValues[(int)currentBiteStep]; int sDec = (int)(biteStepValues[(int)currentBiteStep] * 10) % 10; sprintf_P(msgBuff, PSTR("Step +/- %d.%d%%"), sInt, sDec); sendSimHubMsg(SH_MSG_PREFIX, msgBuff); }
      else if (currentMode == OperationMode::CLUTCH_MODE_SET_ACTIVE) { tempReverseMode = !tempReverseMode; if (tempReverseMode) sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH MODE REVERSE")); else sendSimHubMsg(SH_MSG_PREFIX, F("CLUTCH MODE NORMAL")); }
  }
  lastCycleState = cyclePressed;

  Joystick.sendState();
}

void scanMatrix() {
  bool isManagedMode = ((stableRotaryPos[0] == 2 || stableRotaryPos[0] == 3 || stableRotaryPos[0] == 4 || stableRotaryPos[0] == 5) && currentMode == OperationMode::NORMAL);
  
  for (int r = 0; r < NUM_ROWS; r++) {
    mcpWrite(MCP_GPIOA, ~(1 << r)); byte cols = mcpRead(MCP_GPIOB); 
    for (int c = 0; c < NUM_COLS; c++) {
      bool isPressed = !((cols >> c) & 1);
      if (isPressed != buttonState[r][c]) {
        buttonState[r][c] = isPressed;
        int logicalID = matrixMap[r][c];
        
        if (isManagedMode && (logicalID == 26 || logicalID == 27 || logicalID == 28 || logicalID == 29)) {
            int encIdx = -1;
            if (logicalID == 28) encIdx = 0; else if (logicalID == 29) encIdx = 1; else if (logicalID == 26) encIdx = 2; else if (logicalID == 27) encIdx = 3; 
            if (encIdx != -1) { Serial.print("WPUSH:"); Serial.print(encIdx); Serial.print(":"); Serial.println(isPressed ? 1 : 0); }
        }
      }
      bool suppressPush = false;
      if (r == 2 && c == 4) { if (buttonState[2][0] || buttonState[2][1] || buttonState[2][2] || buttonState[2][3]) suppressPush = true; }
      if (r == 3 && c == 4) { if (buttonState[3][0] || buttonState[3][1] || buttonState[3][2] || buttonState[3][3]) suppressPush = true; }
      
      int logicalID = matrixMap[r][c];
      bool suppressPluginInput = false;
      if (isManagedMode && (logicalID == 26 || logicalID == 27 || logicalID == 28 || logicalID == 29)) suppressPluginInput = true;

      if (logicalID != -1) { 
          if (suppressPush || suppressPluginInput) Joystick.setButton(logicalID, 0); 
          else Joystick.setButton(logicalID, isPressed); 
      }
    }
  }
  mcpWrite(MCP_GPIOA, 0xFF); 
}

void handleEncoderV16(PolledEncoder &enc, int idx, int btnStart, unsigned long now) {
  EncoderState &st = encStates[idx];
  bool isManagedMode = ((stableRotaryPos[0] == 2 || stableRotaryPos[0] == 3 || stableRotaryPos[0] == 4 || stableRotaryPos[0] == 5) && currentMode == OperationMode::NORMAL);
  bool isTargetEncoder = (idx >= 0 && idx <= 3);
  bool suppressHID = (isManagedMode && isTargetEncoder);

  if (enc.valueChanged()) {
    int newPos = enc.getValue();
    int dir = 0; 
    if (newPos > st.oldPos) { st.cwActive = true; st.cwTime = now; st.ccwActive = false; dir = 1; } 
    else if (newPos < st.oldPos) { st.ccwActive = true; st.ccwTime = now; st.cwActive = false; dir = -1; }
    st.oldPos = newPos;
    if (suppressHID && dir != 0) { Serial.print("WENC:"); Serial.print(idx); Serial.print(":"); Serial.println(dir); }
  }

  if (!suppressHID) {
      if (st.cwActive) { if (now - st.cwTime < ENC_PULSE_MS) Joystick.setButton(btnStart, 1); else { Joystick.setButton(btnStart, 0); st.cwActive = false; } }
      if (st.ccwActive) { if (now - st.ccwTime < ENC_PULSE_MS) Joystick.setButton(btnStart + 1, 1); else { Joystick.setButton(btnStart + 1, 0); st.ccwActive = false; } }
  } else {
      Joystick.setButton(btnStart, 0); Joystick.setButton(btnStart + 1, 0);
  }
}

// Helpers
int normalizeAxis(int raw, uint16_t minVal, uint16_t maxVal, bool inverted) {
    if (maxVal <= minVal) return 0; 
    
    // Calcolo della deadzone dinamica basata sul range calibrato
    int range = maxVal - minVal;
    int deadzone = (int)(range * CLUTCH_DEADZONE_PCT); 
    
    int effMin = minVal + deadzone;
    int effMax = maxVal - deadzone;
    
    // Fallback di sicurezza se la calibrazione è troppo stretta
    if (effMax <= effMin) {
        effMin = minVal;
        effMax = maxVal;
    }
    
    raw = constrain(raw, effMin, effMax);
    int mapped = map(raw, effMin, effMax, 0, 1023);
    return inverted ? (1023 - mapped) : mapped;
}

void sendNormalModeUpdate(int pos) { 
    if (pos == 1) sendSimHubMsg(SH_MODE_PREFIX, F("RACE")); 
    else if (pos == 2) sendSimHubMsg(SH_MODE_PREFIX, F("PIT")); 
    else if (pos == 3) sendSimHubMsg(SH_MODE_PREFIX, F("PIT2")); 
    else if (pos == 4) sendSimHubMsg(SH_MODE_PREFIX, F("STRAT")); 
    else if (pos == 5) sendSimHubMsg(SH_MODE_PREFIX, F("FORECAST")); 
    else sendSimHubMsg(SH_MODE_PREFIX, F("NORMAL")); 
}
void mcpWrite(byte reg, byte val) { Wire.beginTransmission(MCP_ADDR); Wire.write(reg); Wire.write(val); Wire.endTransmission(); }
byte mcpRead(byte reg) { Wire.beginTransmission(MCP_ADDR); Wire.write(reg); Wire.endTransmission(); Wire.requestFrom(MCP_ADDR, 1); return Wire.read(); }
void handleRotaryInvertedStabilized(int pin, int idx, unsigned long now) { int raw = analogRead(pin); int detectedPos = 1; for (int i = 0; i < 11; i++) { if (raw >= ROTARY_POS_THRESHOLDS[i]) detectedPos = i + 2; else break; } int finalPos = 13 - detectedPos; if (finalPos != candidateRotaryPos[idx]) { candidateRotaryPos[idx] = finalPos; rotaryStabilityTimer[idx] = now; } else { if (now - rotaryStabilityTimer[idx] > ROTARY_STABILITY_MS) { if (stableRotaryPos[idx] != candidateRotaryPos[idx]) { stableRotaryPos[idx] = candidateRotaryPos[idx]; } } } }
void loadSettings() { EEPROM.get(0, settings); if (settings.validMarker != EEPROM_MARKER) { settings.leftMin = 0; settings.leftMax = 1023; settings.rightMin = 0; settings.rightMax = 1023; settings.bitePoint = 512.0; settings.reverseMode = false; settings.leftInverted = false; settings.rightInverted = false; settings.validMarker = EEPROM_MARKER; saveSettings(); } }
void saveSettings() { EEPROM.put(0, settings); }
void sendSimHubMsg(const char* p, const char* m) { Serial.print(p); Serial.println(m); }
void sendSimHubMsg(const char* p, const __FlashStringHelper* m) { Serial.print(p); Serial.println(m); }
void sendSimHubVal(const char* p, float v) { Serial.print(p); Serial.println(v, 1); }
void sendModeUpdate(int encIdx, int modeIdx) { Serial.print(SH_IDX_PREFIX); Serial.print(encIdx); Serial.print(":"); Serial.println(modeIdx); }
float getCalibPercent(int normVal) { return constrain((normVal / 1023.0) * 100.0, 0.0, 100.0); }
void processSimHubCommand() {
    if (Serial.available() > 0) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();
        if (cmd.equals("WHO")) {
            Serial.println("ID:SIMRIG_INPUT");
            return;
        }
        if (cmd.equals("S")) {
            if (currentMode == OperationMode::NORMAL) {
                sendNormalModeUpdate(stableRotaryPos[0]);
            } else {
                const __FlashStringHelper* modeStr = F("NORMAL");
                switch(currentMode) {
                    case OperationMode::CLUTCH_MODE_SET_PENDING: modeStr = F("CLUTCH MODE PENDING"); break;
                    case OperationMode::CLUTCH_MODE_SET_ACTIVE: modeStr = F("CLUTCH MODE ACTIVE"); break;
                    case OperationMode::CLUTCH_CALIBRATION_PENDING: modeStr = F("CLUTCH CAL PENDING"); break;
                    case OperationMode::CLUTCH_CALIBRATION_ACTIVE: modeStr = F("CALIB ACTIVE"); break;
                    case OperationMode::GROSS_BITE_SET_PENDING: modeStr = F("GROSS BITE PENDING"); break;
                    case OperationMode::GROSS_BITE_SET_ACTIVE: modeStr = F("GROSS BITE ACTIVE"); break;
                    case OperationMode::PRECISE_BITE_SET_PENDING: modeStr = F("PRECISE BITE PENDING"); break;
                    case OperationMode::PRECISE_BITE_SET_ACTIVE: modeStr = F("PRECISE BITE ACTIVE"); break;
                }
                sendSimHubMsg(SH_MODE_PREFIX, modeStr);
            }
            sendSimHubVal(SH_VAL_PREFIX, getCalibPercent((int)PaddleClutchManager.getBitePointValue()));
            for(int i=0; i<4; i++) {
                sendModeUpdate(i, encModeIndex[i]);
            }
        } else if (cmd.startsWith("M:")) {
            String params = cmd.substring(2);
            int splitIdx = params.indexOf(':');
            if (splitIdx > 0) {
                String encStr = params.substring(0, splitIdx);
                String modeStr = params.substring(splitIdx + 1);
                int encIdx = encStr.toInt();
                int modeIdx = modeStr.toInt();
                if (encIdx >= 0 && encIdx < 2 && modeIdx >= 0 && modeIdx < 12) {
                    encModeIndex[encIdx] = modeIdx;
                    sendModeUpdate(encIdx, modeIdx);
                }
            }
        }
    }
}
void revertToSavedSettings() { PaddleClutchManager.setLeftPaddleRange(0, 1023); PaddleClutchManager.setRightPaddleRange(0, 1023); PaddleClutchManager.setBitePointValue(settings.bitePoint); PaddleClutchManager.setReverseMode(settings.reverseMode); tempBitePoint = settings.bitePoint; sendSimHubVal(SH_VAL_PREFIX, getCalibPercent((int)settings.bitePoint)); }
void triggerMessageDelay(const char* msg, bool reevaluate) { sendSimHubMsg(SH_MSG_PREFIX, msg); isMessageDelayActive = true; shouldReevaluateAfterDelay = reevaluate; messageDelayTimer = millis() + MESSAGE_HOLD_TIME; }
void triggerMessageDelay(const __FlashStringHelper* msg, bool reevaluate) { sendSimHubMsg(SH_MSG_PREFIX, msg); isMessageDelayActive = true; shouldReevaluateAfterDelay = reevaluate; messageDelayTimer = millis() + MESSAGE_HOLD_TIME; }