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
            try {
                var harmony = new Harmony("com.blender.camera");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log("[BlenderCamera] Soul-Lock v1.14 Loaded: Isolated Scene Architecture");
            } catch (Exception e) {
                Debug.LogError("[BlenderCamera] Fatal Patching Error: " + e.Message);
            }
        }
    }

    // ============================================================
    // THE REDIRECTION GATES (Isolated Interceptors)
    // ============================================================
    
    // EDITOR INTERCEPTOR (VAB/SPH) - DO NOT TOUCH (PROVEN PERFECT)
    public static class EditorInputInterceptor
    {
        public static bool GetMouseButton(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButton(button); }
        public static bool GetMouseButtonDown(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButtonDown(button); }
        public static bool GetMouseButtonUp(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButtonUp(button); }
        public static bool GetKey(KeyCode key) { return key == KeyCode.Mouse2 ? false : UnityEngine.Input.GetKey(key); }
        public static bool GetKeyDown(KeyCode key) { return key == KeyCode.Mouse2 ? false : UnityEngine.Input.GetKeyDown(key); }
        public static float GetAxis(string axisName) { return axisName == "Mouse ScrollWheel" ? 0f : UnityEngine.Input.GetAxis(axisName); }
        public static float GetAxisRaw(string axisName) { return axisName == "Mouse ScrollWheel" ? 0f : UnityEngine.Input.GetAxisRaw(axisName); }
        public static Vector2 get_mouseScrollDelta() { return Vector2.zero; }
        public static float AxisBinding_GetAxis(AxisBinding binding) { return (binding == GameSettings.AXIS_MOUSEWHEEL) ? 0f : binding.GetAxis(); }
        public static float AxisBinding_GetScale(AxisBinding binding) { return (binding == GameSettings.AXIS_MOUSEWHEEL) ? 0f : binding.scale; }
    }

    // FLIGHT INTERCEPTOR (REWRITTEN FOR ISOLATION)
    public static class FlightInputInterceptor
    {
        public static bool BlockRMB = false;
        public static bool GetMouseButton(int button) {
            if (button == 2) return false;
            if (button == 1 && BlockRMB) return false;
            return UnityEngine.Input.GetMouseButton(button);
        }
        public static bool GetMouseButtonDown(int button) {
            if (button == 2) return false;
            if (button == 1 && BlockRMB) return false;
            return UnityEngine.Input.GetMouseButtonDown(button);
        }
        public static bool GetMouseButtonUp(int button) {
            if (button == 2) return false;
            if (button == 1 && BlockRMB) return false;
            return UnityEngine.Input.GetMouseButtonUp(button);
        }
        public static bool GetKey(KeyCode key) {
            if (key == KeyCode.Mouse2) return false;
            if (key == KeyCode.Mouse1 && BlockRMB) return false;
            return UnityEngine.Input.GetKey(key);
        }
        public static bool GetKeyDown(KeyCode key) {
            if (key == KeyCode.Mouse2) return false;
            if (key == KeyCode.Mouse1 && BlockRMB) return false;
            return UnityEngine.Input.GetKeyDown(key);
        }
        public static float GetAxis(string axisName) { return UnityEngine.Input.GetAxis(axisName); }
        public static float GetAxisRaw(string axisName) { return UnityEngine.Input.GetAxisRaw(axisName); }
        public static Vector2 get_mouseScrollDelta() { return UnityEngine.Input.mouseScrollDelta; }
        
        // Blocking AxisBindings in Flight to silence native FreeLook
        public static float AxisBinding_GetAxis(AxisBinding binding) {
            if (BlockRMB && (binding == GameSettings.AXIS_CAMERA_HDG || binding == GameSettings.AXIS_CAMERA_PITCH)) return 0f;
            return binding.GetAxis();
        }
        public static float AxisBinding_GetScale(AxisBinding binding) {
            if (BlockRMB && (binding == GameSettings.AXIS_CAMERA_HDG || binding == GameSettings.AXIS_CAMERA_PITCH)) return 0f;
            return binding.scale;
        }
    }

    // PLANETARIUM INTERCEPTOR (MAP VIEW)
    public static class PlanetariumInputInterceptor
    {
        public static bool GetMouseButton(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButton(button); }
        public static bool GetMouseButtonDown(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButtonDown(button); }
        public static bool GetMouseButtonUp(int button) { return button == 2 ? false : UnityEngine.Input.GetMouseButtonUp(button); }
        public static bool GetKey(KeyCode key) { return key == KeyCode.Mouse2 ? false : UnityEngine.Input.GetKey(key); }
        public static bool GetKeyDown(KeyCode key) { return key == KeyCode.Mouse2 ? false : UnityEngine.Input.GetKeyDown(key); }
        public static float GetAxis(string axisName) { return UnityEngine.Input.GetAxis(axisName); }
        public static float GetAxisRaw(string axisName) { return UnityEngine.Input.GetAxisRaw(axisName); }
        public static Vector2 get_mouseScrollDelta() { return UnityEngine.Input.mouseScrollDelta; }
        public static float AxisBinding_GetAxis(AxisBinding binding) { return binding.GetAxis(); }
        public static float AxisBinding_GetScale(AxisBinding binding) { return binding.scale; }
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
                ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * 0.05f;
                ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * 0.05f, ___minPitch, ___maxPitch);
            }
        }
        static Exception Finalizer(Exception __exception, ref float ___mouseZoomSensitivity, EditorLockState __state) { ___mouseZoomSensitivity = __state.originalZoomSens; return __exception; }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
    }

    [HarmonyPatch(typeof(GAPVesselCamera), "Update")]
    public static class GAPPatch {
        static void Prefix(ref float ___mouseZoomSensitivity, out EditorLockState __state) {
            __state = new EditorLockState { originalZoomSens = ___mouseZoomSensitivity, realScrollValue = GameSettings.AXIS_MOUSEWHEEL.GetAxis() };
            ___mouseZoomSensitivity = 0f;
        }
        static void Postfix(EditorLockState __state, ref float ___distance, ref float ___camHdg, ref float ___camPitch, float ___minDistance, float ___maxDistance, float ___minPitch, float ___maxPitch) {
            if (Mathf.Abs(__state.realScrollValue) > 0.001f) ___distance = Mathf.Clamp(___distance - (__state.realScrollValue * __state.originalZoomSens * 50f), ___minDistance, ___maxDistance);
            if (UnityEngine.Input.GetMouseButton(2)) {
                ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * 0.1f;
                ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * 0.1f, ___minPitch, ___maxPitch);
            }
        }
        static Exception Finalizer(Exception __exception, ref float ___mouseZoomSensitivity, EditorLockState __state) { ___mouseZoomSensitivity = __state.originalZoomSens; return __exception; }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
    }

    [HarmonyPatch(typeof(SPHCamera), "Update")]
    public static class SPHPatch {
        static void Prefix(ref float ___mouseZoomSensitivity, out EditorLockState __state) {
            __state = new EditorLockState { originalZoomSens = ___mouseZoomSensitivity, realScrollValue = GameSettings.AXIS_MOUSEWHEEL.GetAxis() };
            ___mouseZoomSensitivity = 0f;
        }
        static void Postfix(EditorLockState __state, ref float ___distance, ref float ___scrollHeight, ref float ___clampedScrollHeight, ref float ___camHdg, ref float ___camPitch, float ___minHeight, float ___maxHeight, float ___minDistance, float ___maxDistance, float ___minPitch, float ___maxPitch) {
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
                ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * 0.1f;
                ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * 0.1f, ___minPitch, ___maxPitch);
            }
        }
        static Exception Finalizer(Exception __exception, ref float ___mouseZoomSensitivity, EditorLockState __state) { ___mouseZoomSensitivity = __state.originalZoomSens; return __exception; }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
    }

    // FLIGHT: REWRITTEN - Patch both LateUpdate and UpdateCameraTransform
    [HarmonyPatch(typeof(FlightCamera))]
    public static class FlightPatch {
        [HarmonyPatch("LateUpdate")]
        [HarmonyPatch("UpdateCameraTransform")]
        [HarmonyPrefix]
        static void Prefix(ref float ___orbitSensitivity, out float __state) {
            __state = ___orbitSensitivity;
            FlightInputInterceptor.BlockRMB = (UnityEngine.Input.GetMouseButton(1) || UnityEngine.Input.GetMouseButton(2));
            if (FlightInputInterceptor.BlockRMB) ___orbitSensitivity = 0f;
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPatch("UpdateCameraTransform")]
        [HarmonyPostfix]
        static void Postfix(float __state, ref float ___camHdg, ref float ___camPitch, ref float ___offsetHdg, ref float ___offsetPitch) {
            if (UnityEngine.Input.GetMouseButton(2)) {
                ___camHdg += UnityEngine.Input.GetAxis("Mouse X") * (__state * 1f);
                ___camPitch = Mathf.Clamp(___camPitch - UnityEngine.Input.GetAxis("Mouse Y") * (__state * 1f), -89f, 89f);
            }
            if (UnityEngine.Input.GetMouseButton(1)) {
                ___offsetHdg += UnityEngine.Input.GetAxis("Mouse X") * (__state * 0.5f);
                ___offsetPitch = Mathf.Clamp(___offsetPitch - UnityEngine.Input.GetAxis("Mouse Y") * (__state * 0.5f), -89f, 89f);
            }
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPatch("UpdateCameraTransform")]
        [HarmonyFinalizer]
        static Exception Finalizer(Exception __exception, ref float ___orbitSensitivity, float __state) { ___orbitSensitivity = __state; return __exception; }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPatch("UpdateCameraTransform")]
        [HarmonyTranspiler] 
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(FlightInputInterceptor)); }
    }

    [HarmonyPatch(typeof(PlanetariumCamera), "LateUpdate")]
    public static class PlanetariumPatch {
        static void Postfix(ref float ___camHdg, ref float ___targetHeading, ref float ___camPitch, float ___distance) {
            if (UnityEngine.Input.GetMouseButton(2)) {
                float sens = 0.1f;
                if (___distance > 0.1f) sens *= Mathf.Clamp(Mathf.Log10(___distance) * 0.2f, 0.4f, 2.0f);
                float h = ___camHdg + (UnityEngine.Input.GetAxis("Mouse X") * sens);
                ___camHdg = h; ___targetHeading = h;
                ___camPitch = Mathf.Clamp(___camPitch - (UnityEngine.Input.GetAxis("Mouse Y") * sens), -89f, 89f);
            }
        }
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(PlanetariumInputInterceptor)); }
    }

    [HarmonyPatch(typeof(EditorLogic), "LateUpdate")]
    public static class EditorLogicLatePatch {
        [HarmonyTranspiler] static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
    }

    public static class PatchHelper {
        public static IEnumerable<CodeInstruction> DoTranspiler(IEnumerable<CodeInstruction> instructions, Type interceptorType) {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++) {
                var inst = codes[i];
                if (inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt) {
                    var method = inst.operand as MethodBase;
                    if (method == null) continue;
                    if (method.DeclaringType == typeof(UnityEngine.Input)) {
                        if (method.Name == "GetAxis" || method.Name == "GetAxisRaw" || method.Name == "GetMouseButton" || method.Name == "GetMouseButtonDown" || method.Name == "GetMouseButtonUp" || method.Name == "GetKey" || method.Name == "GetKeyDown" || method.Name == "get_mouseScrollDelta") {
                            inst.opcode = OpCodes.Call; inst.operand = AccessTools.Method(interceptorType, method.Name);
                        }
                    } else if (method.DeclaringType == typeof(AxisBinding) && (method.Name == "GetAxis" || method.Name == "get_scale")) {
                        string target = method.Name == "GetAxis" ? "AxisBinding_GetAxis" : "AxisBinding_GetScale";
                        MethodInfo replacement = AccessTools.Method(interceptorType, target);
                        if (replacement != null) { inst.opcode = OpCodes.Call; inst.operand = replacement; }
                    }
                }
            }
            return codes;
        }
    }
}
