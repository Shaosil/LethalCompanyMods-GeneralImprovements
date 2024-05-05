using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using static GeneralImprovements.Plugin.Enums;

namespace GeneralImprovements.Patches
{
    internal static class MenuPatches
    {
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPrefix]
        private static void MenuManager_Start(MenuManager __instance)
        {
            if (!Plugin.AlwaysShowNews.Value && GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.firstTimeInMenu = false;
            }

            // If needed, create a new AudioSource for our menu music to play at a different volume
            if (Plugin.MenuMusicVolume.Value > 0 && Plugin.MenuMusicVolume.Value < 100 && __instance.MenuAudio != null)
            {
                var newAudioSource = __instance.gameObject.AddComponent<AudioSource>();
                ObjectHelper.CopyAudioSource(__instance.MenuAudio, newAudioSource);
                newAudioSource.clip = __instance.menuMusic;
                newAudioSource.volume = Plugin.MenuMusicVolume.Value / 100f;
                __instance.StartCoroutine(PlayMenuMusicDelayedCoroutine(newAudioSource));
            }
        }

        [HarmonyPatch(typeof(InitializeGame), "Start")]
        [HarmonyPrefix]
        private static void Start_Initialize(InitializeGame __instance)
        {
            if (Plugin.SkipStartupScreen.Value)
            {
                __instance.runBootUpScreen = false;
            }
        }

        [HarmonyPatch(typeof(PreInitSceneScript), nameof(SkipToFinalSetting))]
        [HarmonyPrefix]
        private static bool SkipToFinalSetting(PreInitSceneScript __instance)
        {
            if (Plugin.AutoSelectLaunchMode.Value != eAutoLaunchOptions.NONE)
            {
                Plugin.MLS.LogInfo($"Automatically launching {Plugin.AutoSelectLaunchMode.Value} mode.");
                __instance.ChooseLaunchOption(Plugin.AutoSelectLaunchMode.Value == eAutoLaunchOptions.ONLINE);
                __instance.launchSettingsPanelsContainer.SetActive(false);

                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(MenuManager), nameof(PlayMenuMusicDelayed), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlayMenuMusicDelayed(IEnumerable<CodeInstruction> instructions)
        {
            if (Plugin.MenuMusicVolume.Value < 100)
            {
                if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdloc(),
                    i => i.LoadsField(typeof(MenuManager).GetField(nameof(MenuManager.MenuAudio))),
                    i => i.IsLdloc(),
                    i => i.LoadsField(typeof(MenuManager).GetField(nameof(MenuManager.menuMusic))),
                    i => i.Calls(typeof(AudioSource).GetMethod("set_clip")),
                    i => i.IsLdloc(),
                    i => i.LoadsField(typeof(MenuManager).GetField(nameof(MenuManager.MenuAudio))),
                    i => i.Calls(typeof(AudioSource).GetMethod(nameof(AudioSource.Play), Type.EmptyTypes))
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching MenuManager.PlayMenuMusicDelayed to control music volume.");

                    // Remove call to load and play menu music since we will create our own audio source for that
                    foreach (var item in found)
                    {
                        item.Instruction.opcode = OpCodes.Nop;
                        item.Instruction.operand = null;
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not patch MenuManager.PlayMenuMusicDelayed to control music volume!");
                }
            }

            return instructions;
        }

        private static IEnumerator PlayMenuMusicDelayedCoroutine(AudioSource menuMusicSource)
        {
            yield return new WaitForSeconds(0.4f);
            menuMusicSource.Play();
        }
    }
}