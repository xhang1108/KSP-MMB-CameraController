# KSP-MMB-CameraController

A patch for Kerbal Space Program (KSP) designed to provide a Blender-like camera experience, specifically optimized for Flight, VAB, and SPH modes.

## Controls

### Flight Mode
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse (Drag)** | Offset | **Orbit** |
| **Right Mouse Button (RMB)** | Orbit | **Offset** |

### VAB (Editors)
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse (Drag)** | Zoom | **Orbit** |
| **Scroll Wheel** | Vertical Pan | **Zoom** |
| **Shift + Scroll Wheel** | Zoom | **Vertical Pan** |
| **Shift + MMB** | None | **Vertical Pan** |

### Planetarium (Map View)
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse (Drag)** | None | **Orbit (Adaptive)** |

### SPH
| Action | Original Function | New Function |
| :--- | :--- | :--- |
| **Middle Mouse** | Pan | **Orbit** |
| **Shift + MMB** | None | **Pan** |
| **Right Mouse** | Orbit | **Pan** |

### KSC (Kerbal Space Center)
| Action | Original Function | New Function (Blender Style) |
| :--- | :--- | :--- |
| **Middle Mouse** | Pan | **Orbit** |
| **Right Mouse** | Orbit | **Pan** |
| **Shift + MMB** | None | **Pan** |

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
