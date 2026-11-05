using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    // Editor-only: creates the SkinSet asset and fills its slots from the generated PNG files in
    // Assets/Art/Generated/ by file name. A missing file leaves its slot empty, so the object
    // keeps its built-in sprite. File names (variant slots count up from 0 until a gap):
    //   Grass_0.png, Grass_1.png, ...   GrassDetail_0..   Path_0..   Tree_0..   Rock_0..   Bush_0..
    //   GruntShadow, TankShadow,
    //   SpawnPortal, GoalGate, TowerBody, TowerBase, TowerShadow, Grunt, Tank,
    //   CliffLeft, CliffRight, Projectile, HudPanel, HudButton, Background
    public static class SkinLoader
    {
        // Loads the SkinSet at 'path', or creates it (an existing asset keeps its GUID).
        public static SkinSet LoadOrCreate(string path)
        {
            SkinSet skin = AssetDatabase.LoadAssetAtPath<SkinSet>(path);
            if (skin != null) return skin;
            skin = ScriptableObject.CreateInstance<SkinSet>();
            AssetDatabase.CreateAsset(skin, path);
            return skin;
        }

        // Empties 'skin' and fills every slot whose PNG exists in 'folder'.
        // Returns the number of sprites assigned (0 = the skin changes nothing).
        public static int Fill(SkinSet skin, string folder = PixelArt.GeneratedFolder)
        {
            skin.Clear();
            int count = 0;
            if (AssetDatabase.IsValidFolder(folder))
            {
                skin.grassTiles = LoadVariants(folder, "Grass", ref count);
                skin.pathTiles = LoadVariants(folder, "Path", ref count);
                skin.grassDetails = LoadVariants(folder, "GrassDetail", ref count);
                skin.trees = LoadVariants(folder, "Tree", ref count);
                skin.rocks = LoadVariants(folder, "Rock", ref count);
                skin.bushes = LoadVariants(folder, "Bush", ref count);
                skin.spawnPortal = Load(folder, "SpawnPortal", ref count);
                skin.goalGate = Load(folder, "GoalGate", ref count);
                skin.towerBody = Load(folder, "TowerBody", ref count);
                skin.towerBase = Load(folder, "TowerBase", ref count);
                skin.towerShadow = Load(folder, "TowerShadow", ref count);
                skin.grunt = Load(folder, "Grunt", ref count);
                skin.tank = Load(folder, "Tank", ref count);
                skin.gruntShadow = Load(folder, "GruntShadow", ref count);
                skin.tankShadow = Load(folder, "TankShadow", ref count);
                skin.cliffLeft = Load(folder, "CliffLeft", ref count);
                skin.cliffRight = Load(folder, "CliffRight", ref count);
                skin.projectile = Load(folder, "Projectile", ref count);
                skin.hudPanel = Load(folder, "HudPanel", ref count);
                skin.hudButton = Load(folder, "HudButton", ref count);
                skin.background = Load(folder, "Background", ref count);
            }
            EditorUtility.SetDirty(skin);
            return count;
        }

        private static Sprite Load(string folder, string name, ref int count)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
            if (sprite != null) count++;
            return sprite;
        }

        private static Sprite[] LoadVariants(string folder, string prefix, ref int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; ; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{prefix}_{i}.png");
                if (sprite == null) break;
                list.Add(sprite);
            }
            count += list.Count;
            return list.ToArray();
        }
    }
}
