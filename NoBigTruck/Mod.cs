﻿using CitiesHarmony.API;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using HarmonyLib;
using ICities;
using ModsCommon;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using static ColossalFramework.Plugins.PluginManager;

namespace NoBigTruck
{
    public class Mod : BasePatcherMod<Mod>
    {
        public override string NameRaw => "No Big Truck";
        public override string Description => "Big trucks dont deliver goods to stores";

        protected override string StableWorkshopUrl => "https://steamcommunity.com/sharedfiles/filedetails/?id=2069057130";
        protected override string BetaWorkshopUrl => string.Empty;

        public override List<Version> Versions { get; } = new List<Version>
        {
            new Version("1.0"),
        };

#if BETA
        public override bool IsBeta => true;
#else
        public override bool IsBeta => false;
#endif
        protected override string IdRaw => nameof(NoBigTruck);
        public override CultureInfo Culture
        {
            get => Localize.Culture;
            protected set => Localize.Culture = value;
        }
        protected override bool LoadError
        {
            get => base.LoadError || AVONotExist || AVOStateWatcher.IsEnabled == false;
            set => base.LoadError = value;
        }
        private bool AVONotExist { get; set; }
        public static PlaginStateWatcher AVOStateWatcher { get; set; }

        private static IPluginSearcher AVOSearcher { get; } = PluginUtilities.GetSearcher("Advanced Vehicle Options", 1548831935ul);
        public static PluginInfo AVO => PluginUtilities.GetPlugin(AVOSearcher);

        public override string GetLocalizeString(string str, CultureInfo culture = null) => Localize.ResourceManager.GetString(str, culture ?? Culture);
        protected override void GetSettings(UIHelperBase helper)
        {
            var settings = new Settings();
            settings.OnSettingsUI(helper);
        }

        protected override void Enable()
        {
            base.Enable();

            if (AVO is PluginInfo plugin)
                AVOStateWatcher = new PlaginStateWatcher(plugin);

            if (AVOStateWatcher != null)
            {
                AVONotExist = false;
                AVOStateWatcher.StateChanged += AVOStateChanged;
            }
            else
                AVONotExist = true;
        }
        protected override void Disable()
        {
            base.Disable();

            if (AVOStateWatcher != null)
                AVOStateWatcher.StateChanged -= AVOStateChanged;
        }

        private void AVOStateChanged(PluginInfo plugin, bool state)
        {
            if (!state)
                OnAVODisable();
        }
        protected override void OnLoadError(out bool shown)
        {
            base.OnLoadError(out shown);

            if (shown)
                return;
            else if (AVONotExist)
            {
                OnAVONotExist();
                shown = true;
            }
            else if (!AVOStateWatcher.IsEnabled)
            {
                OnAVODisable();
                shown = true;
            }
        }
        public void OnAVONotExist()
        {
            var messageBox = MessageBox.Show<TwoButtonMessageBox>();
            messageBox.CaptionText = NameRaw;
            messageBox.MessageText = string.Format(Localize.Mod_NeedSubscribeAVO, NameRaw);
            messageBox.Button1Text = CommonLocalize.MessageBox_OK;
            messageBox.Button2Text = Localize.Mod_GetAVO;
            messageBox.OnButton2Click = Enable;

            messageBox.SetButtonsRatio(2, 5);

            static bool Enable()
            {
                Utilites.OpenUrl("https://steamcommunity.com/sharedfiles/filedetails/?id=1548831935");
                return true;
            }
        }
        public void OnAVODisable()
        {
            var messageBox = MessageBox.Show<TwoButtonMessageBox>();
            messageBox.CaptionText = NameRaw;
            messageBox.MessageText = string.Format(Localize.Mod_NeedEnableAVO, NameRaw);
            messageBox.Button1Text = CommonLocalize.MessageBox_OK;
            messageBox.Button2Text = Localize.Mod_EnableAVO;
            messageBox.OnButton2Click = Enable;

            messageBox.SetButtonsRatio(2, 5);

            static bool Enable()
            {
                AVOStateWatcher.Plugin.SetState(true);
                return true;
            }
        }

        #region PATCHER

        protected override bool PatchProcess()
        {
            var success = true;

            success &= IndustrialBuildingAIStartTransferPatch();
            success &= OutsideConnectionAIStartConnectionTransferImplPatch();
            success &= WarehouseAIStartTransferPatch();
            success &= VehicleManagerRefreshTransferVehiclesPatch();

            if (AVO is null)
                Logger.Debug("Advanced Vehicle Options not exist, skip patches");
            else
                success &= AVOPatch();

            return success;
        }

        private bool IndustrialBuildingAIStartTransferPatch()
        {
            return AddTranspiler(typeof(Mod), nameof(Mod.BuildingDecorationLoadPathsTranspiler), typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths));
        }
        private bool OutsideConnectionAIStartConnectionTransferImplPatch()
        {
            return AddTranspiler(typeof(Mod), nameof(Mod.BuildingDecorationLoadPathsTranspiler), typeof(OutsideConnectionAI), "StartConnectionTransferImpl");
        }
        private static IEnumerable<CodeInstruction> BuildingDecorationLoadPathsTranspiler(MethodBase original, ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand?.ToString().Contains(nameof(VehicleManager.GetRandomVehicleInfo)) == true)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 0 : 1);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 2 : 3);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 3 : 4);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Manager), nameof(Manager.GetRandomVehicleInfo)));
                }
                else
                    yield return instruction;
            }
        }

        private bool WarehouseAIStartTransferPatch()
        {
            return AddTranspiler(typeof(Mod), nameof(Mod.WarehouseAIStartTransferTranspiler), typeof(WarehouseAI), nameof(WarehouseAI.StartTransfer));
        }
        private static IEnumerable<CodeInstruction> WarehouseAIStartTransferTranspiler(MethodBase original, ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand?.ToString().Contains(nameof(WarehouseAI.GetTransferVehicleService)) == true)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 1);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 4);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Manager), nameof(Manager.GetTransferVehicleService)));
                }
                else
                    yield return instruction;
            }
        }

        private bool VehicleManagerRefreshTransferVehiclesPatch()
        {
            return AddPostfix(typeof(Manager), nameof(Manager.RefreshTransferVehicles), typeof(VehicleManager), nameof(VehicleManager.RefreshTransferVehicles));
        }
        private bool AVOPatch()
        {
            return AddPostfix(typeof(Manager), nameof(Manager.AVOCheckChanged), Type.GetType("AdvancedVehicleOptionsUID.GUI.UIOptionPanel"), "OnCheckChanged");
        }

        #endregion
    }
}
