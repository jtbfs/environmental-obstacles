using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using ManagementScripts;
using PropertiesScripts;
using SettingScripts;
using SimulationScripts;
using SimulationScripts.BibiteScripts;
using UnityEngine;

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

    public static class GoTowardsHome
    {
        public static float Delay = 5f;

        public static Dictionary<BibiteBody, float> LastSeenPrey = new Dictionary<BibiteBody, float>();

        public static Dictionary<BibiteBody, BibiteBody> CurrentTarget = new Dictionary<BibiteBody, BibiteBody>();

        public static Dictionary<BibiteBody, ZoneSettings> Home = new Dictionary<BibiteBody, ZoneSettings>();
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Die))]
    public static class NullifyDeath
    {
        static bool Prefix(BibiteBody __instance, bool swallowed)
        {
            return __instance.gene.speciesTag != "333immortal";
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Hurting))]
    public static class NullifyDamage
    {
        static bool Prefix(BibiteBody __instance, ref float __result)
        {
            if (__instance.gene.speciesTag == "333immortal")
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
            return __instance.gene.speciesTag != "333immortal";
        }
    }

    [HarmonyPatch(typeof(BibiteBody), "UseEnergy")]
    public static class NullifyEnergyLoss
    {
        static bool Prefix(BibiteBody __instance)
        {
            return __instance.gene.speciesTag != "333immortal";
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
        public static readonly AccessTools.FieldRef<BibiteOrgan, BibiteGenes> GenesRef = AccessTools.FieldRefAccess<BibiteOrgan, BibiteGenes>("genes");

        static bool Prefix(BibiteMouth __instance, GrabbableObject targetToGrab)
        {
            BibiteGenes genes = GenesRef(__instance);
            BibiteBody targetBody = targetToGrab.GetComponentInParent<BibiteBody>();

            if (targetBody != null && targetBody.gene.speciesTag == "333immortal") return false;

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
        public static readonly AccessTools.FieldRef<BibiteOrgan, BibiteGenes> GenesRef = AccessTools.FieldRefAccess<BibiteOrgan, BibiteGenes>("genes");
        static bool Prefix(BibiteMouth __instance, GameObject target, ref bool __result)
        {
            BibiteGenes genes = GenesRef(__instance);

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
    public static class ClosestPreyOnly
    {
        static void Postfix(FieldOfView __instance)
        {
            BibiteBody body = __instance.GetComponent<BibiteBody>();
            if (body == null || body.gene.speciesTag != "333immortal") return;

            Traverse t = Traverse.Create(__instance);

            t.Field("nPlantsInRange").SetValue(0);
            t.Field("nMeatsInRange").SetValue(0);
            t.Field("nCorpsesInRange").SetValue(0);

            int nBibites = t.Field("nBibitesInRange").GetValue<int>();
            int closestIndex = -1;
            bool isSpecial = body.gene.GetBodyColor() == new Color(0.5f, 0, 0.5f);
            bool isMixed = body.gene.GetBodyColor() == new Color(1, 1, 1);
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
                    float dist = Vector2.Distance(body.transform.position, seen.transform.position);
                    float score = (maturity * (maturity + 1)) / ((dist * 0.75f) + 1f);
                    if (score > Value)
                    {
                        Value = score;
                        closestIndex = i;
                    }
                }
                else
                {
                    float dSq = (seen.transform.position - __instance.transform.position).sqrMagnitude;
                    if (dSq < Value)
                    {
                        Value = dSq;
                        closestIndex = i;
                    }
                }
            }

            if (closestIndex >= 0)
            {
                __instance.seenBibites[0] = __instance.seenBibites[closestIndex];
                __instance.bibiteWeights[0] = __instance.bibiteWeights[closestIndex];
                t.Field("nBibitesInRange").SetValue(1);
                GoTowardsHome.LastSeenPrey[body] = Time.time;
                GoTowardsHome.CurrentTarget[body] = __instance.seenBibites[0];
            }
            else
            {
                t.Field("nBibitesInRange").SetValue(0);
                GoTowardsHome.CurrentTarget.Remove(body);
            }
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateBitePeriod")]
    public static class FixedBitePeriod
    {
        public static readonly AccessTools.FieldRef<BibiteOrgan, BibiteGenes> GenesRef = AccessTools.FieldRefAccess<BibiteOrgan, BibiteGenes>("genes");
        static void Postfix(BibiteMouth __instance)
        {
            BibiteGenes genes = GenesRef(__instance);
            if (genes.speciesTag == "333immortal") __instance.bitePeriod = 0.05f;
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateOrgan")]
    public static class OnlyAlivePreySilly
    {
        public static readonly AccessTools.FieldRef<BibiteOrgan, BibiteGenes> GenesRef = AccessTools.FieldRefAccess<BibiteOrgan, BibiteGenes>("genes");

        static void Prefix(BibiteMouth __instance)
        {
            BibiteGenes genes = GenesRef(__instance);
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
        public static readonly AccessTools.FieldRef<BibiteBody, Rigidbody2D> Rb2dRef = AccessTools.FieldRefAccess<BibiteBody, Rigidbody2D>("rb2d");

        static void Postfix(BibiteBody __instance)
        {
            if (__instance.gene == null || __instance.gene.speciesTag != "333immortal") return;

            Rigidbody2D rb = Rb2dRef(__instance);
            if (rb == null) return;

            bool isBlue = (__instance.gene.GetBodyColor() == new Color(0, 0, 1));
            if (!isBlue)
            {
                if (GoTowardsHome.CurrentTarget.TryGetValue(__instance, out BibiteBody target)
                    && target != null
                    && !target.dead
                    && !target.dying)
                {
                    Vector2 dir = (target.transform.position - __instance.transform.position).normalized;
                    float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    rb.MoveRotation(targetAngle);
                    return;
                }
                else
                {
                    GoTowardsHome.CurrentTarget.Remove(__instance);
                }

                if (GoTowardsHome.Home.TryGetValue(__instance, out ZoneSettings homeZone))
                {
                    float simSize = ScenarioIndependentSettings.Instance.SimulationSize.val;
                    Vector2 zoneCenter = new Vector2(homeZone.posX.val, homeZone.posY.val) * simSize;
                    float radius = homeZone.absoluteRadius;
                    Vector2 toCenter = zoneCenter - (Vector2)__instance.transform.position;

                    if (toCenter.sqrMagnitude > radius * radius)
                    {
                        Vector2 HomeDesired = toCenter.normalized;
                        float targetAngle = Mathf.Atan2(HomeDesired.y, HomeDesired.x) * Mathf.Rad2Deg - 90f;
                        rb.MoveRotation(targetAngle);
                        return;
                    }
                }

                if (!GoTowardsHome.LastSeenPrey.TryGetValue(__instance, out float lastTime))
                {
                    GoTowardsHome.LastSeenPrey[__instance] = Time.time;
                    return;
                }
                if (Time.time - lastTime < GoTowardsHome.Delay) return;
            }

            Vector2 pos = rb.position;
            if (pos.sqrMagnitude < 0.01f) return;

            float maxDistance = ScenarioIndependentSettings.Instance.SimulationSize.val * 1.5f;
            if (isBlue && pos.sqrMagnitude > maxDistance * maxDistance)
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

    [HarmonyPatch(typeof(BibiteBody), "StartBodyAtGrowthAndNormalize")]
    public static class DetermineZone
    {
        static void Postfix(BibiteBody __instance)
        {
            if (__instance.gene == null || __instance.gene.speciesTag != "333immortal" || __instance.gene.GetBodyColor() == new Color(0, 0, 1)) return;
            if (GoTowardsHome.Home.ContainsKey(__instance)) return;

            Vector2 pos = __instance.transform.position;
            float simSize = ScenarioIndependentSettings.Instance.SimulationSize.val;
            List<ZoneSettings> allZones = ScenarioSettings.Instance.allZones;

            foreach (ZoneSettings zone in allZones)
            {
                Vector2 zoneCenter = new Vector2(zone.posX.val, zone.posY.val) * simSize;
                float radius = zone.absoluteRadius;
                float distSq = (pos - zoneCenter).sqrMagnitude;

                if (distSq <= radius * radius)
                {
                    GoTowardsHome.Home[__instance] = zone;
                    break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(MenuInitializer), "Start")]
    public static class Cleanup
    {
        static void Postfix()
        {
            GoTowardsHome.LastSeenPrey.Clear();
            GoTowardsHome.CurrentTarget.Clear();
            GoTowardsHome.Home.Clear();
        }
    }
}
