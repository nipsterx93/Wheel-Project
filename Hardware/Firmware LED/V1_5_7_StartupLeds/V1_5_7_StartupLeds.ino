// --------------------------------------------------------------------
// Firmware LED V1.5.7 (DYNAMIC ASYMMETRIC STAGE 1 & TIMING FIX)
// Hardware: Arduino Leonardo/Uno/Nano
// Base: V1.5.6
// --------------------------------------------------------------------

#include <Adafruit_NeoPixel.h>

// --- HARDWARE CONFIG ---
#define PIN_RPM      8
#define NUM_RPM      22

#define PIN_LEFT     10
#define NUM_LEFT     24 

#define PIN_RIGHT    6
#define NUM_RIGHT    24 

#define BAUDRATE     115200

Adafruit_NeoPixel stripRPM   = Adafruit_NeoPixel(NUM_RPM,   PIN_RPM,   NEO_GRB + NEO_KHZ800);
Adafruit_NeoPixel stripLeft  = Adafruit_NeoPixel(NUM_LEFT,  PIN_LEFT,  NEO_GRB + NEO_KHZ800);
Adafruit_NeoPixel stripRight = Adafruit_NeoPixel(NUM_RIGHT, PIN_RIGHT, NEO_GRB + NEO_KHZ800);

// --- STRUTTURA EVENTI ---
struct EventConfig {
  bool active;            
  int zoneA_start;
  int zoneA_count;
  bool zoneB_enabled;
  int zoneB_start;
  int zoneB_count;
  uint32_t color1;
  uint32_t color2;        
  bool blinking;
  int blinkInterval;      
};

EventConfig events[7];

// --- RPM & GLOBAL VARS ---
int rpmLedStart = 0;         
int rpmLedCount = 22;        
int brightnessBack = 255;    
int brightnessRpm = 200;     
int idleMode = 0; 
int rpmStyle = 0; 

bool useRpmGradient = true;
uint32_t rpmCustomMap[NUM_RPM]; 

uint32_t idleColor = 0; 
uint32_t rpmColorStart = 0;
uint32_t rpmColorEnd = 0;

bool gameRunning = false;
int currentRpmPercent = 0;
int currentFlag = 0;         

uint32_t logicColorsLeft[6];
uint32_t logicColorsRight[6];

// Animazioni
int kittPos = 0;
int kittDir = 1;
unsigned long lastKittUpdate = 0;

// Variabili per l'Hardware Check Avanzato
bool bootCheckComplete = false;
int bootStage = 1;             
int bootStep = 0;
unsigned long lastBootUpdate = 0;

// Ricalibrati i tempi: lo Stage 1 sale a 120ms per marcare il salto laterale
const int DELAY_STAGE1_MS = 100; // Più cadenzato per spezzare l'illusione ottica
const int DELAY_STAGE2_MS = 60;  
const int DELAY_STAGE3_MS = 80;  

uint32_t colorBootCheck;
uint32_t colorBootPulse;
uint32_t colorRpmIdleScan;

const int orderLeft[]  = {0, 1, 2, 3, 4, 5};
const int orderRight[] = {11, 10, 9, 8, 7, 6};

void setup() {
  Serial.begin(BAUDRATE);
  Serial.setTimeout(5); 
  
  stripLeft.begin(); stripLeft.setBrightness(255); stripLeft.show();
  stripRight.begin(); stripRight.setBrightness(255); stripRight.show();
  stripRPM.begin(); stripRPM.setBrightness(255); stripRPM.show();

  clearAllState(); 

  colorBootCheck = stripLeft.Color(0, 150, 255);   
  colorBootPulse = stripLeft.Color(0, 50, 110);    
  colorRpmIdleScan = stripRPM.Color(160, 0, 0);    

  idleColor = stripLeft.Color(255, 255, 255);
  rpmColorStart = stripRPM.Color(0, 255, 0);
  rpmColorEnd   = stripRPM.Color(255, 0, 0);

  // Default Events 
  initEvent(0, 0, 3, true, 19, 3, stripRPM.Color(255, 255, 0), 0, true, 300); // Yellow
  initEvent(1, 0, 22, false, 0, 0, stripRPM.Color(255, 0, 0), 0, true, 200);  // Red
  initEvent(2, 0, 3, true, 19, 3, stripRPM.Color(0, 0, 255), 0, true, 300);   // Blue
  initEvent(3, 0, 3, true, 19, 3, stripRPM.Color(0, 255, 0), 0, true, 500);   // Green
  initEvent(4, 3, 3, true, 16, 3, stripRPM.Color(0, 255, 255), 0, false, 0);  // ABS
  initEvent(5, 3, 3, true, 16, 3, stripRPM.Color(255, 100, 0), 0, false, 0);  // TC
  initEvent(6, 0, 22, false, 0, 0, stripRPM.Color(255, 0, 0), stripRPM.Color(0, 0, 255), true, 150); // PIT
}

void clearAllState() {
  for(int i=0; i<6; i++) { logicColorsLeft[i] = 0; logicColorsRight[i] = 0; }
  for(int i=0; i<NUM_RPM; i++) rpmCustomMap[i] = 0;
  clearAllEvents();
}

void clearAllEvents() {
  for(int i=0; i<7; i++) events[i].active = false;
}

void initEvent(int id, int zA_s, int zA_c, bool zB_en, int zB_s, int zB_c, uint32_t c1, uint32_t c2, bool blink, int rate) {
  events[id].active = false;
  events[id].zoneA_start = zA_s; events[id].zoneA_count = zA_c;
  events[id].zoneB_enabled = zB_en; events[id].zoneB_start = zB_s; events[id].zoneB_count = zB_c;
  events[id].color1 = c1; events[id].color2 = c2;
  events[id].blinking = blink; events[id].blinkInterval = rate;
}

void loop() {
  handleSerial();

  if (gameRunning) {
    renderGameMode();
  } else {
    renderIdleMode();
  }
  
  stripLeft.show();
  stripRight.show();
  stripRPM.show();
}

void handleSerial() {
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();
    
    if (cmd == "WHO") { Serial.println("ID:SIMRIG_LEDS"); return; }
    if (cmd == "RESET") { clearAllState(); bootCheckComplete = false; bootStage = 1; bootStep = 0; return; } 

    if (cmd.startsWith("GAME:")) {
      int state = cmd.substring(5).toInt();
      gameRunning = (state == 1);
      if (!gameRunning) {
        clearAllEvents();
        stripRPM.clear(); 
      }
    }
    else if (cmd.startsWith("RPM:")) currentRpmPercent = cmd.substring(4).toInt();
    else if (cmd.startsWith("EVT:")) {
      int firstSep = cmd.indexOf(':');
      int secondSep = cmd.indexOf(':', firstSep + 1);
      if (secondSep > 0) {
        int id = cmd.substring(firstSep + 1, secondSep).toInt();
        int state = cmd.substring(secondSep + 1).toInt();
        if (id >= 0 && id < 7) {
          events[id].active = (state == 1);
        }
      }
    }
    else if (cmd.startsWith("EVTCFG:")) { parseEventConfig(cmd); }
    else if (cmd.startsWith("RPMCFG:")) {
      int firstSep = cmd.indexOf(':');
      int secondSep = cmd.indexOf(':', firstSep + 1);
      if (secondSep > 0) {
        rpmLedStart = cmd.substring(firstSep + 1, secondSep).toInt();
        rpmLedCount = cmd.substring(secondSep + 1).toInt();
        if (rpmLedCount < 1) rpmLedCount = 1;
        if (rpmLedStart + rpmLedCount > NUM_RPM) rpmLedCount = NUM_RPM - rpmLedStart;
      }
    }
    else if (cmd.startsWith("RPMSTYLE:")) { rpmStyle = cmd.substring(9).toInt(); }
    else if (cmd.startsWith("RPMGRAD:")) { useRpmGradient = (cmd.substring(8).toInt() == 1); }
    else if (cmd.startsWith("RPMSEG:")) {
      int idx1 = cmd.indexOf(':'); int idx2 = cmd.indexOf(':', idx1 + 1); int idx3 = cmd.indexOf(':', idx2 + 1);
      if(idx3 > 0) {
        int start = cmd.substring(idx1+1, idx2).toInt();
        int count = cmd.substring(idx2+1, idx3).toInt();
        String rgb = cmd.substring(idx3+1);
        int r = rgb.substring(0, rgb.indexOf(',')).toInt();
        int g = rgb.substring(rgb.indexOf(',')+1, rgb.lastIndexOf(',')).toInt();
        int b = rgb.substring(rgb.lastIndexOf(',')+1).toInt();
        uint32_t c = stripRPM.Color(r,g,b);
        for(int i=0; i<count; i++) { if(start+i < NUM_RPM) rpmCustomMap[start+i] = c; }
      }
    }
    else if (cmd.startsWith("RPMCOLS:")) {
      int firstColon = cmd.indexOf(':'); int midColon = cmd.indexOf(':', firstColon + 1); 
      if (midColon > 0) {
        String startStr = cmd.substring(firstColon + 1, midColon); String endStr = cmd.substring(midColon + 1);
        int r1 = startStr.substring(0, startStr.indexOf(',')).toInt(); int g1 = startStr.substring(startStr.indexOf(',')+1, startStr.lastIndexOf(',')).toInt(); int b1 = startStr.substring(startStr.lastIndexOf(',')+1).toInt();
        rpmColorStart = stripRPM.Color(r1, g1, b1);
        int r2 = endStr.substring(0, endStr.indexOf(',')).toInt(); int g2 = endStr.substring(endStr.indexOf(',')+1, endStr.lastIndexOf(',')).toInt(); int b2 = endStr.substring(endStr.lastIndexOf(',')+1).toInt();
        rpmColorEnd = stripRPM.Color(r2, g2, b2);
      }
    }
    else if (cmd.startsWith("IDLE:")) {
      String modeStr = cmd.substring(5);
      if (modeStr == "Rainbow") idleMode = 0; else if (modeStr == "Static") idleMode = 1; else if (modeStr == "Breath") idleMode = 2; else if (modeStr == "Off") idleMode = 3;
      bootCheckComplete = true; 
    }
    else if (cmd.startsWith("IDLECOL:")) {
      String rgbStr = cmd.substring(8);
      int rSep = rgbStr.indexOf(','); int gSep = rgbStr.indexOf(',', rSep + 1);
      if (gSep > 0) {
        int r = rgbStr.substring(0, rSep).toInt(); int g = rgbStr.substring(rSep + 1, gSep).toInt(); int b = rgbStr.substring(gSep + 1).toInt();
        idleColor = stripLeft.Color(r, g, b);
      }
    }
    else if (cmd.startsWith("BRT:")) {
      int firstSep = cmd.indexOf(':'); int secondSep = cmd.indexOf(':', firstSep + 1);
      if (secondSep > 0) { brightnessBack = cmd.substring(firstSep + 1, secondSep).toInt(); brightnessRpm = cmd.substring(secondSep + 1).toInt(); }
    }
    else if (cmd.startsWith("BL:")) { parseButtonColor(cmd, true); bootCheckComplete = true; }
    else if (cmd.startsWith("BR:")) { parseButtonColor(cmd, false); bootCheckComplete = true; }
  }
}

void renderGameMode() {
  for(int i=0; i<6; i++) {
    uint32_t cL = logicColorsLeft[i]; uint32_t cR = logicColorsRight[i];
    for(int k=0; k<4; k++) { applyPixel(stripLeft, (i*4)+k, cL, brightnessBack); applyPixel(stripRight, (i*4)+k, cR, brightnessBack); }
  }
  stripRPM.clear(); 
  
  renderRpmBase();
  for (int i=4; i<=6; i++) { if (events[i].active) renderEvent(i); } 
  for (int i=0; i<=3; i++) { if (events[i].active) renderEvent(i); } 
}

void renderRpmBase() {
  if (rpmLedCount > 0) {
    int totalSteps = (rpmStyle == 2 || rpmStyle == 3) ? (rpmLedCount / 2) : rpmLedCount;
    if ((rpmStyle == 2 || rpmStyle == 3) && rpmLedCount % 2 != 0) totalSteps++;
    int activeSteps = map(currentRpmPercent, 0, 100, 0, totalSteps);

    for (int i = 0; i < totalSteps; i++) {
      uint32_t c = 0;
      if (useRpmGradient) { c = lerpColor(rpmColorStart, rpmColorEnd, i, totalSteps); } 
      else { if (i < NUM_RPM) c = rpmCustomMap[i]; }
      
      if (i < activeSteps) {
        int physIdx = -1; int physIdxMirror = -1;
        switch(rpmStyle) {
          case 0: physIdx = rpmLedStart + i; break;
          case 1: physIdx = rpmLedStart + (rpmLedCount - 1 - i); break;
          case 2: { int half = rpmLedCount / 2; physIdx = (rpmLedStart + half) + i; physIdxMirror = (rpmLedStart + half - 1) - i; } break;
          case 3: { physIdx = rpmLedStart + i; physIdxMirror = (rpmLedStart + rpmLedCount - 1) - i; } break;
        }
        if(physIdx >= rpmLedStart && physIdx < rpmLedStart + rpmLedCount) applyPixel(stripRPM, physIdx, c, brightnessRpm);
        if(physIdxMirror >= rpmLedStart && physIdxMirror < rpmLedStart + rpmLedCount) applyPixel(stripRPM, physIdxMirror, c, brightnessRpm);
      }
    }
  }
}

void renderEvent(int id) {
  EventConfig *e = &events[id];
  uint32_t colorToDraw = 0;
  bool draw = true;

  if (e->blinking) {
    bool state = (millis() / e->blinkInterval) % 2 == 0;
    if (id == 6) { colorToDraw = state ? e->color1 : e->color2; } 
    else { if (state) colorToDraw = e->color1; else draw = false; }
  } else { colorToDraw = e->color1; }

  if (draw) {
    for (int i=0; i<e->zoneA_count; i++) { int idx = e->zoneA_start + i; if (idx < NUM_RPM) applyPixel(stripRPM, idx, colorToDraw, brightnessRpm); }
    if (e->zoneB_enabled) { for (int i=0; i<e->zoneB_count; i++) { int idx = e->zoneB_start + i; if (idx < NUM_RPM) applyPixel(stripRPM, idx, colorToDraw, brightnessRpm); } }
  }
}

void renderIdleMode() {
  if (!bootCheckComplete) {
    unsigned long now = millis();
    stripLeft.clear();
    stripRight.clear();
    stripRPM.clear();

    // -------------------------------------------------------------
    // STAGE 1: Scansione Alternata Scandita (120ms) + Dinamica RPM
    // -------------------------------------------------------------
    if (bootStage == 1) {
      if (now - lastBootUpdate > DELAY_STAGE1_MS) {
        bootStep++;
        lastBootUpdate = now;
        if (bootStep >= 12) {
          bootStage = 2; bootStep = 0;
        }
      }

      if (bootStage == 1) {
        int currentRow = bootStep / 2;    
        bool isRightSide = (bootStep % 2 != 0); 

        if (!isRightSide) {
          // Accensione colonna Sinistra
          int ledIndex = orderLeft[currentRow] * 4;
          for(int k=0; k<4; k++) applyPixel(stripLeft, ledIndex + k, colorBootCheck, 255);
          
          // Lato sinistro attivo: i LED centrali della barra RPM rimangono fermi stabili al centro
          applyPixel(stripRPM, 10, colorBootCheck, brightnessRpm);
          applyPixel(stripRPM, 11, colorBootCheck, brightnessRpm);
        } else {
          // Accensione colonna Destra
          int logicalIdx = orderRight[currentRow] - 6; 
          int ledIndex = (5 - logicalIdx) * 4; 
          for(int k=0; k<4; k++) applyPixel(stripRight, ledIndex + k, colorBootCheck, 255);

          // LATO DESTRO ATTIVO: La barra RPM spara i LED verso l'esterno per marcare il ping-pong!
          int outLeft = 10 - currentRow;
          int outRight = 11 + currentRow;
          if (outLeft >= 0) applyPixel(stripRPM, outLeft, colorBootCheck, brightnessRpm);
          if (outRight < NUM_RPM) applyPixel(stripRPM, outRight, colorBootCheck, brightnessRpm);
        }
        return;
      }
    }

    // -------------------------------------------------------------
    // STAGE 2: Scansione Circolare Perimetrale + Caricamento RPM Split
    // -------------------------------------------------------------
    if (bootStage == 2) {
      if (now - lastBootUpdate > DELAY_STAGE2_MS) {
        bootStep++;
        lastBootUpdate = now;
        if (bootStep >= 12) {
          bootStage = 3; bootStep = 0; 
        }
      }

      if (bootStage == 2) {
        if (bootStep < 6) {
          int ledIndex = orderLeft[bootStep] * 4;
          for(int k=0; k<4; k++) applyPixel(stripLeft, ledIndex + k, colorBootCheck, 255);
          
          for(int i = 10; i >= 10 - bootStep; i--) {
            if(i >= 0) applyPixel(stripRPM, i, colorBootCheck, brightnessRpm);
          }
        } else {
          int rightStep = bootStep - 6; 
          int logicalIdx = orderRight[5 - rightStep] - 6; 
          int ledIndex = (5 - logicalIdx) * 4;
          for(int k=0; k<4; k++) applyPixel(stripRight, ledIndex + k, colorBootCheck, 255);

          for(int i = 10; i >= 0; i--) applyPixel(stripRPM, i, colorBootCheck, brightnessRpm);
          for(int i = 11; i <= 11 + rightStep; i++) {
            if(i < NUM_RPM) applyPixel(stripRPM, i, colorBootCheck, brightnessRpm);
          }
        }
        return;
      }
    }

    // -------------------------------------------------------------
    // STAGE 3: BLINK VELOCE TOTALE (3 Flash sincroni)
    // -------------------------------------------------------------
    if (bootStage == 3) {
      if (now - lastBootUpdate > DELAY_STAGE3_MS) {
        bootStep++;
        lastBootUpdate = now;
        if (bootStep >= 6) { 
          bootCheckComplete = true; 
          kittPos = 0;
          kittDir = 1;
          lastKittUpdate = millis();
        }
      }

      if (!bootCheckComplete) {
        bool flashOn = (bootStep % 2 == 0); 
        if (flashOn) {
          for(int i=0; i<NUM_LEFT; i++) { applyPixel(stripLeft, i, colorBootCheck, 255); applyPixel(stripRight, i, colorBootCheck, 255); }
          for(int i=0; i<NUM_RPM; i++) { applyPixel(stripRPM, i, colorBootCheck, brightnessRpm); }
        } else {
          stripLeft.clear(); stripRight.clear(); stripRPM.clear();
        }
        return;
      }
    }
  }

  // --- REGIME DI STANDBY IDLE ---
  if (idleMode == 0) {
    float val = (exp(sin(millis()/2500.0*PI)) - 0.36787944)*108.0;
    int effectiveBrt = (val * 160) / 255.0; 
    for(int i=0; i<NUM_LEFT; i++) { 
      applyPixel(stripLeft, i, colorBootPulse, effectiveBrt); 
      applyPixel(stripRight, i, colorBootPulse, effectiveBrt); 
    }

    for(int i=0; i<NUM_RPM; i++) {
      uint32_t c = stripRPM.getPixelColor(i);
      uint8_t r = (uint8_t)(c >> 16); uint8_t g = (uint8_t)(c >> 8); uint8_t b = (uint8_t)c;
      r = (uint8_t)(r * 0.82); 
      stripRPM.setPixelColor(i, r, g, b);
    }
    
    if (millis() - lastKittUpdate > 75) { 
      applyPixel(stripRPM, kittPos, colorRpmIdleScan, brightnessRpm);
      kittPos += kittDir;
      if (kittPos >= NUM_RPM) { kittPos = NUM_RPM - 2; kittDir = -1; } 
      else if (kittPos < 0) { kittPos = 1; kittDir = 1; }
      lastKittUpdate = millis();
    }
  } 
  else {
    if (idleMode != 3) supercarEffectRPM(); else stripRPM.clear();
    switch (idleMode) { 
      case 1: fillBacklight(idleColor, brightnessBack); break; 
      case 2: breathEffect(); break; 
      case 3: stripLeft.clear(); stripRight.clear(); break; 
    }
  }
}

void parseEventConfig(String cmd) {
  int idx = 0; int nextIdx = 0;
  auto nextInt = [&](int start) -> int {
    nextIdx = cmd.indexOf(':', start);
    if(nextIdx == -1) nextIdx = cmd.length();
    return cmd.substring(start, nextIdx).toInt();
  };

  idx = 7; 
  int id = nextInt(idx); idx = nextIdx + 1;
  
  if (id >= 0 && id < 7) {
    events[id].zoneA_start = nextInt(idx); idx = nextIdx + 1;
    events[id].zoneA_count = nextInt(idx); idx = nextIdx + 1;
    events[id].zoneB_enabled = (nextInt(idx) == 1); idx = nextIdx + 1;
    events[id].zoneB_start = nextInt(idx); idx = nextIdx + 1;
    events[id].zoneB_count = nextInt(idx); idx = nextIdx + 1;
    events[id].blinking = (nextInt(idx) == 1); idx = nextIdx + 1;
    events[id].blinkInterval = nextInt(idx); idx = nextIdx + 1;

    nextIdx = cmd.indexOf(':', idx); String rgb1 = cmd.substring(idx, nextIdx); idx = nextIdx + 1;
    int r1 = rgb1.substring(0, rgb1.indexOf(',')).toInt();
    int g1 = rgb1.substring(rgb1.indexOf(',')+1, rgb1.lastIndexOf(',')).toInt();
    int b1 = rgb1.substring(rgb1.lastIndexOf(',')+1).toInt();
    events[id].color1 = stripRPM.Color(r1, g1, b1);

    String rgb2 = cmd.substring(idx);
    int r2 = rgb2.substring(0, rgb2.indexOf(',')).toInt();
    int g2 = rgb2.substring(rgb2.indexOf(',')+1, rgb2.lastIndexOf(',')).toInt();
    int b2 = rgb2.substring(rgb2.lastIndexOf(',')+1).toInt();
    events[id].color2 = stripRPM.Color(r2, g2, b2);
  }
}

uint32_t lerpColor(uint32_t c1, uint32_t c2, int step, int totalSteps) {
  if (totalSteps <= 1) return c2; 
  float ratio = (float)step / (float)(totalSteps - 1);
  uint8_t r1 = (uint8_t)(c1 >> 16), g1 = (uint8_t)(c1 >> 8), b1 = (uint8_t)c1;
  uint8_t r2 = (uint8_t)(c2 >> 16), g2 = (uint8_t)(c2 >> 8), b2 = (uint8_t)c2;
  uint8_t r = r1 + (uint8_t)((r2 - r1) * ratio);
  uint8_t g = g1 + (uint8_t)((g2 - g1) * ratio);
  uint8_t b = b1 + (uint8_t)((b2 - b1) * ratio);
  return stripRPM.Color(r, g, b);
}

void supercarEffectRPM() {
  for(int i=0; i<NUM_RPM; i++) {
    uint32_t c = stripRPM.getPixelColor(i);
    uint8_t r = (uint8_t)(c >> 16); uint8_t g = (uint8_t)(c >> 8); uint8_t b = (uint8_t)c;
    r = (uint8_t)(r * 0.65); g = (uint8_t)(g * 0.65); b = (uint8_t)(b * 0.65);
    stripRPM.setPixelColor(i, r, g, b);
  }
  if (millis() - lastKittUpdate > 30) {
    uint32_t eyeColor = stripRPM.Color(255, 0, 0); 
    applyPixel(stripRPM, kittPos, eyeColor, brightnessRpm);
    kittPos += kittDir;
    if (kittPos >= NUM_RPM) { kittPos = NUM_RPM - 2; kittDir = -1; } else if (kittPos < 0) { kittPos = 1; kittDir = 1; }
    lastKittUpdate = millis();
  }
}

void breathEffect() {
  float val = (exp(sin(millis()/2000.0*PI)) - 0.36787944)*108.0;
  int effectiveBrt = (val * brightnessBack) / 255.0; 
  for(int i=0; i<NUM_LEFT; i++) { applyPixel(stripLeft, i, idleColor, effectiveBrt); applyPixel(stripRight, i, idleColor, effectiveBrt); }
}

void fillBacklight(uint32_t color, int brightness) {
  for(int i=0; i<NUM_LEFT; i++) applyPixel(stripLeft, i, color, brightness);
  for(int i=0; i<NUM_RIGHT; i++) applyPixel(stripRight, i, color, brightness);
}

void applyPixel(Adafruit_NeoPixel &strip, int idx, uint32_t c, int brightness) {
   if(idx >= strip.numPixels()) return;
   uint8_t r = (uint8_t)(c >> 16); uint8_t g = (uint8_t)(c >> 8); uint8_t b = (uint8_t)c;
   if(brightness < 255) { r = (r * brightness) / 255; g = (g * brightness) / 255; b = (b * brightness) / 255; }
   strip.setPixelColor(idx, r, g, b);
}

void parseButtonColor(String cmd, bool isLeft) {
  int idxSep = cmd.indexOf(':'); int valSep = cmd.indexOf(':', idxSep + 1);
  if (valSep > 0) {
    int btnIdx = cmd.substring(idxSep + 1, valSep).toInt();
    String rgbStr = cmd.substring(valSep + 1);
    int rSep = rgbStr.indexOf(','); int gSep = rgbStr.indexOf(',', rSep + 1);
    if (gSep > 0 && btnIdx >= 0 && btnIdx < 6) {
      int r = rgbStr.substring(0, rSep).toInt(); int g = rgbStr.substring(rSep + 1, gSep).toInt(); int b = rgbStr.substring(gSep + 1).toInt();
      uint32_t c = stripLeft.Color(r, g, b); 
      if (isLeft) logicColorsLeft[btnIdx] = c; else logicColorsRight[btnIdx] = c;
    }
  }
}