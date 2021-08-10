

namespace Mh.MapleLib.WzLib.Util
{
    public static class GameUtil
    {
        // #region 装扮
        //
        // private static readonly Dictionary<int, Clothing> ClothCache = new Dictionary<int, Clothing>();
        //
        // public static Clothing GetCloth(int clothId, BodyDrawInfo drawInfo)
        // {
        //     if (!ClothCache.ContainsKey(clothId))
        //         ClothCache[clothId] = new Clothing(clothId, drawInfo);
        //     return ClothCache[clothId];
        // }

        // #endregion
        //
        // #region 身体
        //
        // private static readonly Dictionary<int, Body> BodyCache = new Dictionary<int, Body>();
        //
        // public static Body GetBody(int bodyId, BodyDrawInfo drawInfo)
        // {
        //     if (!BodyCache.ContainsKey(bodyId))
        //         BodyCache[bodyId] = new Body(bodyId, drawInfo);
        //     return BodyCache[bodyId];
        // }
        //
        // #endregion
        //
        // #region 头发
        //
        // private static readonly Dictionary<int, Hair> HairCache = new Dictionary<int, Hair>();
        //
        // public static Hair GetHair(int hairId, BodyDrawInfo drawInfo)
        // {
        //     if (!HairCache.ContainsKey(hairId))
        //         HairCache[hairId] = new Hair(hairId, drawInfo);
        //     return HairCache[hairId];
        // }
        //
        // #endregion
        //
        // #region 脸型
        //
        // private static readonly Dictionary<int, Face> FaceCache = new Dictionary<int, Face>();
        //
        // public static Face GetFace(int faceId)
        // {
        //     if (!FaceCache.ContainsKey(faceId))
        //         FaceCache[faceId] = new Face(faceId);
        //     return FaceCache[faceId];
        // }
        //
        // #endregion
        //
        // #region 武器
        //
        // private static readonly Dictionary<int, WeaponData> WeaponDataDictionary = new Dictionary<int, WeaponData>();
        //
        // public static WeaponData GetWeaponData(int weaponId)
        // {
        //     if (!WeaponDataDictionary.ContainsKey(weaponId))
        //         WeaponDataDictionary[weaponId] = new WeaponData(weaponId);
        //     return WeaponDataDictionary[weaponId];
        // }
        //
        // #endregion
        //
        // #region 物品
        //
        // private static readonly Dictionary<int, EquipData> EquipDataDictionary = new Dictionary<int, EquipData>();
        //
        // public static EquipData GetEquipData(int itemId)
        // {
        //     if (!EquipDataDictionary.ContainsKey(itemId))
        //         EquipDataDictionary[itemId] = new EquipData(itemId);
        //     return EquipDataDictionary[itemId];
        // }
        //
        // #endregion
        //
        // #region 技能数据
        //
        // private static readonly Dictionary<int, SkillData> SkillDataDictionary = new Dictionary<int, SkillData>();
        //
        // public static SkillData GetSkillData(int skillId)
        // {
        //     if (!SkillDataDictionary.ContainsKey(skillId))
        //         SkillDataDictionary[skillId] = new SkillData(skillId);
        //     return SkillDataDictionary[skillId];
        // }
        //
        // #endregion

        /**
         * 游戏每步ms
         */
        public const short TimeStep = 8;

        public static short ViewWidth = 800;

        public static short ViewHeight = 600;
    }
}