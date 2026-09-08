using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BarbosinaStory
{
    // ============================================================  
    // Патч на стартовых пешек сценария "Барбосина: Дипломатический крах".  
    //  
    // Метод StartingPawnUtility.NewGeneratedStartingPawn(int index) вызывается  
    // для каждого стартового слота, в том числе при "перекатать" на экране  
    // ConfigureStartingPawns. Если сигнатура в твоей версии игры другая,  
    // Harmony напишет в Player.log ошибку "Method not found".  
    //  
    // Что делает патч (только для сценария BarbosinaStory_Crash):  
    //  - для слотов 0..5 ставит имя, пол, черты, навыки, страсти, биографии;  
    //  - всем шестерым принудительно ставит мужской пол и чинит тело/голову,  
    //    чтобы модель не оставалась женской или пустой;  
    //  - чистит все случайные родственные и социальные связи;  
    //  - Фостера (слот 1) и Акаси (слот 2) делает геями и любовниками;  
    //  - после правок сбрасывает графику и портрет, чтобы превью не было пустым.  
    // ============================================================  

    [StaticConstructorOnStartup]
    public static class BarbosinaPawnPresetsInit
    {
        static BarbosinaPawnPresetsInit()
        {
            var harmony = new Harmony("local.barbosinastory.pawnpresets");
            harmony.PatchAll();
            Log.Message("[BarbosinaStory] Pawn preset patch загружен.");
        }
    }

    public class BarbosinaCharacterPreset
    {
        public string firstName;
        public string nickName;
        public string lastName;
        public string childBackstoryDefName;
        public string adultBackstoryDefName;
        public List<(string traitDefName, int degree)> traits;
        public Dictionary<string, int> skillLevels; // ключ — defName SkillDef, значение 0..20  
        public Dictionary<string, Passion> passions;

        public BarbosinaCharacterPreset(string first, string nick, string last,
            string childBs, string adultBs,
            List<(string, int)> traits,
            Dictionary<string, int> skills,
            Dictionary<string, Passion> passions = null)
        {
            firstName = first; nickName = nick; lastName = last;
            childBackstoryDefName = childBs; adultBackstoryDefName = adultBs;
            this.traits = traits;
            skillLevels = skills;
            this.passions = passions ?? new Dictionary<string, Passion>();
        }
    }

    public static class BarbosinaPresetData
    {
        public const string ScenarioDefName = "BarbosinaStory_Crash";

        // Слоты пары любовников  
        public const int FosterIndex = 1;
        public const int AkasiIndex = 2;

        // Индекс в списке = индекс стартового слота (0..5).  
        public static readonly List<BarbosinaCharacterPreset> Characters = new List<BarbosinaCharacterPreset>
        {  
            // 0: Кумар — Кумаростан. Нервный и хрупкий слабак.  
            new BarbosinaCharacterPreset(
                "Кумар", "Кумар", "Кумаростан",
                "BB_KumarChild", "BB_KumarAdult",
                new List<(string, int)> { ("Wimp", 0), ("Nerves", -1) },
                new Dictionary<string, int> {
                    {"Social", 7}, {"Intellectual", 5}, {"Shooting", 3}, {"Melee", 2}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major} }
            ),  
  
            // 1: Фостер — Солнечное королевство. Обаятельный, но толку мало.  
            new BarbosinaCharacterPreset(
                "Фостер", "Фостер", "Фостерия",
                "BB_FosterChild", "BB_FosterAdult",
                new List<(string, int)> { ("Gay", 0), ("NaturalMood", 2), ("Beauty", 1) },
                new Dictionary<string, int> {
                    {"Social", 8}, {"Artistic", 5}, {"Construction", 2}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major}, {"Artistic", Passion.Minor} }
            ),  
  
            // 2: Акаси — апостол и любовник Фостера. Слабый, но что-то умеет.  
            new BarbosinaCharacterPreset(
                "Акаси", "Акаси", "Фостерия",
                "BB_AkasiChild", "BB_AkasiAdult",
                new List<(string, int)> { ("Gay", 0), ("Nerves", 2) },
                new Dictionary<string, int> {
                    {"Intellectual", 6}, {"Medicine", 4}, {"Social", 5}
                },
                new Dictionary<string, Passion> { {"Intellectual", Passion.Minor}, {"Medicine", Passion.Minor} }
            ),  
  
            // 3: Барбос/Барсик — Барсеговина. Сильный универсал.  
            new BarbosinaCharacterPreset(
                "Барбос", "Барсик", "Барсеговина",
                "BB_BarbosChild", "BB_BarbosAdult",
                new List<(string, int)> { ("Nerves", 1), ("Industriousness", 1) },
                new Dictionary<string, int> {
                    {"Social", 7}, {"Shooting", 7}, {"Construction", 6}, {"Melee", 5}, {"Mining", 4}
                },
                new Dictionary<string, Passion> { {"Shooting", Passion.Major}, {"Construction", Passion.Minor} }
            ),  
  
            // 4: Киткат — Respectable. Сильный: умный, социальный, рукастый.  
            new BarbosinaCharacterPreset(
                "Киткат", "Respectable", "",
                "BB_KitkatChild", "BB_KitkatAdult",
                new List<(string, int)> { ("Undergrounder", 0), ("NaturalMood", 1) },
                new Dictionary<string, int> {
                    {"Social", 8}, {"Intellectual", 7}, {"Artistic", 6}, {"Crafting", 5}
                },
                new Dictionary<string, Passion> { {"Intellectual", Passion.Major}, {"Social", Passion.Minor} }
            ),  
  
            // 5: Хмурый / muederatte — Хмуростан. Сильный строитель и шахтёр.  
            new BarbosinaCharacterPreset(
                "Хмурый", "muederatte", "Хмуростан",
                "BB_HmuryChild", "BB_HmuryAdult",
                new List<(string, int)> { ("NaturalMood", -2), ("Industriousness", 2) },
                new Dictionary<string, int> {
                    {"Construction", 9}, {"Mining", 7}, {"Shooting", 6}, {"Intellectual", 6}
                },
                new Dictionary<string, Passion> { {"Construction", Passion.Major}, {"Mining", Passion.Minor} }
            ),
        };
    }

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    public static class Patch_NewGeneratedStartingPawn
    {
        // Ссылки на сгенерированные стартовые пешки по индексу слота,  
        // чтобы связать Фостера и Акаси даже если слоты катаются по отдельности.  
        private static readonly Dictionary<int, Pawn> GeneratedPawns = new Dictionary<int, Pawn>();

        public static void Postfix(int index, ref Pawn __result)
        {
            if (__result == null) return;
            if (Find.Scenario == null) return;

            ScenarioDef scenDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(BarbosinaPresetData.ScenarioDefName);
            if (scenDef == null || Find.Scenario != scenDef.scenario) return;

            if (index < 0 || index >= BarbosinaPresetData.Characters.Count) return;

            ApplyPreset(__result, BarbosinaPresetData.Characters[index]);

            GeneratedPawns[index] = __result;
            TryLinkFosterAkasi();
            RefreshVisuals(__result);
        }

        private static void ApplyPreset(Pawn pawn, BarbosinaCharacterPreset data)
        {
            // Имя  
            pawn.Name = new NameTriple(data.firstName, data.nickName, data.lastName);

            // Пол: всем мужской, с починкой тела и головы  
            ForceMale(pawn);

            // Чистим все случайные связи (родня, бывшие и т.п.).  
            // Единственная намеренная связь — Фостер и Акаси, ставится ниже отдельно.  
            if (pawn.relations != null)
            {
                pawn.relations.ClearAllRelations();
            }

            // Биографии (флейвор; навыки выставляем явно ниже)  
            BackstoryDef childBs = DefDatabase<BackstoryDef>.GetNamedSilentFail(data.childBackstoryDefName);
            BackstoryDef adultBs = DefDatabase<BackstoryDef>.GetNamedSilentFail(data.adultBackstoryDefName);
            if (childBs != null) pawn.story.Childhood = childBs;
            if (adultBs != null) pawn.story.Adulthood = adultBs;

            // Черты: чистим случайные, ставим наши  
            pawn.story.traits.allTraits.Clear();
            foreach (var (traitDefName, degree) in data.traits)
            {
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                if (traitDef == null)
                {
                    Log.Warning($"[BarbosinaStory] TraitDef '{traitDefName}' не найден, пропускаю.");
                    continue;
                }
                pawn.story.traits.GainTrait(new Trait(traitDef, degree, true));
            }

            // Навыки  
            foreach (var kv in data.skillLevels)
            {
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(kv.Key);
                if (skillDef == null)
                {
                    Log.Warning($"[BarbosinaStory] SkillDef '{kv.Key}' не найден, пропускаю.");
                    continue;
                }
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                record.Level = kv.Value;
                if (data.passions.TryGetValue(kv.Key, out Passion passion))
                {
                    record.passion = passion;
                }
            }
        }

        // Принудительно делает пешку мужчиной и чинит тело/голову,  
        // чтобы модель не осталась женской и не пропала.  
        private static void ForceMale(Pawn pawn)
        {
            try
            {
                pawn.gender = Gender.Male;

                if (pawn.story != null)
                {
                    // Тело: если стоит женское, меняем на стандартное мужское  
                    if (pawn.story.bodyType == BodyTypeDefOf.Female)
                    {
                        pawn.story.bodyType = BodyTypeDefOf.Male;
                    }

                    // Голова: если женская или пустая, подбираем мужскую  
                    bool needHead = pawn.story.headType == null || pawn.story.headType.gender == Gender.Female;
                    if (needHead)
                    {
                        var maleHeads = DefDatabase<HeadTypeDef>.AllDefsListForReading
                            .Where(h => h.randomChosen && (h.gender == Gender.Male || h.gender == Gender.None))
                            .ToList();

                        if (!pawn.story.TryGetRandomHeadFromSet(maleHeads) && maleHeads.Count > 0)
                        {
                            pawn.story.headType = maleHeads.RandomElement();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось выставить мужской пол: {e.Message}");
            }
        }

        // Связывает Фостера и Акаси как любовников, если оба слота уже созданы.  
        private static void TryLinkFosterAkasi()
        {
            if (!GeneratedPawns.TryGetValue(BarbosinaPresetData.FosterIndex, out Pawn foster)) return;
            if (!GeneratedPawns.TryGetValue(BarbosinaPresetData.AkasiIndex, out Pawn akasi)) return;
            if (foster == null || akasi == null || foster == akasi) return;
            if (foster.relations == null || akasi.relations == null) return;

            if (foster.relations.DirectRelationExists(PawnRelationDefOf.Lover, akasi)) return;

            foster.relations.AddDirectRelation(PawnRelationDefOf.Lover, akasi);
        }

        // Сброс графики и портрета, чтобы превью пешки не было пустым.  
        private static void RefreshVisuals(Pawn pawn)
        {
            try
            {
                if (pawn.Drawer != null && pawn.Drawer.renderer != null)
                {
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                }
                PortraitsCache.SetDirty(pawn);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось обновить графику пешки: {e.Message}");
            }
        }
    }
}