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

namespace Bibites_Predatory_Obstacle
{
    // startup lol, if it wasnt obv

    [BepInPlugin("Immortality", "Predatory Obstacle", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            new Harmony("Immortality").PatchAll();
        }
    }

    // variables

    public static class GoTowardsHome
    {
        public static float Delay = 5f;
        public static bool ManualDestroy = false;

        public static readonly Color Blue = Color.blue;
        public static readonly Color Purple = new Color(0.5f, 0, 0.5f);
        public static readonly Color White = Color.white;

        public static Dictionary<BibiteBody, float> LastSeenPrey = new Dictionary<BibiteBody, float>();
        public static Dictionary<BibiteBody, BibiteBody> CurrentTarget = new Dictionary<BibiteBody, BibiteBody>();
        public static Dictionary<BibiteBody, BibiteBody> TargetedBy = new Dictionary<BibiteBody, BibiteBody>();
        public static Dictionary<BibiteBody, ZoneSettings> Home = new Dictionary<BibiteBody, ZoneSettings>();

        public static void DetermineZone(BibiteBody bibite)
        {
            if (bibite.gene.speciesTag != "333immortal" || bibite.gene.GetBodyColor() == Blue) return;
            if (Home.ContainsKey(bibite)) return;

            Vector2 pos = bibite.transform.position;
            float simSize = ScenarioIndependentSettings.Instance.SimulationSize.val;
            List<ZoneSettings> allZones = ScenarioSettings.Instance.allZones;

            foreach (ZoneSettings zone in allZones)
            {
                Vector2 zoneCenter = new Vector2(zone.posX.val, zone.posY.val) * simSize;
                float radius = zone.absoluteRadius;
                float distSq = (pos - zoneCenter).sqrMagnitude;

                if (distSq <= radius * radius)
                {
                    Home[bibite] = zone;
                    break;
                }
            }
        }
        public static void SetTarget(BibiteBody predator, BibiteBody prey)
        {
            if (CurrentTarget.TryGetValue(predator, out BibiteBody oldPrey))
            {
                TargetedBy.Remove(oldPrey);
            }
            CurrentTarget[predator] = prey;
            TargetedBy[prey] = predator;
        }
        public static void ClearTarget(BibiteBody predator)
        {
            if (CurrentTarget.TryGetValue(predator, out BibiteBody prey))
            {
                TargetedBy.Remove(prey);
            }
            CurrentTarget.Remove(predator);
        }
        public static void Cleanup()
        {
            LastSeenPrey.Clear();
            CurrentTarget.Clear();
            TargetedBy.Clear();
            Home.Clear();
            ManualDestroy = false;
        }
    }

    // this is just all the code for bibite actions

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.Die))]
    public static class NullifyDeath
    {
        static bool Prefix(BibiteBody __instance, bool swallowed)
        {
            if (__instance.gene.speciesTag != "333immortal") return true;

            if (GoTowardsHome.ManualDestroy) return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(BibiteStatsPanel), "KillCurrentBibite")]
    public static class UnnullifyDeath
    {
        static void Prefix() => GoTowardsHome.ManualDestroy = true;
        static void Postfix() => GoTowardsHome.ManualDestroy = false;
    }

    [HarmonyPatch(typeof(UserActions), "KillHalfBibites")]
    public static class AlsoUnnullifyDeath
    {
        static void Prefix() => GoTowardsHome.ManualDestroy = true;
        static void Postfix() => GoTowardsHome.ManualDestroy = false;
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
            BibiteGenes genes = Cache.genes(__instance);
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
            BibiteGenes genes = Cache.genes(__instance);

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

            Cache.nPlantsInRange(__instance) = 0;
            Cache.nMeatsInRange(__instance) = 0;
            Cache.nCorpsesInRange(__instance) = 0;

            int nBibites = Cache.nBibitesInRange(__instance);
            int closestIndex = -1;
            bool isSpecial = body.gene.GetBodyColor() == GoTowardsHome.Purple;
            bool isMixed = body.gene.GetBodyColor() == GoTowardsHome.White;
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
                    float distance = (body.transform.position - seen.transform.position).sqrMagnitude;
                    float score = (maturity * (maturity + 1)) / (distance * 0.02f + 1f);
                    if (score > Value)
                    {
                        Value = score;
                        closestIndex = i;
                    }
                }
                else
                {
                    float distance = (seen.transform.position - body.transform.position).sqrMagnitude;
                    if (distance < Value)
                    {
                        Value = distance;
                        closestIndex = i;
                    }
                }
            }

            if (closestIndex >= 0)
            {
                __instance.seenBibites[0] = __instance.seenBibites[closestIndex];
                __instance.bibiteWeights[0] = __instance.bibiteWeights[closestIndex];
                Cache.nBibitesInRange(__instance) = 1;
                GoTowardsHome.LastSeenPrey[body] = Time.time;
                GoTowardsHome.SetTarget(body, __instance.seenBibites[0]);
            }
            else
            {
                Cache.nBibitesInRange(__instance) = 0;
                GoTowardsHome.ClearTarget(body);
            }
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateBitePeriod")]
    public static class FixedBitePeriod
    {
        static void Postfix(BibiteMouth __instance)
        {
            BibiteGenes genes = Cache.genes(__instance);
            if (genes.speciesTag == "333immortal") __instance.bitePeriod = 0.05f;
        }
    }

    [HarmonyPatch(typeof(BibiteMouth), "UpdateOrgan")]
    public static class OnlyAlivePreySilly
    {
        static void Prefix(BibiteMouth __instance)
        {
            BibiteGenes genes = Cache.genes(__instance);
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

            Rigidbody2D rb = Cache.rb2d(__instance);
            if (rb == null) return;

            bool isBlue = (__instance.gene.GetBodyColor() == GoTowardsHome.Blue);
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
                    GoTowardsHome.ClearTarget(__instance);
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
            if (pos.sqrMagnitude > maxDistance * maxDistance)
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

    // this is where the bibite's "home" is set up, ensuring they are locked to their initial zone on spawn (if outside of zone, they remain homeless)

    [HarmonyPatch(typeof(BibiteBody), "StartBodyAtGrowthAndNormalize")]
    public static class Zone
    {
        static void Postfix(BibiteBody __instance) => GoTowardsHome.DetermineZone(__instance);
    }

    [HarmonyPatch(typeof(BibiteBody), "StartBody")]
    public static class Zone_Fallback
    {
        static void Postfix(BibiteBody __instance) => GoTowardsHome.DetermineZone(__instance);
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.SaveState))]
    public static class SaveHome
    {
        static void Postfix(BibiteBody __instance, ref JObject __result)
        {
            if (__instance.gene.speciesTag == "333immortal" && __instance.gene.GetBodyColor() != GoTowardsHome.Blue && GoTowardsHome.Home.TryGetValue(__instance, out ZoneSettings zone))
            {
                __result["homeZoneID"] = zone.zoneID;
            }
        }
    }

    [HarmonyPatch(typeof(BibiteBody), nameof(BibiteBody.LoadState))]
    public static class LoadHome
    {
        static void Postfix(BibiteBody __instance, JObject state)
        {
            JToken zoneIdToken = state["homeZoneID"];
            if (zoneIdToken == null) return;

            int zoneId = zoneIdToken.ToObject<int>();
            List<ZoneSettings> allZones = ScenarioSettings.Instance.allZones;

            for (int i = 0; i < allZones.Count; i++)
            {
                if (allZones[i].zoneID == zoneId)
                {
                    GoTowardsHome.Home[__instance] = allZones[i];
                    return;
                }
            }
        }
    }

    // this is cleanup, really important to ensure no memory leaks

    [HarmonyPatch(typeof(MenuInitializer), "Start")]
    public static class Cleanup
    {
        static void Postfix() => GoTowardsHome.Cleanup();
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public static class AlsoCleanup
    {
        static void Prefix() => GoTowardsHome.Cleanup();
    }

    [HarmonyPatch(typeof(BibiteBody), "OnDestroy")]
    public static class EvenMoreCleanup
    {
        static void Postfix(BibiteBody __instance)
        {
            GoTowardsHome.Home.Remove(__instance);
            GoTowardsHome.LastSeenPrey.Remove(__instance);
            GoTowardsHome.ClearTarget(__instance);

            if (GoTowardsHome.TargetedBy.TryGetValue(__instance, out BibiteBody predator))
            {
                GoTowardsHome.CurrentTarget.Remove(predator);
                GoTowardsHome.TargetedBy.Remove(__instance);
            }
        }
    }
}
