using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;

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
        Debug.Log("[BlenderCamera] Mod initialized with strong-typed patches.");
    }
}

public static class InputInterceptor
{
    // Block button 2 (MMB) for native code, allow button 1 (RMB)
    public static bool GetMouseButton(int button)
    {
        return button == 2 ? false : UnityEngine.Input.GetMouseButton(button);
    }
}

public struct CamState { public float h, p; public Vector3? o; }

public static class PatchHelper
{
    public static void DoPrefix(object instance, out CamState state)
    {
        var t = Traverse.Create(instance);
        state = new CamState {
            h = t.Field("camHdg").GetValue<float>(),
            p = t.Field("camPitch").GetValue<float>(),
            o = t.Field("offset").FieldExists() ? t.Field("offset").GetValue<Vector3>() : (Vector3?)null
        };
    }

    public static void DoPostfix(object instance, CamState state)
    {
        if (UnityEngine.Input.GetMouseButton(2))
        {
            var t = Traverse.Create(instance);
            float s = t.Field("orbitSensitivity").GetValue<float>();
            // Apply orbit rotation based on snapshot to ensure smoothness
            t.Field("camHdg").SetValue(state.h + UnityEngine.Input.GetAxis("Mouse X") * s);
            t.Field("camPitch").SetValue(Mathf.Clamp(state.p - UnityEngine.Input.GetAxis("Mouse Y") * s, 
                t.Field("minPitch").GetValue<float>(), t.Field("maxPitch").GetValue<float>()));
            // Revert native panning in VAB/SPH
            if (state.o.HasValue) t.Field("offset").SetValue(state.o.Value);
        }
    }
}

[HarmonyPatch(typeof(VABCamera), "UpdateCamera")]
public static class VABPatch {
    static void Prefix(VABCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state); }
    static void Postfix(VABCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state); }
}

[HarmonyPatch(typeof(SPHCamera), "Update")]
public static class SPHPatch {
    static void Prefix(SPHCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state); }
    static void Postfix(SPHCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state); }
}

[HarmonyPatch(typeof(FlightCamera), "LateUpdate")]
public static class FlightPatch {
    static void Prefix(FlightCamera __instance, out CamState __state) { PatchHelper.DoPrefix(__instance, out __state); }
    static void Postfix(FlightCamera __instance, CamState __state) { PatchHelper.DoPostfix(__instance, __state); }
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        foreach (var inst in instructions) {
            if (inst.Calls(AccessTools.Method(typeof(UnityEngine.Input), "GetMouseButton", new[] { typeof(int) })))
                inst.operand = AccessTools.Method(typeof(InputInterceptor), "GetMouseButton");
            yield return inst;
        }
    }
}
