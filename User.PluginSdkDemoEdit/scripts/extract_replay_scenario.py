#!/usr/bin/env python3
# -------------------------------------------------------------------------
# SimHub Replay Scenario Extractor
# Extracts a compact telemetry window around pit stops for automated testing
# -------------------------------------------------------------------------

import os
import sys
import json
import zlib
import time

def extract_scenario(replay_path, output_dir, pre_laps=3, post_laps=3):
    print(f"[*] Opening SimHub Replay: {replay_path}")
    if not os.path.exists(replay_path):
        print(f"[!] File not found: {replay_path}")
        return False

    metadata_path = replay_path + ".metadata"
    metadata = {}
    if os.path.exists(metadata_path):
        try:
            with open(metadata_path, 'r', encoding='utf-8', errors='ignore') as mf:
                metadata = json.load(mf)
                print(f"[*] Track: {metadata.get('TrackName')}, Car: {metadata.get('CarModel')}")
        except Exception as e:
            print(f"[!] Metadata error: {e}")

    file_size_mb = os.path.getsize(replay_path) / (1024 * 1024)
    print(f"[*] File size: {file_size_mb:.1f} MB. Decompressing frame blocks...")

    frames = []
    pit_entry_frames = []

    with open(replay_path, 'rb') as f:
        f.seek(10000) # Skip header
        data = f.read()

    pos = 0
    data_len = len(data)
    last_pit_state = False

    while pos < data_len - 10:
        idx = data.find(b'\xed\x3d', pos)
        if idx == -1:
            break

        try:
            decomp = zlib.decompress(data[idx:], -15)
            text = decomp.decode('utf-8', errors='ignore')
            
            # Parse JSON frame block
            try:
                frame = json.loads(text)
                frames.append(frame)
                
                # Check for pit entry event
                is_in_pit = False
                if 'NewData' in frame and frame['NewData'] is not None:
                    nd = frame['NewData']
                    is_in_pit = bool(nd.get('IsInPit', 0) or nd.get('IsInPitLane', 0))
                    
                if is_in_pit and not last_pit_state:
                    pit_entry_frames.append(len(frames) - 1)
                last_pit_state = is_in_pit

            except Exception:
                pass

            pos = idx + max(50, len(decomp) // 2)
        except Exception:
            pos = idx + 2

    print(f"[*] Extracted {len(frames)} total telemetry frames. Found {len(pit_entry_frames)} pit entry events.")

    os.makedirs(output_dir, exist_ok=True)

    if not pit_entry_frames:
        print("[!] No pit stops detected in this replay. Exporting a 200-frame sample...")
        sample_frames = frames[:min(200, len(frames))]
        scenario_data = {
            "Metadata": metadata,
            "TotalExtractedFrames": len(sample_frames),
            "PitStopIndex": -1,
            "Frames": sample_frames
        }
        out_name = "Scenario_Sample.json"
        out_path = os.path.join(output_dir, out_name)
        with open(out_path, 'w', encoding='utf-8') as out_f:
            json.dump(scenario_data, out_f, indent=2)
        print(f"[+] Exported sample scenario: {out_path} ({os.path.getsize(out_path)/1024:.1f} KB)")
        return True

    for i, pit_idx in enumerate(pit_entry_frames):
        start_idx = max(0, pit_idx - 300)
        end_idx = min(len(frames), pit_idx + 300)
        scenario_frames = frames[start_idx:end_idx]

        track_name = str(metadata.get('TrackName', 'UnknownTrack')).replace(' ', '_')
        car_model = str(metadata.get('CarModel', 'UnknownCar')).replace(' ', '_')
        out_name = f"Scenario_{track_name}_{car_model}_Pit{i+1}.json"
        out_path = os.path.join(output_dir, out_name)

        scenario_data = {
            "Metadata": metadata,
            "PitStopEventFrameIndex": pit_idx - start_idx,
            "TotalExtractedFrames": len(scenario_frames),
            "Frames": scenario_frames
        }

        with open(out_path, 'w', encoding='utf-8') as out_f:
            json.dump(scenario_data, out_f, indent=2)

        out_mb = os.path.getsize(out_path) / (1024 * 1024)
        print(f"[+] Exported Benchmark Scenario #{i+1}: {out_name} ({out_mb:.2f} MB)")

    return True

if __name__ == '__main__':
    replay_file = r"E:\SimHub\Replays\IRacing\20260303_151035.telemetry.json"
    out_directory = r"c:\Users\Andreas\Desktop\The Wheel Project\Antigravity2.0\User.PluginSdkDemoEdit\User.PluginSdkDemo.Tests\Scenarios"
    
    if len(sys.argv) > 1:
        replay_file = sys.argv[1]
    if len(sys.argv) > 2:
        out_directory = sys.argv[2]

    extract_scenario(replay_file, out_directory)
