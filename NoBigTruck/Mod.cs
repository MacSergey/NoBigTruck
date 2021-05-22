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

        protected override ulong StableWorkshopId => 2069057130ul;
        protected override ulong BetaWorkshopId => 0ul;

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
        protected override List<BaseDependencyInfo> DependencyInfos
        {
            get
            {
                var infos = base.DependencyInfos;

                var searcher = PluginUtilities.GetSearcher("Advanced Vehicle Options", 1548831935ul);
                var info = new NeedDependencyInfo(DependencyState.Enable, searcher, "Advanced Vehicle Options", 1548831935ul);
                infos.Add(info);
//#if DEBUG
//                var imtSearcher = PluginUtilities.GetSearcher("Intersection");
//                var imtInfo = new ConflictDependencyInfo(DependencyState.Disable, imtSearcher);
//                infos.Add(imtInfo);

//                var harmonySearcher = PluginUtilities.GetSearcher("Harmony 2", 2040656402ul, 2399204842ul);
//                var harmonyInfo = new ConflictDependencyInfo(DependencyState.Unsubscribe, harmonySearcher);
//                infos.Add(harmonyInfo);
//#endif
                return infos;
            }
        }
        private static PluginSearcher AVOSearcher { get; } = PluginUtilities.GetSearcher("Advanced Vehicle Options", 1548831935ul);
        public static PluginInfo AVO => PluginUtilities.GetPlugin(AVOSearcher);


        public override string GetLocalizeString(string str, CultureInfo culture = null) => Localize.ResourceManager.GetString(str, culture ?? Culture);
        protected override void GetSettings(UIHelperBase helper)
        {
            var settings = new Settings();
            settings.OnSettingsUI(helper);
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
