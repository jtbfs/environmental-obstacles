using HarmonyLib;
using SimulationScripts.BibiteScripts;
using UnityEngine;

namespace Bibites_Predatory_Obstacle
{
    internal class Cache
    {
        public static readonly AccessTools.FieldRef<FieldOfView, int> nBibitesInRange =
            AccessTools.FieldRefAccess<FieldOfView, int>("nBibitesInRange");

        public static readonly AccessTools.FieldRef<FieldOfView, int> nPlantsInRange =
            AccessTools.FieldRefAccess<FieldOfView, int>("nPlantsInRange");

        public static readonly AccessTools.FieldRef<FieldOfView, int> nMeatsInRange =
            AccessTools.FieldRefAccess<FieldOfView, int>("nMeatsInRange");

        public static readonly AccessTools.FieldRef<FieldOfView, int> nCorpsesInRange =
            AccessTools.FieldRefAccess<FieldOfView, int>("nCorpsesInRange");

        public static readonly AccessTools.FieldRef<BibiteBody, Rigidbody2D> rb2d =
            AccessTools.FieldRefAccess<BibiteBody, Rigidbody2D>("rb2d");

        public static readonly AccessTools.FieldRef<BibiteOrgan, BibiteGenes> cGenes =
            AccessTools.FieldRefAccess<BibiteOrgan, BibiteGenes>("genes");
    }
}
