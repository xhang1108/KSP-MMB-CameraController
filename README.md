# KSP-MMB-CameraController

A patch for Kerbal Space Program (KSP) designed to provide a Blender-like camera experience, specifically optimized for Flight, VAB, and SPH modes.

## Controls

### Flight Mode
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse (Drag)** | FreeLook / Offset | **Orbit** |
| **Right Mouse Button (RMB)** | Orbit | **FreeLook / Offset** |

### VAB / SPH (Editor)
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse (Drag)** | Zoom | **Orbit** |
| **Scroll Wheel** | Vertical Pan | **Zoom** |
| **Shift + Scroll Wheel** | Zoom | **Vertical Pan** |

## Installation

1.  Ensure you have [Harmony](https://github.com/pardeike/Harmony) installed.
2.  Place the compiled `BlenderCameraControls.dll` into any subfolder within KSP's `GameData` directory.

## Development & Building

To compile this project from source, ensure you have the following environment:

1.  **.NET Framework 4.7.2**
2.  **PowerShell 7 (pwsh)** or PowerShell 5
3.  **KSP Core DLLs** (placed in the `GameFile` subfolder):
    *   `UnityEngine.dll`
    *   `Assembly-CSharp.dll`
    *   `0Harmony.dll`

### Build Steps

Run the following command in the project root:

```powershell
./build.ps1
```
