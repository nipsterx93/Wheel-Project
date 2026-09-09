/*
  PaddleClutch.h - PaddleClutch manager class
  Created by Morgan Gardner. Modified for V2.3 (Reverse Mode Logic).
*/

#ifndef PaddleClutch_h
#define PaddleClutch_h

#include "Arduino.h"
#include <math.h> 

class PaddleClutch
{

public:
  struct calibration
  {
    uint16_t lpMin;     // Left Paddle Min
    uint16_t lpMax;     // Left Paddle Max
    uint16_t rpMin;     // Right Paddle Min
    uint16_t rpMax;     // Right Paddle Max
    float    btptValue; // Bite point value (0-1023)
  } calibVals;

  bool _reverseMode; // TRUE = Destra è Master, Sinistra è Slave

  PaddleClutch()
  {
    calibVals.lpMin = 0; calibVals.lpMax = 1023;
    calibVals.rpMin = 0; calibVals.rpMax = 1023;
    calibVals.btptValue = 512.0f; 
    _reverseMode = false; // Default: Sinistra Master
  }

  // Setta la modalità (chiamato dal Firmware)
  void setReverseMode(bool reverse) {
      _reverseMode = reverse;
  }

  bool getReverseMode() {
      return _reverseMode;
  }

  // ===================================================================================
  // LOGICA FRIZIONE V2.3 (Master/Slave Dinamico)
  // ===================================================================================
  uint16_t getClutchOutput(uint16_t leftPaddleReading, uint16_t rightPaddleReading)
  {
    // 1. Mappa i valori grezzi (ognuno con la SUA calibrazione fisica)
    uint16_t left_mapped = map(constrain(leftPaddleReading, calibVals.lpMin, calibVals.lpMax), calibVals.lpMin, calibVals.lpMax, 0, 1023);
    uint16_t right_mapped = map(constrain(rightPaddleReading, calibVals.rpMin, calibVals.rpMax), calibVals.rpMin, calibVals.rpMax, 0, 1023);

    uint16_t masterOut, slaveOutLimited;

    // 2. Applica la logica Master/Slave in base alla modalità
    if (!_reverseMode) {
        // NORMAL: Sinistra (Left) è Master, Destra (Right) è Slave
        masterOut = left_mapped;
        // Slave limitata dal Bite Point
        slaveOutLimited = map(right_mapped, 0, 1023, 0, (long)round(calibVals.btptValue));
    } else {
        // REVERSE: Destra (Right) è Master, Sinistra (Left) è Slave
        masterOut = right_mapped;
        // Slave limitata dal Bite Point
        slaveOutLimited = map(left_mapped, 0, 1023, 0, (long)round(calibVals.btptValue));
    }

    // 3. Logica MAX (Vince il valore più alto)
    // Sostituito max() con operatore ternario per compatibilità
    uint16_t finalOutput = (masterOut > slaveOutLimited) ? masterOut : slaveOutLimited;

    // 4. Disinnesto completo (Entrambe al 98%)
    if (left_mapped > 1002 && right_mapped > 1002) {
        finalOutput = 1023;
    }

    return constrain(finalOutput, 0, 1023);
  }

  // Update Bite Point (Usa valore grezzo)
  void updateBitePoint(int rawValue, bool useLeftAsSource) 
  {
    // Se siamo in Reverse, il bite point si setta con la Master (che è la Destra)
    // Se siamo in Normal, si setta con la Sinistra.
    // Il firmware passerà il valore grezzo corretto, ma dobbiamo costringerlo nel range giusto.
    
    if (useLeftAsSource) {
       calibVals.btptValue = (float)constrain(rawValue, calibVals.lpMin, calibVals.lpMax);
    } else {
       calibVals.btptValue = (float)constrain(rawValue, calibVals.rpMin, calibVals.rpMax);
    }
  }

  void setLeftPaddleRange(uint16_t min, uint16_t max) { calibVals.lpMin = min; calibVals.lpMax = max; }
  void setRightPaddleRange(uint16_t min, uint16_t max) { calibVals.rpMin = min; calibVals.rpMax = max; }
  void setBitePointValue(float bp) { calibVals.btptValue = constrain(bp, 0.0f, 1023.0f); }
  float getBitePointValue() const { return calibVals.btptValue; }
};

#endif