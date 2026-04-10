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
        Debug.Log("[BlenderCamera] Mod initialized with Sensitivity Nullification logic.");
    }
}

public static class InputInterceptor
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
}

public struct CamState 
{ 
    public float h, p; // Heading/Pitch
    public float oh, op; // Offset Heading/Pitch
    public float s; // Original Sensitivity
    public Vector3? o; // Position Offset
}

public static class PatchHelper
{
    public static void DoPrefix(object instance, out CamState state, bool isFlight)
    {
        var t = Traverse.Create(instance);
        float currentS = t.Field("orbitSensitivity").GetValue<float>();
        state = new CamState {
            h = t.Field("camHdg").GetValue<float>(),
            p = t.Field("camPitch").GetValue<float>(),
            oh = t.Field("offsetHdg").FieldExists() ? t.Field("offsetHdg").GetValue<float>() : 0f,
            op = t.Field("offsetPitch").FieldExists() ? t.Field("offsetPitch").GetValue<float>() : 0f,
            s = currentS,
            o = t.Field("offset").FieldExists() ? t.Field("offset").GetValue<Vector3>() : (Vector3?)null
        };

        // PARALYZE native rotation logic by setting sensitivity to 0 during the call
        if (isFlight && UnityEngine.Input.GetMouseButton(1))
        {
            t.Field("orbitSensitivity").SetValue(0f);
        }
    }

    public static void DoPostfix(object instance, CamState state, bool isFlight)
    {
        var t = Traverse.Create(instance);
        
        // Restore sensitivity IMMEDIATELY
        t.Field("orbitSensitivity").SetValue(state.s);

        float mouseX = UnityEngine.Input.GetAxis("Mouse X");
        float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

        // Logic 1: MMB -> Orbit
        if (UnityEngine.Input.GetMouseButton(2))
        {
            t.Field("camHdg").SetValue(state.h + mouseX * state.s);
            t.Field("camPitch").SetValue(Mathf.Clamp(state.p - mouseY * state.s, 
                t.Field("minPitch").GetValue<float>(), t.Field("maxPitch").GetValue<float>()));
            if (state.o.HasValue) t.Field("offset").SetValue(state.o.Value);
        }

        // Logic 2: RMB -> FreeLook (Only in Flight)
        if (isFlight && UnityEngine.Input.GetMouseButton(1))
        {
            // Still revert to be double-safe, though with sensitivity=0 it should already be the same
            t.Field("camHdg").SetValue(state.h);
            t.Field("camPitch").SetValue(state.p);

            t.Field("offsetHdg").SetValue(state.oh + mouseX * state.s);
            t.Field("offsetPitch").SetValue(state.op - mouseY * state.s);
        }
    }
}

[HarmonyPatch(typeof(VABCamera), "UpdateCamera")]
public static class VABPatch {
    static void Prefix(VABCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state, false); }
    static void Postfix(VABCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state, false); }
}

[HarmonyPatch(typeof(SPHCamera), "Update")]
public static class SPHPatch {
    static void Prefix(SPHCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state, false); }
    static void Postfix(SPHCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state, false); }
}

[HarmonyPatch(typeof(FlightCamera), "LateUpdate")]
public static class FlightPatch {
    static void Prefix(FlightCamera __instance, out CamState __state) 
    { 
        InputInterceptor.BlockRMB = true;
        PatchHelper.DoPrefix(__instance, out __state, true); 
    }
    static void Postfix(FlightCamera __instance, CamState __state) 
    { 
        PatchHelper.DoPostfix(__instance, __state, true); 
        InputInterceptor.BlockRMB = false;
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        foreach (var inst in instructions) {
            MethodInfo method = inst.operand as MethodInfo;
            if (inst.opcode == OpCodes.Call && method != null && method.DeclaringType == typeof(UnityEngine.Input)) {
                if (method.Name == "GetMouseButton" || method.Name == "GetMouseButtonDown" || method.Name == "GetMouseButtonUp")
                    inst.operand = AccessTools.Method(typeof(InputInterceptor), method.Name);
                else if (method.Name == "GetKey")
                    inst.operand = AccessTools.Method(typeof(InputInterceptor), "GetKey");
            }
            yield return inst;
        }
    }
}
