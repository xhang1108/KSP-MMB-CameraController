using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[KSPAddon(KSPAddon.Startup.Instantly, true)]
public class BlenderCameraMod : MonoBehaviour
{
    private static bool initialized = false;
    void Awake()
    {
        if (initialized) return;
        DontDestroyOnLoad(this);
        new Harmony("com.antigravity.blendercamera").PatchAll();
        initialized = true;
        Debug.Log("[BlenderCamera] Harmony patches applied.");
    }
}



// ==================== Input Interceptors ====================

// Flight: blocks MMB + RMB from native code
public static class FlightInputInterceptor
{
    public static bool BlockRMB = false;

    public static bool GetMouseButton(int button)
    {
        if (button == 2) return false;
        if (button == 1 && BlockRMB) return false;
        return UnityEngine.Input.GetMouseButton(button);
    }
    public static bool GetMouseButtonDown(int button)
    {
        if (button == 2) return false;
        if (button == 1 && BlockRMB) return false;
        return UnityEngine.Input.GetMouseButtonDown(button);
    }
    public static bool GetMouseButtonUp(int button)
    {
        if (button == 2) return false;
        if (button == 1 && BlockRMB) return false;
        return UnityEngine.Input.GetMouseButtonUp(button);
    }
    public static bool GetKey(KeyCode key)
    {
        if (key == KeyCode.Mouse2) return false;
        if (key == KeyCode.Mouse1 && BlockRMB) return false;
        return UnityEngine.Input.GetKey(key);
    }
    public static float GetAxis(string axisName) { return UnityEngine.Input.GetAxis(axisName); }
    // Property interception: Input.mouseScrollDelta compiles to get_mouseScrollDelta()
    public static Vector2 get_mouseScrollDelta() { return UnityEngine.Input.mouseScrollDelta; }
}

// Editor: blocks MMB + ScrollWheel from native code
public static class EditorInputInterceptor
{
    public static bool GetMouseButton(int button)
    {
        if (button == 2) return false; // Block MMB
        return UnityEngine.Input.GetMouseButton(button);
    }
    public static bool GetMouseButtonDown(int button)
    {
        if (button == 2) return false;
        return UnityEngine.Input.GetMouseButtonDown(button);
    }
    public static bool GetMouseButtonUp(int button)
    {
        if (button == 2) return false;
        return UnityEngine.Input.GetMouseButtonUp(button);
    }
    public static bool GetKey(KeyCode key)
    {
        if (key == KeyCode.Mouse2) return false;
        return UnityEngine.Input.GetKey(key);
    }
    public static float GetAxis(string axisName)
    {
        if (axisName == "Mouse ScrollWheel") return 0f;
        return UnityEngine.Input.GetAxis(axisName);
    }
    public static Vector2 get_mouseScrollDelta() { return Vector2.zero; }

    // KEY FIX: Intercept AxisBinding.GetAxis() (Callvirt, instance method)
    // Native code does: GameSettings.AXIS_MOUSEWHEEL.GetAxis() via Callvirt
    // We replace it with this static method that consumes the AxisBinding from the stack
    public static float AxisBinding_GetAxis(AxisBinding binding)
    {
        // Block AXIS_MOUSEWHEEL, pass through everything else
        if (binding == GameSettings.AXIS_MOUSEWHEEL) return 0f;
        return binding.GetAxis();
    }
}

// ==================== State & Core Logic ====================

public struct CamState 
{ 
    public float h, p;
    public float oh, op;
    public float s;
    public float d;
    public float sh, csh;
    public Vector3? o;
    // Editor: real scroll value from KSP's input system (before we zero it)
    public float scrollAxis;
}

public static class PatchHelper
{
    public static void DoPrefix(object instance, out CamState state, bool isFlight)
    {
        var t = Traverse.Create(instance);
        float currentS = t.Field("orbitSensitivity").FieldExists() ? t.Field("orbitSensitivity").GetValue<float>() : 1f;
        state = new CamState {
            h = t.Field("camHdg").FieldExists() ? t.Field("camHdg").GetValue<float>() : 0f,
            p = t.Field("camPitch").FieldExists() ? t.Field("camPitch").GetValue<float>() : 0f,
            oh = t.Field("offsetHdg").FieldExists() ? t.Field("offsetHdg").GetValue<float>() : 0f,
            op = t.Field("offsetPitch").FieldExists() ? t.Field("offsetPitch").GetValue<float>() : 0f,
            s = currentS,
            d = t.Field("distance").FieldExists() ? t.Field("distance").GetValue<float>() : 0f,
            sh = t.Field("scrollHeight").FieldExists() ? t.Field("scrollHeight").GetValue<float>() : 0f,
            csh = t.Field("clampedScrollHeight").FieldExists() ? t.Field("clampedScrollHeight").GetValue<float>() : 0f,
            o = t.Field("offset").FieldExists() ? t.Field("offset").GetValue<Vector3>() : (Vector3?)null,
            scrollAxis = 0f
        };

        // [Flight] Paralyze native orbit when RMB is held
        if (isFlight && UnityEngine.Input.GetMouseButton(1))
        {
            if (t.Field("orbitSensitivity").FieldExists()) t.Field("orbitSensitivity").SetValue(0f);
        }

        // [Editor] Save real scroll value (Transpiler will block native reading)
        if (!isFlight)
        {
            state.scrollAxis = GameSettings.AXIS_MOUSEWHEEL.GetAxis();
        }
    }

    public static void DoPostfix(object instance, CamState state, bool isFlight)
    {
        var t = Traverse.Create(instance);
        // Restore sensitivity after native code ran
        if (t.Field("orbitSensitivity").FieldExists()) t.Field("orbitSensitivity").SetValue(state.s);

        float mouseX = UnityEngine.Input.GetAxis("Mouse X");
        float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

        // ---- MMB -> Blender Orbit ----
        if (UnityEngine.Input.GetMouseButton(2))
        {
            if (t.Field("camHdg").FieldExists()) t.Field("camHdg").SetValue(state.h + mouseX * state.s);
            if (t.Field("camPitch").FieldExists()) t.Field("camPitch").SetValue(Mathf.Clamp(state.p - mouseY * state.s, 
                t.Field("minPitch").FieldExists() ? t.Field("minPitch").GetValue<float>() : -90f, 
                t.Field("maxPitch").FieldExists() ? t.Field("maxPitch").GetValue<float>() : 90f));
            if (state.o.HasValue && t.Field("offset").FieldExists()) t.Field("offset").SetValue(state.o.Value);
        }

        // ---- [Flight] RMB -> FreeLook ----
        if (isFlight && UnityEngine.Input.GetMouseButton(1))
        {
            if (t.Field("camHdg").FieldExists()) t.Field("camHdg").SetValue(state.h);
            if (t.Field("camPitch").FieldExists()) t.Field("camPitch").SetValue(state.p);
            if (t.Field("offsetHdg").FieldExists()) t.Field("offsetHdg").SetValue(state.oh + mouseX * state.s);
            if (t.Field("offsetPitch").FieldExists()) t.Field("offsetPitch").SetValue(state.op - mouseY * state.s);
        }

        // ---- [Editor] Scroll Swap using KSP's own scroll value ----
        if (!isFlight)
        {
            float scroll = state.scrollAxis;  // AXIS_MOUSEWHEEL value (small, ~0.1)
            if (Mathf.Abs(scroll) > 0.001f)
            {
                bool isShift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                if (!isShift)
                {
                    // Plain scroll -> Zoom (change distance)
                    if (t.Field("distance").FieldExists())
                    {
                        float zoomSens = t.Field("mouseZoomSensitivity").FieldExists() ? t.Field("mouseZoomSensitivity").GetValue<float>() : 1f;
                        float minD = t.Field("minDistance").FieldExists() ? t.Field("minDistance").GetValue<float>() : 2f;
                        float maxD = t.Field("maxDistance").FieldExists() ? t.Field("maxDistance").GetValue<float>() : 100f;
                        float newDist = Mathf.Clamp(state.d - scroll * zoomSens * 50f, minD, maxD);
                        t.Field("distance").SetValue(newDist);
                    }
                }
                else
                {
                    // Shift+scroll -> Height (change scrollHeight)
                    if (t.Field("scrollHeight").FieldExists())
                    {
                        float minH = t.Field("minHeight").FieldExists() ? t.Field("minHeight").GetValue<float>() : 0f;
                        float maxH = t.Field("maxHeight").FieldExists() ? t.Field("maxHeight").GetValue<float>() : 50f;
                        float newH = Mathf.Clamp(state.sh + scroll * 10f, minH, maxH);
                        t.Field("scrollHeight").SetValue(newH);
                        if (t.Field("clampedScrollHeight").FieldExists())
                            t.Field("clampedScrollHeight").SetValue(newH);
                    }
                }
            }
        }
    }

    // Shared Transpiler: redirect Input.* AND AxisBinding.GetAxis() calls
    public static IEnumerable<CodeInstruction> DoTranspiler(IEnumerable<CodeInstruction> instructions, System.Type interceptorType)
    {
        int count = 0;
        foreach (var inst in instructions) {
            MethodInfo method = inst.operand as MethodInfo;

            // Handle BOTH Call (static) and Callvirt (instance) methods
            if ((inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt) && method != null)
            {
                // Intercept Input.* static calls (Call)
                if (method.DeclaringType == typeof(UnityEngine.Input)) {
                    if (method.Name == "GetMouseButton" || method.Name == "GetMouseButtonDown" || method.Name == "GetMouseButtonUp")
                    { inst.operand = AccessTools.Method(interceptorType, method.Name); count++; }
                    else if (method.Name == "GetKey" || method.Name == "GetKeyDown")
                    {
                        MethodInfo replacement = AccessTools.Method(interceptorType, method.Name);
                        if (replacement != null) { inst.operand = replacement; count++; }
                    }
                    else if (method.Name == "GetAxis")
                    { inst.operand = AccessTools.Method(interceptorType, "GetAxis"); count++; }
                    else if (method.Name == "get_mouseScrollDelta")
                    {
                        MethodInfo replacement = AccessTools.Method(interceptorType, "get_mouseScrollDelta");
                        if (replacement != null) { inst.operand = replacement; count++; }
                    }
                }

                // KEY FIX: Intercept AxisBinding.GetAxis() (Callvirt, instance method)
                // This is how VABCamera reads scroll input from GameSettings
                if (method.DeclaringType == typeof(AxisBinding) && method.Name == "GetAxis")
                {
                    MethodInfo replacement = AccessTools.Method(interceptorType, "AxisBinding_GetAxis");
                    if (replacement != null)
                    {
                        inst.opcode = OpCodes.Call; // Change Callvirt to Call (static method)
                        inst.operand = replacement;
                        count++;
                    }
                }
            }
            yield return inst;
        }
    }
}

// ==================== Patches ====================

// VAB: Transpile BOTH Update and UpdateCamera to block ALL native input processing
[HarmonyPatch(typeof(VABCamera), "Update")]
public static class VABPatch {
    static void Prefix(VABCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state, false); }
    static void Postfix(VABCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state, false); }
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) 
    { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
}

// Safety net: also transpile UpdateCamera in case it has its own Input calls
[HarmonyPatch(typeof(VABCamera), "UpdateCamera")]
public static class VABUpdateCameraPatch {
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) 
    { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
}

// KEY FIX: VABCamera delegates scroll processing to CameraMouseLook.GetMouseLook
// We must transpile this method to block scroll input at its TRUE source
[HarmonyPatch(typeof(CameraMouseLook), "GetMouseLook")]
public static class CameraMouseLookPatch {
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) 
    { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
}

// SPH: same pattern
[HarmonyPatch(typeof(SPHCamera), "Update")]
public static class SPHPatch {
    static void Prefix(SPHCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state, false); }
    static void Postfix(SPHCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state, false); }
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) 
    { return PatchHelper.DoTranspiler(instructions, typeof(EditorInputInterceptor)); }
}

// Flight: unchanged (already perfect)
[HarmonyPatch(typeof(FlightCamera), "LateUpdate")]
public static class FlightPatch {
    static void Prefix(FlightCamera __instance, out CamState __state) 
    { 
        FlightInputInterceptor.BlockRMB = true;
        PatchHelper.DoPrefix(__instance, out __state, true); 
    }
    static void Postfix(FlightCamera __instance, CamState __state) 
    { 
        PatchHelper.DoPostfix(__instance, __state, true); 
        FlightInputInterceptor.BlockRMB = false;
    }
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) 
    { return PatchHelper.DoTranspiler(instructions, typeof(FlightInputInterceptor)); }
}
