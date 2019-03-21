using System;
using System.Collections.Generic;
using System.Reflection;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;

namespace DvShunterFanCoolingMod
{
    public class Main
    {
        public const float FAN_COOL = 6f;
        public const float FUEL_CONSUMPTION = 5f;
        public const float POWER_LOSS = 0.2f;

        public static bool isFanOn = false;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);

            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }
    }

    // decrease temperature
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateEngineTemp")]
    class ShunterLocoSimulation_SimulateEngineTemp_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn || __instance.engineTemp.value <= 45f)
                return;

            if (Main.isFanOn)
            {
                __instance.engineTemp.AddNextValue(-Main.FAN_COOL * delta);
            }
        }
    }

    // increase fuel consumption
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateFuel")]
    class ShunterLocoSimulation_SimulateFuel_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn || __instance.fuel.value <= 0.0f)
                return;

            if (Main.isFanOn)
            {
                __instance.fuel.AddNextValue(Mathf.Lerp(0.025f, 1f, __instance.engineRPM.value) * -Main.FUEL_CONSUMPTION * delta);
            }
        }
    }

    // decrease power
    [HarmonyPatch(typeof(LocoControllerBase), "GetTotalAppliedForcePerBogie")]
    class LocoControllerBase_GetTotalAppliedForcePerBogie_Patch
    {
        static void Postfix(ShunterLocoSimulation __instance, ref float __result)
        {
            if (Main.isFanOn)
            {
                __result *= 1f - Main.POWER_LOSS;
            }
        }
    }

    // listen to fan switch
    [HarmonyPatch(typeof(ShunterDashboardControls), "OnEnable")]
    class ShunterDashboardControls_OnEnable_Patch
    {
        static ShunterDashboardControls instance;

        static void Postfix(ShunterDashboardControls __instance)
        {
            instance = __instance;

            __instance.StartCoroutine(AttachListeners());
        }

        static IEnumerator<object> AttachListeners()
        {
            yield return (object)null;

            DV.CabControls.ControlImplBase fanCtrl = instance.fanSwitchButton.GetComponent<DV.CabControls.ControlImplBase>();
            
            fanCtrl.ValueChanged += (e =>
            {
                Main.isFanOn = e.newValue >= 0.5f;
            });
        }
    }
}
