using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using ManagementScripts;
using Newtonsoft.Json.Linq;
using PropertiesScripts;
using SettingScripts;
using SimulationScripts;
using SimulationScripts.BibiteScripts;
using UIScripts;
using UnityEngine;
using static Bibites_Predatory_Obstacle.Variables;
using static Bibites_Predatory_Obstacle.Cache;

namespace Bibites_Predatory_Obstacle
{

    [BepInPlugin("Immortality", "Predatory Obstacle", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            new Harmony("Immortality").PatchAll();
        }
    }

    public static class Variables
    {
        public static float SimSize = 1;
        public static float Threshold = 1;
        public static float ThresholdSquare = 1;
        public static List<ZoneSettings> AllZones;
        public static bool SimSizeCheck = false;
        public static bool ManualDestroy = false;

        public static readonly Color Blue = Color.blue;
        public static readonly Color Purple = new Color(0.5f, 0, 0.5f);
        public static readonly Color White = Color.white;
        public sealed class BibiteData
        {
            public BibiteType ColorType;
            public ZoneSettings Home;
            public bool Homeless = false;
            public BibiteBody CurrentTarget;
            public float LastSeenPrey;
            public Rigidbody2D Rigidbody;
            public Transform PredatorTransform;
        }
        public static readonly Dictionary<BibiteBody, HashSet<BibiteBody>> TargetedBy = new Dictionary<BibiteBody, HashSet<BibiteBody>>();
        public static readonly Dictionary<BibiteBody, BibiteData> Data = new Dictionary<BibiteBody, BibiteData>();
        public static readonly Stack<HashSet<BibiteBody>> Pool = new Stack<HashSet<BibiteBody>>();
        public enum BibiteType : byte
        {
            Default,
            Rammer,
            Witch,
            Mixed
        }

        public static HashSet<BibiteBody> GetSet()
        {
            return Pool.Count > 0 ? Pool.Pop() : new HashSet<BibiteBody>();
        }

        public static void ReturnSet(HashSet<BibiteBody> set)
        {
            set.Clear();
            Pool.Push(set);
        }
        public static void CreateData(BibiteBody bibite, bool assignZone = true)
        {
            if (bibite.gene.speciesTag != "333immortal") return;
            Color color = bibite.gene.GetBodyColor();

            BibiteData data = new BibiteData
            {
                ColorType = BibiteType.Default,
                Rigidbody = rb2d(bibite),
                PredatorTransform = bibite.transform
            };

            if (color == Blue) data.ColorType = BibiteType.Rammer;
            else if (color == Purple) data.ColorType = BibiteType.Witch;
            else if (color == White) data.ColorType = BibiteType.Mixed;

            Data[bibite] = data;

            CreateSimData();

            if (data.ColorType == BibiteType.Rammer || data.Home != null || data.Homeless || !assignZone) return;

            Vector2 pos = data.PredatorTransform.position;

            if (AllZones == null) return;

            foreach (ZoneSettings zone in AllZones)
            {
                Vector2 zoneCenter = new Vector2(zone.posX.val, zone.posY.val) * SimSize;
                float radius = zone.absoluteRadius;
                float distSq = (pos - zoneCenter).sqrMagnitude;

                if (distSq <= radius * radius)
                {
                    data.Home = zone;
                    break;
                }
            }
        }
        public static void CreateSimData()
        {
            if (SimSizeCheck) return;
            SimSize = ScenarioIndependentSettings.Instance.SimulationSize.val;
            Threshold = SimSize * 1.5f;
            ThresholdSquare = Threshold * Threshold;
            AllZones = ScenarioSettings.Instance.allZones;
            SimSizeCheck = true;
        }
        public static void SetTarget(BibiteBody predator, BibiteBody prey)
        {
            ClearTarget(predator);
            BibiteData data = Data[predator];
            data.CurrentTarget = prey;

            if (!TargetedBy.TryGetValue(prey, out HashSet<BibiteBody> predators))
            {
                predators = GetSet();
                TargetedBy[prey] = predators;
            }
            predators.Add(predator);
        }
        public static void ClearTarget(BibiteBody predator)
        {
            BibiteData predatorData = Data[predator];
            BibiteBody prey = predatorData.CurrentTarget;
            if (prey == null) return;
            predatorData.CurrentTarget = null;

            if (TargetedBy.TryGetValue(prey, out HashSet<BibiteBody> predators))
            {
                predators.Remove(predator);
                if (predators.Count == 0)
                {
                    TargetedBy.Remove(prey);
                    ReturnSet(predators);
                }
            }
        }
        public static void Cleanup()
        {
            SimSizeCheck = false;
            ManualDestroy = false;
            Data.Clear();
            foreach (var set in TargetedBy.Values) ReturnSet(set);
            TargetedBy.Clear();
            foreach (var set in Pool) set.Clear();
            Pool.Clear();
            AllZones = null;
            SimSize = 1;
            Threshold = 1;
            ThresholdSquare = 1;
        }
    }

    // this is just all the code for bibite actions

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Die))]
    public static class NullifyDeath
    {
        static bool Prefix(BibiteBody __instance, bool swallowed)
        {
            if (__instance.gene.speciesTag != "333immortal") return true;

            if (ManualDestroy) return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(BibiteStatsPanel), "KillCurrentBibite")]
    public static class UnnullifyDeath
    {
        static void Prefix() => ManualDestroy = true;
        static void Postfix() => ManualDestroy = false;
    }

    [HarmonyPatch(typeof(UserActions), "KillHalfBibites")]
    public static class AlsoUnnullifyDeath
    {
        static void Prefix() => ManualDestroy = true;
        static void Postfix() => ManualDestroy = false;
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Hurting))]
    public static class NullifyDamage
    {
        static bool Prefix(BibiteBody __instance, ref float __result)
        {
            if (__instance.gene.speciesTag == "333immortal" && !__instance.dying && !__instance.dead)
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Attack))]
    public static class NullifyAttack
    {
        static bool Prefix(BibiteBody __instance)
        {
            return __instance.gene.speciesTag != "333immortal" || __instance.dying || __instance.dead;
        }
    }

    [HarmonyPatch(typeof(BibiteBody), "UseEnergy")]
    public static class NullifyEnergyLoss
    {
        static bool Prefix(BibiteBody __instance)
        {
            return __instance.gene.speciesTag != "333immortal" || __instance.dying || __instance.dead;
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.age), MethodType.Getter)]
    public static class NullifyAge
    {
        static bool Prefix(BibiteBody __instance, ref float __result)
        {
            if (__instance.gene.speciesTag == "333immortal")
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "TryGrabTarget")]
    public static class NullifyGrabUselessMatter
    {
        static bool Prefix(BibiteMouth __instance, GrabbableObject targetToGrab)
        {
            BibiteGenes genes = cGenes(__instance);
            BibiteBody targetBody = targetToGrab.GetComponentInParent<BibiteBody>();

            if (targetBody != null && targetBody.gene.speciesTag == "333immortal" && !targetBody.dying && !targetBody.dead) return false;

            if (genes.speciesTag == "333immortal")
            {
                if (targetToGrab.GetComponent<MatterPellet>() != null) return false;
                if (targetBody == null) return false;
                if (targetBody.dead || targetBody.dying) return false;
                targetBody.Die(false);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "TrySwallowTarget")]
    public static class NullifyEatUselessMatter
    {
        static bool Prefix(BibiteMouth __instance, GameObject target, ref bool __result)
        {
            BibiteGenes genes = cGenes(__instance);

            if (genes.speciesTag == "333immortal")
            {
                MatterPellet pellet = target.GetComponent<MatterPellet>();

                if (pellet != null)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(FieldOfView), nameof(FieldOfView.FindSeenEntities))]
    public static class BestPreyOnly
    {
        static void Postfix(FieldOfView __instance)
        {
            BibiteBody body = __instance.GetComponent<BibiteBody>();
            if (body.gene.speciesTag != "333immortal") return;

            if (!Data.TryGetValue(body, out BibiteData data)) CreateData(body);

            int nBibites = nBibitesInRange(__instance);
            int closestIndex = -1;
            Vector2 bodyPos = data.PredatorTransform.position;
            var type = data.ColorType;
            bool isSpecial = type == BibiteType.Witch;
            bool isMixed = type == BibiteType.Mixed;
            float Value = float.MaxValue;
            if (isSpecial || isMixed) Value = float.MinValue;

            for (int i = 0; i < nBibites; i++)
            {
                BibiteBody seen = __instance.seenBibites[i];
                if (seen == null || seen.dead || seen.dying || seen.gene.speciesTag == "333immortal") continue;

                if (isSpecial)
                {
                    float maturity = seen.growth.maturity;
                    if (maturity > Value)
                    {
                        Value = maturity;
                        closestIndex = i;
                    }
                }
                else if (isMixed)
                {
                    float maturity = seen.growth.maturity;
                    Vector2 seenPos = seen.transform.position;
                    float distance = (bodyPos - seenPos).sqrMagnitude;
                    float score = (maturity * (maturity + 1)) / (distance * 0.02f + 1f);
                    if (score > Value)
                    {
                        Value = score;
                        closestIndex = i;
                    }
                }
                else
                {
                    Vector2 seenPos = seen.transform.position;
                    float distance = (seenPos - bodyPos).sqrMagnitude;
                    if (distance < Value)
                    {
                        Value = distance;
                        closestIndex = i;
                    }
                }
            }

            if (closestIndex >= 0)
            {
                data.LastSeenPrey = Time.fixedTime;
                BibiteBody newTarget = __instance.seenBibites[closestIndex];
                if (data.CurrentTarget != newTarget) SetTarget(body, newTarget);
            }
            else ClearTarget(body);
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateBitePeriod")]
    public static class FixedBitePeriod
    {
        static void Postfix(BibiteMouth __instance)
        {
            BibiteGenes genes = cGenes(__instance);
            if (genes.speciesTag == "333immortal") __instance.bitePeriod = 0.05f;
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateOrgan")]
    public static class OnlyAlivePreySilly
    {
        static void Prefix(BibiteMouth __instance)
        {
            BibiteGenes genes = cGenes(__instance);
            if (genes.speciesTag != "333immortal") return;
            if (__instance.nHeld == 0) return;

            for (int i = __instance.nHeld - 1; i >= 0; i--)
            {
                var link = __instance.links[i];
                if (link == null || link.connectedBody == null) continue;

                BibiteBody heldBody = link.connectedBody.GetComponentInParent<BibiteBody>();
                if (heldBody != null && (heldBody.dead || heldBody.dying))
                {
                    __instance.ReleaseGrabbed(heldBody.gameObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(BibiteBody), "FixedUpdate")]
    public static class Center
    {
        static void Postfix(BibiteBody __instance)
        {
            if (__instance.gene.speciesTag != "333immortal") return;

            if (!Data.TryGetValue(__instance, out BibiteData data)) CreateData(__instance);

            Rigidbody2D rb = data.Rigidbody;
            if (rb == null) return;

            bool isBlue = data.ColorType == BibiteType.Rammer;
            if (!isBlue)
            {
                BibiteBody target = data.CurrentTarget;
                if (target != null
                    && !target.dead
                    && !target.dying)
                {
                    Vector2 dir = (target.transform.position - data.PredatorTransform.position).normalized;
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    rb.MoveRotation(targetAngle);
                    return;
                }
                else
                {
                    ClearTarget(__instance);
                }
                ZoneSettings homeZone = data.Home;
                if (homeZone != null)
                {
                    Vector2 zoneCenter = new Vector2(homeZone.posX.val, homeZone.posY.val) * SimSize;
                    float radius = homeZone.absoluteRadius;
                    Vector2 toCenter = zoneCenter - (Vector2)data.PredatorTransform.position;

                    if (toCenter.sqrMagnitude > radius * radius)
                    {
                        Vector2 HomeDesired = toCenter.normalized;
                        float targetAngle = Mathf.Atan2(HomeDesired.y, HomeDesired.x) * Mathf.Rad2Deg - 90f;
                        rb.MoveRotation(targetAngle);
                        return;
                    }
                }

                if (data.LastSeenPrey == 0)
                {
                    data.LastSeenPrey = Time.fixedTime;
                    return;
                }
                float lastTime = data.LastSeenPrey;
                if (Time.fixedTime - lastTime < 5) return;
            }

            Vector2 pos = rb.position;
            float posSquare = pos.sqrMagnitude;
            if (posSquare < 0.01f) return;

            if (posSquare > ThresholdSquare)
            {
                Vector2 fixdesired = -pos.normalized;
                float targetAngle = Mathf.Atan2(fixdesired.y, fixdesired.x) * Mathf.Rad2Deg - 90f;
                rb.MoveRotation(targetAngle);
                return;
            }

            Vector2 lineDir = pos.normalized;

            Vector2 currentForward = rb.transform.up;

            Vector2 desired =
                Vector2.Dot(currentForward, lineDir) >= 0f
                    ? lineDir
                    : -lineDir;

            float centerAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg - 90f;
            rb.MoveRotation(centerAngle);
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.SaveState))]
    public static class SaveHome
    {
        static void Postfix(BibiteBody __instance, ref JObject __result)
        {
            if (__instance.gene.speciesTag == "333immortal")
            {
                if (Data.TryGetValue(__instance, out BibiteData data))
                {
                    if (data.ColorType != BibiteType.Rammer)
                    {
                        if (data.Home != null) __result["homeZoneID"] = data.Home.zoneID;
                        else __result["homeZoneID"] = -1;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.LoadState))]
    public static class LoadHome
    {
        static void Postfix(BibiteBody __instance, JObject state)
        {
            CreateSimData();
            if (__instance.gene.speciesTag != "333immortal") return;
            JToken zoneIdToken = state["homeZoneID"];
            if (zoneIdToken == null) return;

            if (!Data.TryGetValue(__instance, out BibiteData data))
            {
                CreateData(__instance, false);
                data = Data[__instance];
            }

            int zoneId = zoneIdToken.ToObject<int>();
            if (zoneId == -1)
            {
                data.Homeless = true;
                data.Home = null;
                return;
            }
            for (int i = 0; i < AllZones.Count; i++)
            {
                if (AllZones[i].zoneID == zoneId)
                {
                    data.Home = AllZones[i];
                    return;
                }
            }
        }
    }

    // this is cleanup, really important to ensure no memory leaks

    [HarmonyPatch(typeof(MenuInitializer), "Start")]
    public static class MenuCleanup
    {
        static void Postfix() => Cleanup();
    }

    [HarmonyPatch(typeof(SimulationManager), "Start")]
    public static class AlsoCleanup
    {
        static void Prefix() => Cleanup();

        static void Postfix() => CreateSimData();
    }

    [HarmonyPatch(typeof(BibiteBody), "OnDestroy")]
    public static class EvenMoreCleanup
    {
        static void Postfix(BibiteBody __instance)
        {
            if (Data.TryGetValue(__instance, out BibiteData data))
            {
                ClearTarget(__instance);
                Data.Remove(__instance);
            }

            if (TargetedBy.TryGetValue(__instance, out HashSet<BibiteBody> predators))
            {
                foreach (BibiteBody predator in predators)
                {
                    if (Data.TryGetValue(predator, out BibiteData predatorData))
                    {
                        predatorData.CurrentTarget = null;
                    }
                }
                TargetedBy.Remove(__instance);
                ReturnSet(predators);
            }
        }
    }
}
