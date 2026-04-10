# KSP-MMB-CameraController

A patch for Kerbal Space Program (KSP) designed to provide a Blender-like camera experience, using the Middle Mouse Button (MMB) for orbiting. 

## Controls

| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse Button (MMB) Drag** | Zoom (VAB/SPH) / Offset (Flight) | **Orbit View** |

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

This will automatically generate `BlenderCameraControls.dll` in the root directory.

