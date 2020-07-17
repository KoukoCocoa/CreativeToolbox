﻿using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs;
using Exiled.Permissions.Extensions;
using Grenades;
using Hints;
using MEC;
using Mirror;
using UnityEngine;

namespace CreativeToolbox
{
    public sealed class CreativeToolboxEventHandler
    {
        public static HashSet<ReferenceHub> PlayersThatCanPryGates = new HashSet<ReferenceHub>();
        HashSet<ReferenceHub> PlayersWithRegen = new HashSet<ReferenceHub>();
        HashSet<ReferenceHub> PlayersWithInfiniteAmmo = new HashSet<ReferenceHub>();
        HashSet<String> PlayersWithRetainedScale = new HashSet<String>();
        string[] DoorsThatAreLocked = { "012", "049_ARMORY", "079_FIRST", "079_SECOND", "096", "106_BOTTOM", 
            "106_PRIMARY", "106_SECONDARY", "173_ARMORY", "914", "CHECKPOINT_ENT", "CHECKPOINT_LCZ_A", "CHECKPOINT_LCZ_B",
            "GATE_A", "GATE_B", "HCZ_ARMORY", "HID", "INTERCOM", "LCZ_ARMORY", "NUKE_ARMORY", "NUKE_SURFACE" };
        string[] GatesThatExist = { "914", "GATE_A", "GATE_B", "079_FIRST", "079_SECOND" };
        System.Random RandNum = new System.Random();
        bool IsWarheadDetonated;
        bool IsDecontanimationActivated;
        bool AllowRespawning = false;
        bool PreventFallDamage = false;
        bool WasDeconCommandRun = false;
        bool AutoScaleOn = false;
        CoroutineHandle ChaosRespawnHandle;

        public CreativeToolbox plugin;
        public CreativeToolboxEventHandler(CreativeToolbox plugin) => this.plugin = plugin;

        public void RunOnRoundRestart()
        {
            AllowRespawning = false;
        }

        public void RunOnRoundStart()
        {
            AllowRespawning = false;
            PlayersWithRegen.Clear();
            PlayersWithInfiniteAmmo.Clear();
            PlayersThatCanPryGates.Clear();
            PlayersWithRetainedScale.Clear();
            if (plugin.Config.EnableFallDamagePrevention)
                PreventFallDamage = true;
            if (plugin.Config.EnableAutoScaling)
            {
                foreach (Player Ply in Player.List)
                {
                    if (!plugin.Config.DisableAutoScaleMessages)
                        Map.Broadcast(5, $"Everyone who joined has their playermodel scale set to {plugin.Config.AutoScaleValue}x!", Broadcast.BroadcastFlags.Normal);
                    Ply.Scale = new Vector3(plugin.Config.AutoScaleValue, plugin.Config.AutoScaleValue, plugin.Config.AutoScaleValue);
                    PlayersWithRetainedScale.Add(Ply.UserId);
                    AutoScaleOn = true;
                }
            }
            if (plugin.Config.EnableGrenadeOnDeath)
                Map.Broadcast(10, $"<color=red>Warning: Grenades spawn after you die, they explode after {plugin.Config.GrenadeTimerOnDeath} seconds of them spawning, be careful!</color>", Broadcast.BroadcastFlags.Normal);
            if (plugin.Config.EnableAhpShield)
            {
                foreach (Player Ply in Player.List)
                {
                    Ply.ReferenceHub.gameObject.AddComponent<KeepAHPShield>();
                }
                if (plugin.Config.AhpValueLimit > 75f)
                    plugin.Config.AhpValueLimit = 75f;
                Map.Broadcast(10, $"<color=green>AHP will not go down naturally, only by damage, it can go up if you get more AHP through medical items. The AHP Limit is: {plugin.Config.AhpValueLimit}</color>", Broadcast.BroadcastFlags.Normal);
            }
        }

        public void RunOnPlayerJoin(JoinedEventArgs PlyJoin)
        {
            if (AutoScaleOn && plugin.Config.EnableKeepScale)
            {
                if (PlayersWithRetainedScale.Contains(PlyJoin.Player.UserId)) {
                    if (!plugin.Config.DisableAutoScaleMessages)
                        PlyJoin.Player.Broadcast(5, $"Your playermodel scale was set to {plugin.Config.AutoScaleValue}x!", Broadcast.BroadcastFlags.Normal);
                    PlyJoin.Player.Scale = new Vector3(plugin.Config.AutoScaleValue, plugin.Config.AutoScaleValue, plugin.Config.AutoScaleValue);
                }
            }
            if (plugin.Config.EnableAhpShield)
                PlyJoin.Player.ReferenceHub.gameObject.AddComponent<KeepAHPShield>();
        }

        public void RunOnPlayerLeave(LeftEventArgs PlyLeave)
        {
            if (PlayersWithRegen.Contains(PlyLeave.Player.ReferenceHub))
                PlayersWithRegen.Remove(PlyLeave.Player.ReferenceHub);
            if (PlayersWithInfiniteAmmo.Contains(PlyLeave.Player.ReferenceHub))
                PlayersWithInfiniteAmmo.Remove(PlyLeave.Player.ReferenceHub);
            if (PlayersThatCanPryGates.Contains(PlyLeave.Player.ReferenceHub))
                PlayersThatCanPryGates.Remove(PlyLeave.Player.ReferenceHub);
        }

        public void RunOnPlayerDeath(DiedEventArgs PlyDeath)
        {
            if (AllowRespawning)
            {
                IsWarheadDetonated = Warhead.IsDetonated;
                IsDecontanimationActivated = Map.IsLCZDecontaminated;
                Timing.CallDelayed(plugin.Config.RandomRespawnTimer, () => RevivePlayer(PlyDeath.Target));
            }
            if (plugin.Config.EnableGrenadeOnDeath)
                SpawnGrenadeOnPlayer(PlyDeath.Target, true);
            if (PlyDeath.Killer.Role != RoleType.Scp049)
                return;
        }

        public void RunOnPlayerHurt(HurtingEventArgs PlyHurt)
        {
            if (PreventFallDamage)
                if (PlyHurt.DamageType == DamageTypes.Falldown)
                    PlyHurt.Amount = 0;
        }

        public void RunOnMedItemUsed(UsedMedicalItemEventArgs MedUsed)
        {
            if (plugin.Config.EnableCustomHealing)
            {
                switch (MedUsed.Item)
                {
                    case ItemType.Painkillers:
                        MedUsed.Player.AdrenalineHealth += (int)plugin.Config.PainkillerAhpHealthValue;
                        break;
                    case ItemType.Medkit:
                        MedUsed.Player.AdrenalineHealth += (int)plugin.Config.MedkitAhpHealthValue;
                        break;
                    case ItemType.Adrenaline:
                        if (!(plugin.Config.AdrenalineAhpHealthValue <= 0))
                            MedUsed.Player.AdrenalineHealth += (int)plugin.Config.AdrenalineAhpHealthValue;
                        break;
                    case ItemType.SCP500:
                        MedUsed.Player.AdrenalineHealth += (int)plugin.Config.Scp500AhpHealthValue;
                        break;
                    case ItemType.SCP207:
                        MedUsed.Player.AdrenalineHealth += (int)plugin.Config.Scp207AhpHealthValue;
                        break;
                }
            }
            if (plugin.Config.EnableExplodingAfterDrinkingScp207)
            {
                if (MedUsed.Item == ItemType.SCP207)
                {
                    if (!MedUsed.Player.ReferenceHub.TryGetComponent(out SCP207Counter ExplodeAfterDrinking))
                    {
                        MedUsed.Player.ReferenceHub.gameObject.AddComponent<SCP207Counter>();
                        return;
                    }
                    ExplodeAfterDrinking.Counter++;
                }
            }
        }

        public void RunWhenDoorIsInteractedWith(InteractingDoorEventArgs DoorInter)
        {
            if (plugin.Config.EnableDoorMessages && !DoorInter.Player.Role.IsNotHuman())
            {
                if (PlayersThatCanPryGates.Contains(DoorInter.Player.ReferenceHub) && GatesThatExist.Contains(DoorInter.Door.DoorName))
                {
                    DoorInter.Door.PryGate();
                    if (!DoorInter.Player.IsBypassModeEnabled)
                    {
                        DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.PryGateMessage}", new HintParameter[]
                        {
                            new StringHintParameter("")
                        }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                    }
                    else
                    {
                        DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.PryGateBypassMessage}", new HintParameter[]
                        {
                            new StringHintParameter("")
                        }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                    }
                }
                else
                {
                    if (!DoorInter.Player.IsBypassModeEnabled)
                    {
                        if (DoorInter.Player.ReferenceHub.ItemInHandIsKeycard() && DoorsThatAreLocked.Contains(DoorInter.Door.DoorName))
                        {
                            if (DoorInter.IsAllowed)
                            {
                                DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.UnlockedDoorMessage}", new HintParameter[]
                                {
                                    new StringHintParameter("")
                                }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                            }
                            else
                            {
                                DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.LockedDoorMessage}", new HintParameter[]
                                {
                                    new StringHintParameter("")
                                }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                            }
                        }
                        else if (!DoorInter.Player.ReferenceHub.ItemInHandIsKeycard() && DoorsThatAreLocked.Contains(DoorInter.Door.DoorName))
                        {
                            DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.NeedKeycardMessage}", new HintParameter[]
                            {
                                new StringHintParameter("")
                            }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                        }
                    }
                    else if (DoorInter.Player.IsBypassModeEnabled && DoorsThatAreLocked.Contains(DoorInter.Door.DoorName))
                    {
                        if (DoorInter.Player.ReferenceHub.ItemInHandIsKeycard())
                        {
                            DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.BypassWithKeycardMessage}", new HintParameter[]
                            {
                                new StringHintParameter("")
                            }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                        }
                        else
                        {
                            DoorInter.Player.ReferenceHub.hints.Show(new TextHint($"\n\n\n\n\n\n\n\n\n{plugin.Config.BypassKeycardMessage}", new HintParameter[]
                            {
                                new StringHintParameter("")
                            }, HintEffectPresets.FadeInAndOut(0.25f, 1f, 0f)));
                        }
                    }
                }
            }
        }

        public void RunWhenPlayerEntersFemurBreaker(EnteringFemurBreakerEventArgs FemurBreaker)
        {
            if (plugin.Config.EnableScp106AdvancedGod)
            {
                foreach (Player Ply in Player.List)
                {
                    if (!(Ply.Role == RoleType.Scp106))
                        continue;

                    if (Ply.IsGodModeEnabled)
                    {
                        FemurBreaker.IsAllowed = false;
                        FemurBreaker.Player.Broadcast(2, "SCP-106 has advanced godmode, you cannot contain him", Broadcast.BroadcastFlags.Normal);
                        return;
                    }
                }
            }
        }

        public void RunWhenWarheadIsDetonated()
        {
            if (!IsWarheadDetonated && plugin.Config.EnableDoorsDestroyedWithWarhead)
            {
                foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
                {
                    door.Networkdestroyed = true;
                    door.Networklocked = true;
                }
            }
        }

        public void RunWhenTeamRespawns(RespawningTeamEventArgs TeamRspwn)
        {
            if (plugin.Config.EnableReverseRoleRespawnWaves)
                Timing.CallDelayed(0.1f, () => ChaosRespawnHandle = Timing.RunCoroutine(SpawnReverseOfWave(TeamRspwn.Players, TeamRspwn.NextKnownTeam == Respawning.SpawnableTeamType.ChaosInsurgency)));
        }

        public void RunOnRemoteAdminCommand(SendingRemoteAdminCommandEventArgs RAComEv)
        {
            try
            {
                switch (RAComEv.Name.ToLower())
                {
                    case "arspawn":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.arspawn"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: arspawn (on/off/value) (value (if choosing \"time\"))");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "on":
                                        if (!AllowRespawning)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage("Auto respawning enabled!");
                                            Map.Broadcast(5, "<color=green>Random auto respawning enabled!</color>", Broadcast.BroadcastFlags.Normal);
                                            AllowRespawning = true;
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Auto respawning is already on!");
                                        break;
                                    case "off":
                                        if (AllowRespawning)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage("Auto respawning disabled!");
                                            Map.Broadcast(5, "<color=red>Random auto respawning disabled!</color>", Broadcast.BroadcastFlags.Normal);
                                            AllowRespawning = false;
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Auto respawning is already off!");
                                        break;
                                    case "time":
                                        RAComEv.Sender.RemoteAdminMessage("Missing value for time!");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter either \"on\" or \"off\" (If # of arguments is 1)!");
                                        break;
                                }
                                break;
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "time":
                                        if (float.TryParse(RAComEv.Arguments[1].ToLower(), out float rspwn) && rspwn > 0)
                                        {
                                            plugin.Config.RandomRespawnTimer = rspwn;
                                            RAComEv.Sender.RemoteAdminMessage($"Auto respawning timer is now set to {rspwn} seconds!");
                                            Map.Broadcast(5, $"Auto respawning timer is now set to {rspwn} seconds!", Broadcast.BroadcastFlags.Normal);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Invalid value for auto respawn timer! Value: {RAComEv.Arguments[1].ToLower()}");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter only \"time\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 2");
                                break;
                        }
                        break;
                    case "autoscale":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.autoscale"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command!");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "off":
                                        if (AutoScaleOn)
                                        {
                                            foreach (Player Ply in Player.List)
                                            {
                                                Ply.Scale = Vector3.one;
                                            }
                                            if (!plugin.Config.DisableAutoScaleMessages)
                                                Map.Broadcast(5, "Everyone has been restored to their normal size!", Broadcast.BroadcastFlags.Normal);
                                            PlayersWithRetainedScale.Clear();
                                            RAComEv.Sender.RemoteAdminMessage("Everyone's player scale has been reset");
                                            AutoScaleOn = false;
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Auto scaling is already off!");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter only \"on\" (value) or \"off\"!");
                                        break;
                                }
                                break;
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "on":
                                        if (!float.TryParse(RAComEv.Arguments[1], out float value))
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Invalid value for scale: {RAComEv.Arguments[1]}");
                                            return;
                                        }
                                        foreach (Player Ply in Player.List)
                                        {
                                            Ply.Scale = new Vector3(value, value, value);
                                            PlayersWithRetainedScale.Add(Ply.UserId);
                                        }
                                        AutoScaleOn = true;
                                        RAComEv.Sender.RemoteAdminMessage($"Everyone's player scale is {RAComEv.Arguments[1]} now");
                                        if (!plugin.Config.DisableAutoScaleMessages)
                                            Map.Broadcast(5, $"Everyone has their playermodel scale set to {value}x!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter only \"on\" (value)!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}");
                                break;
                        }
                        break;
                    case "explode":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.explode"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command!");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: explode ((id/name)/*/all)");
                            return;
                        }

                        switch (RAComEv.Arguments[0].ToLower())
                        {
                            case "all":
                            case "*":
                                foreach (Player Ply in Player.List)
                                {
                                    switch (Ply.Role)
                                    {
                                        case RoleType.Spectator:
                                        case RoleType.None:
                                            break;
                                        default:
                                            Ply.Kill();
                                            SpawnGrenadeOnPlayer(Ply, false);
                                            break;
                                    }
                                }
                                RAComEv.Sender.RemoteAdminMessage($"Everyone exploded, Hubert cannot believe you did this");
                                break;
                            default:
                                Player ChosenPlayer = Player.Get(RAComEv.Arguments[0]);
                                if (ChosenPlayer == null)
                                {
                                    RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[0]}\" not found");
                                    return;
                                }
                                
                                switch (ChosenPlayer.Role)
                                {
                                    case RoleType.Spectator:
                                    case RoleType.None:
                                        RAComEv.Sender.RemoteAdminMessage($"Player \"{ChosenPlayer.Nickname}\" is not a valid class to explode, not this time!");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage($"Player \"{ChosenPlayer.Nickname}\" game ended (exploded)");
                                        ChosenPlayer.Kill();
                                        SpawnGrenadeOnPlayer(ChosenPlayer, false);
                                        break;
                                }
                                break;
                        }
                        break;
                    case "fdamage":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.fdamage"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command!");
                            return;
                        }

                        if (plugin.Config.DisableFallModification)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Fall damage cannot be modified!");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: fdamage (on/off)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "on":
                                        if (PreventFallDamage)
                                        {
                                            PreventFallDamage = false;
                                            RAComEv.Sender.RemoteAdminMessage("Fall damage enabled!");
                                            Map.Broadcast(5, "<color=green>Fall damage enabled!</color>", Broadcast.BroadcastFlags.Normal);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Fall damage is already on!");
                                        break;
                                    case "off":
                                        if (!PreventFallDamage)
                                        {
                                            PreventFallDamage = true;
                                            RAComEv.Sender.RemoteAdminMessage("Fall damage disabled!");
                                            Map.Broadcast(5, "<color=red>Fall damage disabled!</color>", Broadcast.BroadcastFlags.Normal);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Fall damage is already off!");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter either \"on\" or \"off\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}");
                                break;
                        }
                        break;
                    case "giveammo":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.giveammo"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 3)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: giveammo (*/all/(id or name)) (5/7/9) (amount)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 3:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "*":
                                    case "all":
                                        switch (RAComEv.Arguments[1].ToLower())
                                        {
                                            case "5":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int FiveMM) && FiveMM >= 0)
                                                {
                                                    foreach (Player Ply in Player.List)
                                                    {
                                                        if (Ply.Role != RoleType.None)
                                                            Ply.SetAmmo(Exiled.API.Enums.AmmoType.Nato556, (uint) (Ply.GetAmmo(Exiled.API.Enums.AmmoType.Nato556) + FiveMM));
                                                    }
                                                    RAComEv.Sender.RemoteAdminMessage($"{FiveMM} 5.56mm ammo given to everyone!");
                                                    Map.Broadcast(3, $"Everyone has been given {FiveMM} 5.56mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            case "7":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int SevenMM) && SevenMM >= 0)
                                                {
                                                    foreach (Player Ply in Player.List)
                                                    {
                                                        if (Ply.Role != RoleType.None)
                                                            Ply.SetAmmo(Exiled.API.Enums.AmmoType.Nato762, (uint) (Ply.GetAmmo(Exiled.API.Enums.AmmoType.Nato762) + SevenMM));
                                                    }
                                                    RAComEv.Sender.RemoteAdminMessage($"{SevenMM} 7.62mm ammo given to everyone!");
                                                    Map.Broadcast(3, $"Everyone has been given {SevenMM} 7.62mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            case "9":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int NineMM) && NineMM >= 0)
                                                {
                                                    foreach (Player Ply in Player.List)
                                                    {
                                                        if (Ply.Role != RoleType.None)
                                                            Ply.SetAmmo(Exiled.API.Enums.AmmoType.Nato9, (uint) (Ply.GetAmmo(Exiled.API.Enums.AmmoType.Nato9) + NineMM));
                                                    }
                                                    RAComEv.Sender.RemoteAdminMessage($"{NineMM} 9.00mm ammo given to everyone!");
                                                    Map.Broadcast(3, $"Everyone has been given {NineMM} 9mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            default:
                                                RAComEv.Sender.RemoteAdminMessage($"Please enter \"5\" (5.56mm), \"7\" (7.62mm), or \"9\" (9.00mm)!");
                                                break;
                                        }
                                        break;
                                    default:
                                        Player ChosenPlayer = Player.Get(RAComEv.Arguments[0]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[0]}\" not found");
                                            return;
                                        }
                                        else if (ChosenPlayer.Role == RoleType.None)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage("You cannot give ammo to a person with no role!");
                                            return;
                                        }
                                        switch (RAComEv.Arguments[1].ToLower())
                                        {
                                            case "5":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int FiveMM) && FiveMM >= 0)
                                                {
                                                    ChosenPlayer.SetAmmo(Exiled.API.Enums.AmmoType.Nato556, (uint) (ChosenPlayer.GetAmmo(Exiled.API.Enums.AmmoType.Nato556) + FiveMM));
                                                    RAComEv.Sender.RemoteAdminMessage($"{FiveMM} 5.56mm ammo given to \"{ChosenPlayer.Nickname}\"!");
                                                    Player.Get(RAComEv.Arguments[0])?.Broadcast(3, $"You were given {FiveMM} of 5.56mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            case "7":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int SevenMM) && SevenMM >= 0)
                                                {
                                                    ChosenPlayer.SetAmmo(Exiled.API.Enums.AmmoType.Nato762, (uint) (ChosenPlayer.GetAmmo(Exiled.API.Enums.AmmoType.Nato762) + SevenMM));
                                                    RAComEv.Sender.RemoteAdminMessage($"{SevenMM} 7.62mm ammo given to \"{ChosenPlayer.Nickname}\"!");
                                                    Player.Get(RAComEv.Arguments[0])?.Broadcast(3, $"You were given {SevenMM} of 7.62mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            case "9":
                                                if (int.TryParse(RAComEv.Arguments[2].ToLower(), out int NineMM) && NineMM >= 0)
                                                {
                                                    ChosenPlayer.SetAmmo(Exiled.API.Enums.AmmoType.Nato9, (uint) (ChosenPlayer.GetAmmo(Exiled.API.Enums.AmmoType.Nato9) + NineMM));
                                                    RAComEv.Sender.RemoteAdminMessage($"{NineMM} 9.00mm ammo given to \"{ChosenPlayer.Nickname}\"!");
                                                    Player.Get(RAComEv.Arguments[0])?.Broadcast(3, $"You were given {NineMM} of 9.00mm ammo!", Broadcast.BroadcastFlags.Normal);
                                                    return;
                                                }
                                                RAComEv.Sender.RemoteAdminMessage($"Invalid value for ammo count! Value: {RAComEv.Arguments[3]}");
                                                break;
                                            default:
                                                RAComEv.Sender.RemoteAdminMessage($"Please enter \"5\" (5.62mm), \"7\" (7mm), or \"9\" (9mm)!");
                                                break;
                                        }
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 4");
                                break;
                        }
                        break;
                    case "gnade":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.gnade"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (!plugin.Config.EnableCustomGrenadeTime)
                        {
                            RAComEv.Sender.RemoteAdminMessage("You cannot modify grenades as it is disabled!");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 2)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: gnade (frag/flash) (value)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "frag":
                                        if (float.TryParse(RAComEv.Arguments[1].ToLower(), out float value) && value > 0)
                                        {
                                            plugin.Config.FragGrenadeFuseTimer = value;
                                            RAComEv.Sender.RemoteAdminMessage($"Frag grenade fuse timer set to {value}");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Invalid value for fuse timer! Value: {RAComEv.Arguments[1]}");
                                        break;
                                    case "flash":
                                        if (float.TryParse(RAComEv.Arguments[1].ToLower(), out float val) && val > 0)
                                        {
                                            plugin.Config.FlashGrenadeFuseTimer = val;
                                            RAComEv.Sender.RemoteAdminMessage($"Flash grenade fuse timer set to {val}");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Invalid value for fuse timer! Value: {RAComEv.Arguments[1]}");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter either \"frag\" or \"flash\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 3");
                                break;
                        }
                        break;
                    case "infammo":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.infammo") || !RAComEv.Sender.CheckPermission("ct.*"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: infam (clear/list/*/all/(id or name))");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "clear":
                                        foreach (Player Ply in Player.List)
                                        {
                                            if (Ply.ReferenceHub.TryGetComponent(out InfiniteAmmoComponent infComponent))
                                            {
                                                UnityEngine.Object.Destroy(infComponent);
                                            }
                                            PlayersWithInfiniteAmmo.Clear();
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Infinite ammo is cleared from all players now!");
                                        Map.Broadcast(5, "Infinite ammo is cleared from all players now!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    case "list":
                                        if (PlayersWithInfiniteAmmo.Count != 0)
                                        {
                                            string playerLister = "Players with Infinite Ammo on: ";
                                            foreach (ReferenceHub hub in PlayersWithInfiniteAmmo)
                                            {
                                                playerLister += hub.nicknameSync.MyNick + ", ";
                                            }
                                            playerLister = playerLister.Substring(0, playerLister.Count() - 2);
                                            RAComEv.Sender.RemoteAdminMessage(playerLister);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("There are no players currently online with Infinite Ammo on");
                                        break;
                                    case "*":
                                    case "all":
                                        foreach (Player Ply in Player.List)
                                        {
                                            if (!Ply.ReferenceHub.TryGetComponent(out InfiniteAmmoComponent infComponent))
                                            {
                                                Ply.ReferenceHub.gameObject.AddComponent<InfiniteAmmoComponent>();
                                                PlayersWithInfiniteAmmo.Add(Ply.ReferenceHub);
                                            }
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Infinite ammo is on for all players now!");
                                        Map.Broadcast(3, "Everyone has been given infinite ammo!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    default:
                                        Player ChosenPlayer = Player.Get(RAComEv.Arguments[0]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[1]}\" not found");
                                            return;
                                        }
                                        if (!ChosenPlayer.ReferenceHub.TryGetComponent(out InfiniteAmmoComponent inf))
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Infinite ammo enabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Infinite ammo is enabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersWithInfiniteAmmo.Add(ChosenPlayer.ReferenceHub);
                                            ChosenPlayer.ReferenceHub.gameObject.AddComponent<InfiniteAmmoComponent>();
                                        }
                                        else
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Infinite ammo disabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Infinite ammo is disabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersWithInfiniteAmmo.Remove(ChosenPlayer.ReferenceHub);
                                            UnityEngine.Object.Destroy(inf);
                                        }
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 2");
                                break;
                        }
                        break;
                    case "locate":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.locate"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 2)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: locate (xyz/room) (id or name)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "room":
                                        Player ChosenPlayer = Player.Get(RAComEv.Arguments[1]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[1]}\" not found");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Player \"{ChosenPlayer.Nickname}\" is located at room: {ChosenPlayer.CurrentRoom.Name}");
                                        break;
                                    case "xyz":
                                        ChosenPlayer = Player.Get(RAComEv.Arguments[1]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[1]}\" not found");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Player \"{ChosenPlayer.Nickname}\" is located at X: {ChosenPlayer.Position.x}, Y: {ChosenPlayer.Position.y}, Z: {ChosenPlayer.Position.z}");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter either \"room\" or \"xyz\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 2");
                                break;
                        }
                        break;
                    case "nuke":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.nuke"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid syntax! Syntax: nuke (start/stop/instant) (value (if using \"start\"))");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "instant":
                                        Warhead.Start();
                                        Warhead.DetonationTimer = 0.05f;
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter only \"instant\" or \"start (value)\"!");
                                        break;
                                }
                                break;
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "start":
                                        if (!float.TryParse(RAComEv.Arguments[1].ToLower(), out float timer) || (timer >= 143 || timer < 0.05))
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Invalid value for timer: {RAComEv.Arguments[1]}, highest is 142, lowest is 0.05");
                                            return;
                                        }
                                        Warhead.Start();
                                        Warhead.DetonationTimer = timer;
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter only \"start (value)\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 1-2");
                                break;
                        }
                        break;
                    case "prygates":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.prygates"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: prygates (clear/list/*/all/id)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "clear":
                                        PlayersThatCanPryGates.Clear();
                                        RAComEv.Sender.RemoteAdminMessage("Ability to pry gates is cleared from all players now!");
                                        Map.Broadcast(5, "The ability to pry gates is cleared from all players now!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    case "list":
                                        if (PlayersThatCanPryGates.Count != 0)
                                        {
                                            string playerLister = "Players with Pry Gates on: ";
                                            foreach (ReferenceHub hub in PlayersThatCanPryGates)
                                            {
                                                playerLister += hub.nicknameSync.MyNick + ", ";
                                            }
                                            playerLister = playerLister.Substring(0, playerLister.Count() - 2);
                                            RAComEv.Sender.RemoteAdminMessage(playerLister);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("There are no players currently online with Pry Gates on");
                                        break;
                                    case "*":
                                    case "all":
                                        foreach (Player Ply in Player.List)
                                        {
                                            if (!PlayersThatCanPryGates.Contains(Ply.ReferenceHub))
                                                PlayersThatCanPryGates.Add(Ply.ReferenceHub);
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Ability to pry gates is on for all players now!");
                                        Map.Broadcast(5, "Everyone has been given the pry gates ability!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    default:
                                        Player ChosenPlayer = Player.Get(RAComEv.Arguments[0]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[0]}\" not found");
                                            return;
                                        }
                                        if (!PlayersThatCanPryGates.Contains(ChosenPlayer.ReferenceHub))
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Pry gates ability enabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Pry gates ability is enabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersThatCanPryGates.Add(ChosenPlayer.ReferenceHub);
                                        }
                                        else
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Pry gates ability disabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Pry gates ability is disabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersThatCanPryGates.Remove(ChosenPlayer.ReferenceHub);
                                        }
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 2");
                                break;
                        }
                        break;
                    case "regen":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.regen"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }

                        if (RAComEv.Arguments.Count < 1)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Invalid parameters! Syntax: regen (clear/list/*/all/id)");
                            return;
                        }

                        switch (RAComEv.Arguments.Count)
                        {
                            case 1:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "clear":
                                        foreach (Player Ply in Player.List)
                                        {
                                            if (Ply.ReferenceHub.TryGetComponent(out RegenerationComponent rgnComponent))
                                            {
                                                UnityEngine.Object.Destroy(rgnComponent);
                                            }
                                            PlayersWithRegen.Clear();

                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Regeneration is cleared from all players now!");
                                        Map.Broadcast(5, "Regeneration is cleared from all players now!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    case "list":
                                        if (PlayersWithRegen.Count != 0)
                                        {
                                            string playerLister = "Players with Regeneration on: ";
                                            foreach (ReferenceHub hub in PlayersWithRegen)
                                            {
                                                playerLister += hub.nicknameSync.MyNick + ", ";
                                            }
                                            playerLister = playerLister.Substring(0, playerLister.Count() - 2);
                                            RAComEv.Sender.RemoteAdminMessage(playerLister);
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("There are no players currently online with Regeneration on");
                                        break;
                                    case "*":
                                    case "all":
                                        foreach (Player ply in Player.List)
                                        {
                                            if (!ply.ReferenceHub.TryGetComponent(out RegenerationComponent rgnComponent))
                                            {
                                                ply.ReferenceHub.gameObject.AddComponent<RegenerationComponent>();
                                                PlayersWithRegen.Add(ply.ReferenceHub);
                                            }
                                        }
                                        RAComEv.Sender.RemoteAdminMessage("Regeneration is on for all players now!");
                                        Map.Broadcast(5, "Regeneration is on for all players now!", Broadcast.BroadcastFlags.Normal);
                                        break;
                                    case "time":
                                        RAComEv.Sender.RemoteAdminMessage("Missing value for seconds!");
                                        break;
                                    case "value":
                                        RAComEv.Sender.RemoteAdminMessage("Missing value for health!");
                                        break;
                                    default:
                                        Player ChosenPlayer = Player.Get(RAComEv.Arguments[0]);
                                        if (ChosenPlayer == null)
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Player \"{RAComEv.Arguments[0]}\" not found");
                                            return;
                                        }
                                        if (!ChosenPlayer.ReferenceHub.TryGetComponent(out RegenerationComponent rgn))
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Regeneration enabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Regeneration is enabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersWithRegen.Add(ChosenPlayer.ReferenceHub);
                                            ChosenPlayer.ReferenceHub.gameObject.AddComponent<RegenerationComponent>();
                                        }
                                        else
                                        {
                                            RAComEv.Sender.RemoteAdminMessage($"Regeneration disabled for \"{ChosenPlayer.Nickname}\"!");
                                            Player.Get(RAComEv.Arguments[0])?.Broadcast(3, "Regeneration is disabled for you!", Broadcast.BroadcastFlags.Normal);
                                            PlayersWithRegen.Remove(ChosenPlayer.ReferenceHub);
                                            UnityEngine.Object.Destroy(rgn);
                                        }
                                        break;
                                }
                                break;
                            case 2:
                                switch (RAComEv.Arguments[0].ToLower())
                                {
                                    case "time":
                                        if (float.TryParse(RAComEv.Arguments[1].ToLower(), out float rgn_t) && rgn_t > 0)
                                        {
                                            plugin.Config.RegenerationTime = rgn_t;
                                            RAComEv.Sender.RemoteAdminMessage($"Players with regeneration gain health every {rgn_t} seconds!");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Invalid value for regeneration timer! Value: {RAComEv.Arguments[1].ToLower()}");
                                        break;
                                    case "value":
                                        if (float.TryParse(RAComEv.Arguments[1].ToLower(), out float rgn_v) && rgn_v > 0)
                                        {
                                            plugin.Config.RegenerationValue = rgn_v;
                                            RAComEv.Sender.RemoteAdminMessage($"Players with regeneration gain {rgn_v} health every {plugin.Config.RegenerationTime} seconds!");
                                            return;
                                        }
                                        RAComEv.Sender.RemoteAdminMessage($"Invalid value for regeneration healing! Value: {RAComEv.Arguments[1].ToLower()}");
                                        break;
                                    default:
                                        RAComEv.Sender.RemoteAdminMessage("Please enter either \"time\" or \"value\"!");
                                        break;
                                }
                                break;
                            default:
                                RAComEv.Sender.RemoteAdminMessage($"Invalid number of parameters! Value: {RAComEv.Arguments.Count}, Expected 3");
                                break;
                        }
                        break;
                    case "sdecon":
                        RAComEv.IsAllowed = false;
                        if (!RAComEv.Sender.CheckPermission("ct.sdecon"))
                        {
                            RAComEv.Sender.RemoteAdminMessage("You are not authorized to use this command");
                            return;
                        }
                        if (!Map.IsLCZDecontaminated || !WasDeconCommandRun)
                        {
                            RAComEv.Sender.RemoteAdminMessage("Light Contaimnent Zone Decontamination is on!");
                            Map.StartDecontamination();
                            WasDeconCommandRun = true;
                            return;
                        }
                        RAComEv.Sender.RemoteAdminMessage("Light Contaimnent Zone Decontamination is already active!");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Info($"Error handling command: {e}");
                RAComEv.Sender.RemoteAdminMessage("There was an error handling this command, check console for details", false);
                return;
            }
        }

        public void RevivePlayer(Player ply)
        {
            if (ply.Role != RoleType.Spectator) return;

            int num = RandNum.Next(0, 7);
            switch (num)
            {
                case 0:
                    ply.Role = RoleType.NtfCadet;
                    break;
                case 1:
                    if (!IsWarheadDetonated && !IsDecontanimationActivated)
                        ply.Role = RoleType.ClassD;
                    else
                        ply.Role = RoleType.ChaosInsurgency;
                    break;
                case 2:
                    if (!IsWarheadDetonated)
                        ply.Role = RoleType.FacilityGuard;
                    else
                        ply.Role = RoleType.NtfCommander;
                    break;
                case 3:
                    ply.Role = RoleType.NtfLieutenant;
                    break;
                case 4:
                    ply.Role = RoleType.NtfScientist;
                    break;
                case 5:
                    ply.Role = RoleType.ChaosInsurgency;
                    break;
                case 6:
                    if (!IsWarheadDetonated && !IsDecontanimationActivated)
                        ply.Role = RoleType.Scientist;
                    else
                        ply.Role = RoleType.NtfLieutenant;
                    break;
                case 7:
                    ply.Role = RoleType.NtfCommander;
                    break;
            }
        }

        public static void SpawnGrenadeOnPlayer(Player PlayerToSpawnGrenade, bool UseCustomTimer)
        {
            GrenadeManager gm = PlayerToSpawnGrenade.ReferenceHub.gameObject.GetComponent<GrenadeManager>();
            Grenade gnade = UnityEngine.Object.Instantiate(gm.availableGrenades[0].grenadeInstance.GetComponent<Grenade>());
            if (UseCustomTimer)
                gnade.fuseDuration = CreativeToolbox.ConfigRef.Config.GrenadeTimerOnDeath;
            else
                gnade.fuseDuration = 0.01f;
            gnade.FullInitData(gm, PlayerToSpawnGrenade.Position, Quaternion.Euler(gnade.throwStartAngle), gnade.throwLinearVelocityOffset, gnade.throwAngularVelocity);
            NetworkServer.Spawn(gnade.gameObject);
        }

        public IEnumerator<float> SpawnReverseOfWave(List<Player> RespawnedPlayers, bool Chaos)
        {
            List<Vector3> StoredPositions = new List<Vector3>();
            foreach (Player Ply in RespawnedPlayers)
            {
                StoredPositions.Add(Ply.Position);
                if (Chaos)
                    Ply.Role = (RoleType)Enum.Parse(typeof(RoleType), RandNum.Next(11, 13).ToString());
                else
                    Ply.Role = RoleType.ChaosInsurgency;
            }
            yield return Timing.WaitForSeconds(0.2f);
            int index = 0;
            foreach (Player Ply in RespawnedPlayers)
            {
                Ply.Position = StoredPositions[index];
                index++;
            }
            Timing.KillCoroutines(ChaosRespawnHandle);
        }
    }
}
