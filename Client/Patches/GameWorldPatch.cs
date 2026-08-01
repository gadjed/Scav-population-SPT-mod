using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace ScavPopulation.Client.Patches;

public class GameWorldPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorldUnityTickListener).GetMethod(
            nameof(GameWorldUnityTickListener.Create),
            BindingFlags.Public | BindingFlags.Static
        )!;
    }

    [PatchPostfix]
    public static void PatchPostfix(GameObject gameObject, GameWorld gameWorld)
    {
        if (gameWorld is HideoutGameWorld)
        {
            return;
        }

        if (gameObject.GetComponent<PopulationMaintainerComponent>() != null)
        {
            return;
        }

        var component = gameObject.AddComponent<PopulationMaintainerComponent>();
        component.Init(gameWorld);
        ScavPopulationPlugin.Log.LogInfo("[ScavPopulation] Raid maintainer attached.");
    }
}

public class BotsControllerPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(BotsController).GetMethod(nameof(BotsController.Init))!;
    }

    [PatchPostfix]
    public static void PatchPostfix(BotsController __instance)
    {
        PopulationMaintainerComponent.BotsController = __instance;
    }
}
