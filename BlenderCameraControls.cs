using System;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[KSPAddon(KSPAddon.Startup.Instantly, true)]
public class BlenderCameraMod : MonoBehaviour
{
    private static Harmony harmony;

    void Awake()
    {
        DontDestroyOnLoad(this);
        if (harmony != null) return;
        try
        {
            harmony = new Harmony("com.antigravity.blendercamera");
            
            // 1. VAB Camera
            TryPatch(typeof(VABCamera), "UpdateCamera", 
                typeof(VABPatches).GetMethod("Prefix"), 
                typeof(VABPatches).GetMethod("Postfix"), null);

            // 2. SPH Camera
            TryPatch(typeof(SPHCamera), "Update", 
                typeof(SPHPatches).GetMethod("Prefix"), 
                typeof(SPHPatches).GetMethod("Postfix"), null);

            // 3. Flight Camera
            TryPatch(typeof(FlightCamera), "LateUpdate", 
                typeof(FlightPatches).GetMethod("Prefix"), 
                typeof(FlightPatches).GetMethod("Postfix"), 
                typeof(FlightPatches).GetMethod("Transpiler"));

            Debug.Log("[BlenderCamera] Consolidated MMB-Only mod initialized successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[BlenderCamera] Critical failure during initialization: " + e.Message);
        }
    }

    private void TryPatch(Type type, string methodName, MethodInfo pre, MethodInfo post, MethodInfo trans)
    {
        if (type == null) return;
        var target = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (target == null) return;
        
        harmony.Patch(target, 
            pre != null ? new HarmonyMethod(pre) : null, 
            post != null ? new HarmonyMethod(post) : null, 
            trans != null ? new HarmonyMethod(trans) : null);
    }
}

public static class InputInterceptor
{
    public static bool GetMouseButton(int button)
    {
        // Block button 1 (RMB) and 2 (MMB) from native FlightCamera code
        // This stops native Rotate (RMB) and native FreeLook (MMB)
        if (button == 1 || button == 2) return false;
        return Input.GetMouseButton(button);
    }
}

public class CamStateSnapshot
{
    public float h, p, s, minP, maxP;
    public Vector3 o;
}

public static class VABPatches
{
    public static void Prefix(VABCamera __instance, out CamStateSnapshot __state)
    {
        Traverse t = Traverse.Create(__instance);
        __state = new CamStateSnapshot {
            h = t.Field("camHdg").GetValue<float>(),
            p = t.Field("camPitch").GetValue<float>(),
            s = t.Field("orbitSensitivity").GetValue<float>(),
            o = t.Field("offset").GetValue<Vector3>(),
            minP = t.Field("minPitch").GetValue<float>(),
            maxP = t.Field("maxPitch").GetValue<float>()
        };
    }

    public static void Postfix(VABCamera __instance, CamStateSnapshot __state)
    {
        if (Input.GetMouseButton(2))
        {
            Traverse t = Traverse.Create(__instance);
            t.Field("offset").SetValue(__state.o); // Block native Pan by reverting it instantly
            t.Field("camHdg").SetValue(__state.h + Input.GetAxis("Mouse X") * __state.s);
            t.Field("camPitch").SetValue(Mathf.Clamp(__state.p - Input.GetAxis("Mouse Y") * __state.s, __state.minP, __state.maxP));
        }
    }
}

public static class SPHPatches
{
    public static void Prefix(SPHCamera __instance, out CamStateSnapshot __state)
    {
        Traverse t = Traverse.Create(__instance);
        __state = new CamStateSnapshot {
            h = t.Field("camHdg").GetValue<float>(),
            p = t.Field("camPitch").GetValue<float>(),
            s = t.Field("orbitSensitivity").GetValue<float>(),
            o = t.Field("offset").GetValue<Vector3>(),
            minP = t.Field("minPitch").GetValue<float>(),
            maxP = t.Field("maxPitch").GetValue<float>()
        };
    }

    public static void Postfix(SPHCamera __instance, CamStateSnapshot __state)
    {
        if (Input.GetMouseButton(2))
        {
            Traverse t = Traverse.Create(__instance);
            t.Field("offset").SetValue(__state.o); // Block native Pan by reverting it instantly
            t.Field("camHdg").SetValue(__state.h + Input.GetAxis("Mouse X") * __state.s);
            t.Field("camPitch").SetValue(Mathf.Clamp(__state.p - Input.GetAxis("Mouse Y") * __state.s, __state.minP, __state.maxP));
        }
    }
}

public static class FlightPatches
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count; i++)
        {
            MethodInfo mi = codes[i].operand as MethodInfo;
            if (codes[i].opcode == OpCodes.Call && mi != null && mi.Name == "GetMouseButton" && mi.DeclaringType == typeof(Input))
            {
                codes[i].operand = typeof(InputInterceptor).GetMethod("GetMouseButton", new Type[] { typeof(int) });
            }
        }
        return codes;
    }

    public static void Prefix(FlightCamera __instance, out CamStateSnapshot __state)
    {
        Traverse t = Traverse.Create(__instance);
        __state = new CamStateSnapshot {
            h = t.Field("camHdg").GetValue<float>(),
            p = t.Field("camPitch").GetValue<float>(),
            s = t.Field("orbitSensitivity").GetValue<float>(),
            minP = t.Field("minPitch").GetValue<float>(),
            maxP = t.Field("maxPitch").GetValue<float>()
        };
    }

    public static void Postfix(FlightCamera __instance, CamStateSnapshot __state)
    {
        if (Input.GetMouseButton(2))
        {
            Traverse t = Traverse.Create(__instance);
            t.Field("camHdg").SetValue(__state.h + Input.GetAxis("Mouse X") * __state.s);
            t.Field("camPitch").SetValue(Mathf.Clamp(__state.p - Input.GetAxis("Mouse Y") * __state.s, __state.minP, __state.maxP));
        }
    }
}
