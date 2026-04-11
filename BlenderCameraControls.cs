using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace BlenderCamera
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class BlenderCameraMod : MonoBehaviour
    {
        void Awake()
        {
            var harmony = new Harmony("com.blender.camera");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // ============================================================
    // THE GREEN CHANNEL (Unified Monolithic Interceptor)
    // ============================================================
    public static class GlobalInputInterceptor 
    {
        public static bool IsInternalCall = false;

        public static bool GetMouseButton(int button) {
            if (IsInternalCall) return UnityEngine.Input.GetMouseButton(button);
            
            // EDITOR BLOCKING
            if (HighLogic.LoadedSceneIsEditor) {
                if (button == 2) return false;
                if (button == 1 && EditorDriver.editorFacility == EditorFacility.SPH) return false;
            }
            
            // FLIGHT BLOCKING
            if (HighLogic.LoadedSceneIsFlight) {
                if (UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2)) {
                    if (button != 0) return false;
                }
            }

            // KSC BLOCKING
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER) {
                if (button == 1 || button == 2) return false;
            }

            // TRACKING BLOCKING
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION && button == 2) return false;

            return UnityEngine.Input.GetMouseButton(button);
        }

        public static bool GetMouseButtonDown(int button) {
            return IsInternalCall ? UnityEngine.Input.GetMouseButtonDown(button) : GetMouseButton(button);
        }
        public static bool GetMouseButtonUp(int button) {
            return IsInternalCall ? UnityEngine.Input.GetMouseButtonUp(button) : GetMouseButton(button);
        }

        public static float GetAxis(string axisName) {
            if (IsInternalCall) return UnityEngine.Input.GetAxis(axisName);
            
            if (HighLogic.LoadedSceneIsEditor) {
                if (axisName == "Mouse ScrollWheel") return 0f;
                if (EditorDriver.editorFacility == EditorFacility.SPH && (axisName == "Mouse X" || axisName == "Mouse Y")) {
                    if (UnityEngine.Input.GetMouseButton(1)) return 0f;
                }
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER) {
                if (axisName == "Mouse ScrollWheel") return 0f;
                if (axisName == "Mouse X" || axisName == "Mouse Y") {
                    if (UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2)) return 0f;
                }
            }

            return UnityEngine.Input.GetAxis(axisName);
        }

        public static float GetAxisRaw(string axisName) {
            return IsInternalCall ? UnityEngine.Input.GetAxisRaw(axisName) : GetAxis(axisName);
        }

        public static float AxisBinding_GetAxis(AxisBinding binding) {
            if (IsInternalCall) return binding.GetAxis();
            if (HighLogic.LoadedSceneIsEditor && binding == GameSettings.AXIS_MOUSEWHEEL) return 0f;
            
            if (HighLogic.LoadedSceneIsFlight) {
                if (UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2)) {
                    if (binding == GameSettings.AXIS_CAMERA_HDG || binding == GameSettings.AXIS_CAMERA_PITCH) return 0f;
                }
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER) {
                if (binding == GameSettings.AXIS_MOUSEWHEEL) return 0f;
            }

            return binding.GetAxis();
        }

        public static float AxisBinding_get_scale(AxisBinding binding) {
            if (IsInternalCall) return binding.scale;
            if (HighLogic.LoadedSceneIsEditor && binding == GameSettings.AXIS_MOUSEWHEEL) return 0f;
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && binding == GameSettings.AXIS_MOUSEWHEEL) return 0f;
            return binding.scale;
        }

        public static bool GetKey(KeyCode key) {
            if (IsInternalCall) return UnityEngine.Input.GetKey(key);
            
            if (HighLogic.LoadedSceneIsEditor) {
                if (key == KeyCode.Mouse2) return false;
                if (key == KeyCode.Mouse1 && EditorDriver.editorFacility == EditorFacility.SPH) return false;
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER) {
                if (key == KeyCode.Mouse1 || key == KeyCode.Mouse2) return false;
            }

            return UnityEngine.Input.GetKey(key);
        }
        public static bool GetKeyDown(KeyCode key) {
            return IsInternalCall ? UnityEngine.Input.GetKeyDown(key) : GetKey(key);
        }
        public static Vector2 get_mouseScrollDelta() {
            return IsInternalCall ? UnityEngine.Input.mouseScrollDelta : Vector2.zero;
        }
    }

    // ============================================================
    // COMPONENT HANDLERS
    // ============================================================
    public struct EditorLockState { public float originalZoomSens; public float realScrollValue; }

    [HarmonyPatch(typeof(VABCamera), "Update")]
    public static class VABPatch {
        static void Prefix(ref float ___mouseZoomSensitivity, out EditorLockState __state) {
            __state = new EditorLockState { originalZoomSens = ___mouseZoomSensitivity, realScrollValue = GameSettings.AXIS_MOUSEWHEEL.GetAxis() };
            ___mouseZoomSensitivity = 0f;
        }
        static void Postfix(EditorLockState __state, ref float ___distance, ref float ___scrollHeight, ref float ___clampedScrollHeight, ref float ___camHdg, ref float ___camPitch, float ___minHeight, float ___maxHeight, float ___minDistance, float ___maxDistance, float ___minPitch, float ___maxPitch) {
            GlobalInputInterceptor.IsInternalCall = true;
            try {
                float scroll = __state.realScrollValue;
                if (Mathf.Abs(scroll) > 0.001f) {
                    if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                        float newH = Mathf.Clamp(___scrollHeight + (scroll * 10f), ___minHeight, ___maxHeight);
                        ___scrollHeight = newH; ___clampedScrollHeight = newH;
                    } else {
                        ___distance = Mathf.Clamp(___distance - (scroll * __state.originalZoomSens * 50f), ___minDistance, ___maxDistance);
                    }
                }
                if (UnityEngine.Input.GetMouseButton(2)) {
                    if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                        float my = UnityEngine.Input.GetAxis("Mouse Y");
                        float newH = Mathf.Clamp(___scrollHeight + (my * ___distance * 0.05f), ___minHeight, ___maxHeight);
                        ___scrollHeight = newH; ___clampedScrollHeight = newH;
                    } else {
                        ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * 0.05f;
                        ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * 0.05f, ___minPitch, ___maxPitch);
                    }
                }
            } finally { GlobalInputInterceptor.IsInternalCall = false; }
        }
        static Exception Finalizer(Exception __exception, ref float ___mouseZoomSensitivity, EditorLockState __state) { 
            GlobalInputInterceptor.IsInternalCall = false;
            ___mouseZoomSensitivity = __state.originalZoomSens; 
            return __exception; 
        }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions); }
    }

    [HarmonyPatch(typeof(SPHCamera), "Update")]
    public static class SPHPatch {
        static void Prefix(ref float ___mouseZoomSensitivity, out EditorLockState __state) {
            __state = new EditorLockState { originalZoomSens = ___mouseZoomSensitivity, realScrollValue = GameSettings.AXIS_MOUSEWHEEL.GetAxis() };
            ___mouseZoomSensitivity = 0f;
        }
        static void Postfix(EditorLockState __state, ref float ___distance, ref float ___scrollHeight, ref float ___clampedScrollHeight, ref float ___camHdg, ref float ___camPitch, ref Vector3 ___endPos, ref Vector3 ___offset, ref GameObject ___pivot, float ___minHeight, float ___maxHeight, float ___minDistance, float ___maxDistance, float ___minPitch, float ___maxPitch) {
            GlobalInputInterceptor.IsInternalCall = true;
            try {
                float scroll = __state.realScrollValue;
                if (Mathf.Abs(scroll) > 0.001f) {
                    if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                        float newH = Mathf.Clamp(___scrollHeight + (scroll * 10f), ___minHeight, ___maxHeight);
                        ___scrollHeight = newH; ___clampedScrollHeight = newH;
                    } else {
                        ___distance = Mathf.Clamp(___distance - (scroll * __state.originalZoomSens * 150f), ___minDistance, ___maxDistance);
                    }
                }

                // PAN LOGIC (SPH Mapping Fix)
                float s = ___distance * 0.02f;
                Quaternion camRot = Quaternion.Euler(0, ___camHdg, 0);
                Vector3 forward = camRot * Vector3.forward;
                Vector3 right = camRot * Vector3.right;

                if (UnityEngine.Input.GetMouseButton(1)) {
                    float mx = UnityEngine.Input.GetAxis("Mouse X");
                    float my = UnityEngine.Input.GetAxis("Mouse Y");
                    ___endPos -= (forward * mx * s) + (right * (-my) * s);
                    if (___pivot != null) ___pivot.transform.position = ___endPos;
                }
                else if (UnityEngine.Input.GetMouseButton(2) && UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                    float mx = UnityEngine.Input.GetAxis("Mouse X");
                    float my = UnityEngine.Input.GetAxis("Mouse Y");
                    ___endPos += (forward * mx * s) + (right * (-my) * s);
                    if (___pivot != null) ___pivot.transform.position = ___endPos;
                }

                // ORBIT LOGIC
                if (UnityEngine.Input.GetMouseButton(2) && !UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                    ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * 0.05f;
                    ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * 0.05f, ___minPitch, ___maxPitch);
                }
                ___offset = Vector3.zero;
            } finally { GlobalInputInterceptor.IsInternalCall = false; }
        }
        static Exception Finalizer(Exception __exception, ref float ___mouseZoomSensitivity, EditorLockState __state) { 
            GlobalInputInterceptor.IsInternalCall = false;
            ___mouseZoomSensitivity = __state.originalZoomSens; 
            return __exception; 
        }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions); }
    }

    [HarmonyPatch(typeof(FlightCamera), "LateUpdate")]
    public static class FlightLateUpdatePatch {
        static void Prefix(ref float ___orbitSensitivity, out float __state) {
            __state = ___orbitSensitivity;
            if (UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2)) ___orbitSensitivity = 0f;
        }
        static void Postfix(float __state, ref float ___camHdg, ref float ___camPitch, ref float ___offsetHdg, ref float ___offsetPitch) {
            GlobalInputInterceptor.IsInternalCall = true;
            try {
                if (UnityEngine.Input.GetMouseButton(2)) {
                    ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * (__state * 1f);
                    ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * (__state * 1f), -89f, 89f);
                }
                if (UnityEngine.Input.GetMouseButton(1)) {
                    ___offsetHdg += UnityEngine.Input.GetAxis("Mouse X") * (__state * 0.5f);
                    ___offsetPitch = Mathf.Clamp(___offsetPitch - UnityEngine.Input.GetAxis("Mouse Y") * (__state * 0.5f), -89f, 89f);
                }
            } finally { GlobalInputInterceptor.IsInternalCall = false; }
        }
        static Exception Finalizer(Exception __exception, ref float ___orbitSensitivity, float __state) { 
            GlobalInputInterceptor.IsInternalCall = false; 
            ___orbitSensitivity = __state; 
            return __exception; 
        }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions); }
    }

    [HarmonyPatch(typeof(PlanetariumCamera), "LateUpdate")]
    public static class PlanetariumPatch {
        static void Postfix(ref float ___camHdg, ref float ___targetHeading, ref float ___camPitch, float ___distance) {
            GlobalInputInterceptor.IsInternalCall = true;
            try {
                if (UnityEngine.Input.GetMouseButton(2)) {
                    float sens = 0.1f;
                    if (___distance > 0.1f) sens *= Mathf.Clamp(Mathf.Log10(___distance) * 0.2f, 0.4f, 2.0f);
                    float h = ___camHdg + (UnityEngine.Input.GetAxis("Mouse X") * sens);
                    ___camHdg = h; ___targetHeading = h;
                    ___camPitch = Mathf.Clamp(___camPitch - (UnityEngine.Input.GetAxis("Mouse Y") * sens), -89f, 89f);
                }
            } finally { GlobalInputInterceptor.IsInternalCall = false; }
        }
        static Exception Finalizer(Exception __exception) { GlobalInputInterceptor.IsInternalCall = false; return __exception; }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions); }
    }

    // ============================================================
    // KSC CHAPTER (SpaceCenterCamera2)
    // ============================================================
    [HarmonyPatch(typeof(SpaceCenterCamera2), "Update")]
    public static class KSCPatch {
        static void Postfix(ref float ___rotationAngle, ref float ___elevationAngle, ref Vector3 ___pos, ref float ___zoom, float ___elevationMin, float ___elevationMax, float ___zoomMin, float ___zoomMax) {
            GlobalInputInterceptor.IsInternalCall = true;
            try {
                // ZOOM LOGIC (Recovering what we silenced in Interceptor)
                float scroll = GameSettings.AXIS_MOUSEWHEEL.GetAxis();
                if (Mathf.Abs(scroll) > 0.001f) {
                    ___zoom = Mathf.Clamp(___zoom - (scroll * 400f), ___zoomMin, ___zoomMax);
                }

                // PAN LOGIC (Lowered sensitivity from 0.15 -> 0.03)
                bool isPanning = UnityEngine.Input.GetMouseButton(1) || (UnityEngine.Input.GetMouseButton(2) && UnityEngine.Input.GetKey(KeyCode.LeftShift));
                if (isPanning) {
                    float mx = UnityEngine.Input.GetAxis("Mouse X");
                    float my = UnityEngine.Input.GetAxis("Mouse Y");
                    float s = ___zoom * 0.03f; 
                    Quaternion camRot = Quaternion.Euler(0, ___rotationAngle, 0);
                    Vector3 forward = camRot * Vector3.forward;
                    Vector3 right = camRot * Vector3.right;
                    ___pos -= (right * mx * s) + (forward * my * s);
                }

                // ORBIT LOGIC (MMB only)
                if (UnityEngine.Input.GetMouseButton(2) && !UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                    ___rotationAngle += UnityEngine.Input.GetAxis("Mouse X") * 2f;
                    ___elevationAngle = Mathf.Clamp(___elevationAngle - UnityEngine.Input.GetAxis("Mouse Y") * 2f, ___elevationMin, ___elevationMax);
                }
            } finally { GlobalInputInterceptor.IsInternalCall = false; }
        }
        static Exception Finalizer(Exception __exception) { GlobalInputInterceptor.IsInternalCall = false; return __exception; }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); }
    }

    [HarmonyPatch(typeof(SpaceCenterCamera2), "InputCamera")] public static class KSCInputCameraPatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }
    [HarmonyPatch(typeof(SpaceCenterCamera2), "InputMovement")] public static class KSCInputMovementPatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }

    // --- DEEP INTERCEPTION ---
    [HarmonyPatch(typeof(EditorLogic), "Update")] public static class EditorLogicUpdatePatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }
    [HarmonyPatch(typeof(EditorLogic), "LateUpdate")] public static class EditorLogicLatePatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }
    [HarmonyPatch(typeof(SPHCamera), "UpdateCamera")] public static class SPHUpdateCameraPatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }
    [HarmonyPatch(typeof(VABCamera), "UpdateCamera")] public static class VABUpdateCameraPatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }
    [HarmonyPatch(typeof(CameraMouseLook), "GetMouseLook")] public static class CameraMouseLookPatch { [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i) { return PatchHelper.DoTranspiler(i); } }

    public static class PatchHelper {
        public static IEnumerable<CodeInstruction> DoTranspiler(IEnumerable<CodeInstruction> instructions) {
            var codes = new List<CodeInstruction>(instructions);
            var interceptor = typeof(GlobalInputInterceptor);
            for (int i = 0; i < codes.Count; i++) {
                var inst = codes[i];
                if (inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt) {
                    var method = inst.operand as MethodBase;
                    if (method == null) continue;
                    if (method.DeclaringType == typeof(UnityEngine.Input)) {
                        MethodInfo replacement = AccessTools.Method(interceptor, method.Name);
                        if (replacement != null) { inst.opcode = OpCodes.Call; inst.operand = replacement; }
                    }
                    else if (method.DeclaringType == typeof(AxisBinding)) {
                        string target = (method.Name == "GetAxis") ? "AxisBinding_GetAxis" : (method.Name == "get_scale" ? "AxisBinding_get_scale" : null);
                        if (target != null) {
                            MethodInfo replacement = AccessTools.Method(interceptor, target);
                            if (replacement != null) { inst.opcode = OpCodes.Call; inst.operand = replacement; }
                        }
                    }
                }
            }
            return codes;
        }
    }
}
