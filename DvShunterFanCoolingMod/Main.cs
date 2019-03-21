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
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // Something
            return true; // If false the mod will show an error.
        }
    }

    // fix temperature
    [HarmonyPatch(typeof(ShunterLocoSimulation), "SimulateEngineTemp")]
    class ShunterLocoSimulation_SimulateEngineTemp_Patch
    {
        public static bool fan = false;

        static void Postfix(ShunterLocoSimulation __instance, float delta)
        {
            if (fan)
            {
                __instance.engineTemp.AddNextValue(-10f * delta);
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
                ShunterLocoSimulation_SimulateEngineTemp_Patch.fan = true ? e.newValue == 1 : false;
            });
        }
    }
}
