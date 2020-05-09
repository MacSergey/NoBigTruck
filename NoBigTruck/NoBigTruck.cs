﻿using CitiesHarmony.API;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using HarmonyLib;
using ICities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NoBigTruck
{
    public class ModInfo : IUserMod
    {
        public string Name => nameof(NoBigTruck);

        public string Description => "Large trucks do not deliver goods to stores";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }

    }
    public static class Patcher
    {
        private const string HarmonyId = nameof(NoBigTruck);

        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;

            Debug.Log($"{HarmonyId}: Patching...");

            patched = true;

            //Harmony.DEBUG = true;
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);

            patched = false;

            Debug.Log($"{HarmonyId}: Reverted...");
        }
    }

    [HarmonyPatch]
    public static class GetVehiclePatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(IndustrialBuildingAI), nameof(IndustrialBuildingAI.StartTransfer));
            yield return AccessTools.Method(typeof(OutsideConnectionAI), "StartConnectionTransferImpl");
        }
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand?.ToString().Contains(nameof(VehicleManager.GetRandomVehicleInfo)) == true)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 0 : 1);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 2 : 3);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, original.IsStatic ? 3 : 4);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GetVehiclePatch), nameof(GetRandomVehicleInfo)));
                }
                else
                    yield return instruction;
            }
        }

        public static VehicleInfo GetRandomVehicleInfo(VehicleManager manager, ref Randomizer r, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level, ushort buildingID, TransferManager.TransferReason material, TransferManager.TransferOffer offer)
        {
            //Debug.Log($"StartTransfer: \nsource: {buildingID}; target: {offer.Building}; {nameof(material)}: {material};");
            try
            {
                if (material == TransferManager.TransferReason.Goods && CheckItemClass(Singleton<BuildingManager>.instance.m_buildings.m_buffer[offer.Building].Info.m_class))
                {
                    var transferIndex = (int)AccessTools.Method(typeof(VehicleManager), "GetTransferIndex").Invoke(null, new object[] { service, subService, level });
                    var fastList = (AccessTools.Field(typeof(VehicleManager), "m_transferVehicles").GetValue(manager) as FastList<ushort>[])[transferIndex];

                    var notLarge = new List<VehicleInfo>();
                    foreach (var index in fastList)
                    {
                        var vehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(index);
                        if (!vehicleInfo.m_isLargeVehicle)
                            notLarge.Add(vehicleInfo);
                    }

                    if (notLarge.Any())
                    {
                        var selectIndex = r.Int32((uint)notLarge.Count);
                        var selectVehicle = notLarge[selectIndex];
                        //Debug.Log($"VehicleSelected: {selectVehicle}");
                        return selectVehicle;
                    }
                    //else
                    //    Debug.Log($"No one not large vehicle");
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"{error.Message}\n{error.StackTrace}");
            }

            return manager.GetRandomVehicleInfo(ref r, service, subService, level);
        }
        public static bool CheckItemClass(ItemClass itemClass)
        {
            //Debug.Log($"CheckItemClass: \n{nameof(itemClass.m_service)}: {itemClass.m_service}; {nameof(itemClass.m_subService)}: {itemClass.m_subService}");

            if (itemClass.m_service != ItemClass.Service.Commercial)
                return false;

            switch (itemClass.m_subService)
            {
                case ItemClass.SubService.CommercialLow:
                case ItemClass.SubService.CommercialHigh:
                case ItemClass.SubService.CommercialEco:
                case ItemClass.SubService.CommercialLeisure:
                case ItemClass.SubService.CommercialTourist:
                    return true;
                default:
                    return false;
            }
        }
    }

    [HarmonyPatch]
    public static class WarehouseAIStartTransferPath
    {
        public static MethodBase TargetMethod() => AccessTools.Method(typeof(WarehouseAI), nameof(WarehouseAI.StartTransfer));

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand?.ToString().Contains(nameof(WarehouseAI.GetTransferVehicleService)) == true)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 1);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, 4);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WarehouseAIStartTransferPath), nameof(WarehouseAIStartTransferPath.GetTransferVehicleService)));
                }
                else
                    yield return instruction;
            }
        }

        public static VehicleInfo GetTransferVehicleService(TransferManager.TransferReason material, ItemClass.Level level, ref Randomizer randomizer, ushort buildingID, TransferManager.TransferOffer offer)
        {
            var vehicleInfo = WarehouseAI.GetTransferVehicleService(material, level, ref randomizer);
            return vehicleInfo == null ? null : GetVehiclePatch.GetRandomVehicleInfo(Singleton<VehicleManager>.instance, ref randomizer, vehicleInfo.GetService(), vehicleInfo.GetSubService(), level, buildingID, material, offer);
        }
    }
}
