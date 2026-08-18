import sys
import subprocess
import time
import os

# Install pyserial if missing
try:
    import serial
    import serial.tools.list_ports
except ImportError:
    print("pyserial is missing. Installing...")
    subprocess.run([sys.executable, "-m", "pip", "install", "pyserial"], check=True)
    import serial
    import serial.tools.list_ports

ARDUINO_CLI = r"C:\Users\Andreas\AppData\Local\Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe"
SKETCH_DIR = os.path.dirname(os.path.abspath(__file__))
SKETCH_PATH = os.path.join(SKETCH_DIR, "V2_8_2")

def find_simrig_input_port():
    print("Scanning COM ports...")
    ports = serial.tools.list_ports.comports()
    
    candidate_ports = []
    for p in ports:
        if "COM" in p.device:
            candidate_ports.append(p.device)
            
    print(f"Found candidate ports: {candidate_ports}")
    
    for port in candidate_ports:
        print(f"Testing port {port}...")
        try:
            # Open port at 115200 baud with a 1.0 second timeout
            ser = serial.Serial(port, 115200, timeout=1.0)
            
            # Wait for Arduino Leonardo bootloader to reset (2 seconds)
            time.sleep(2.0)
            
            ser.reset_input_buffer()
            ser.write(b"WHO\n")
            ser.flush()
            
            response = ser.readline().decode('utf-8', errors='ignore').strip()
            print(f"Response from {port}: '{response}'")
            
            if "ID:SIMRIG_INPUT" in response:
                ser.close()
                return port
            
            ser.close()
        except serial.SerialException as e:
            print(f"Could not open {port}: {e}")
            print("Note: If SimHub is running, it will lock the port. Please close SimHub and try again.")
        except Exception as e:
            print(f"Error testing {port}: {e}")
            
    return None

def main():
    if not os.path.exists(ARDUINO_CLI):
        print(f"Error: arduino-cli not found at {ARDUINO_CLI}")
        return

    input_port = find_simrig_input_port()
    if not input_port:
        print("\nError: Could not find SIMRIG_INPUT chip. Make sure:")
        print("1. The wheel is plugged in.")
        print("2. SimHub is completely CLOSED.")
        return

    print(f"\nTarget port identified: {input_port}")
    
    # 1. Compile
    print("\n--- COMPILING SKETCH ---")
    compile_cmd = [
        ARDUINO_CLI, "compile",
        "--fqbn", "arduino:avr:leonardo",
        SKETCH_PATH
    ]
    res_compile = subprocess.run(compile_cmd)
    if res_compile.returncode != 0:
        print("Compilation failed!")
        return
    print("Compilation successful!")

    # 2. Upload
    print("\n--- UPLOADING TO BOARD ---")
    upload_cmd = [
        ARDUINO_CLI, "upload",
        "-p", input_port,
        "--fqbn", "arduino:avr:leonardo",
        SKETCH_PATH
    ]
    res_upload = subprocess.run(upload_cmd)
    if res_upload.returncode != 0:
        print("Upload failed!")
        return
    print("\nUpload completed successfully! You can now restart SimHub.")

if __name__ == "__main__":
    main()
