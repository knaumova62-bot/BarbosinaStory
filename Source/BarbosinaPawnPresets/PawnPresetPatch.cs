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
    // Что делает патч (только для сценария BarbosinaStory_Crash):  
    //  - для слотов 0..5 ставит имя, пол, возраст, черты, навыки, страсти, биографии;  
    //  - все 12 навыков задаются явно, поэтому рероль не меняет статы;  
    //  - страсти заданы поимённо (мажор + пара миноров), остальным навыкам огонька нет;  
    //  - возраст случайный 19..30 лет;  
    //  - всем шестерым принудительно ставит мужской пол и чинит тело/голову;  
    //  - чистит все случайные родственные и социальные связи;  
    //  - Фостера (слот 1) и Акаси (слот 2) делает геями и любовниками;  
    //  - после правок сбрасывает графику и портрет, чтобы превью не было пустым.  
    //  
    // Отдельный патч на Page_ConfigureStartingPawns.PreOpen сразу перегенерит  
    // все шесть слотов при открытии редактора, чтобы не приходилось вручную  
    // жать "случайно".  
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
        public Dictionary<string, int> skillLevels;   // ключ — defName SkillDef, значение 0..20  
        public Dictionary<string, Passion> passions;   // мажоры + осознанные миноры; остальным None  

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

        // Возраст всех шести: случайный в этом диапазоне (включительно)  
        public const int MinAge = 19;
        public const int MaxAge = 30;

        // Индекс в списке = индекс стартового слота (0..5).  
        // Навыки заданы по всем 12 скиллам. В словаре passions перечислены  
        // и мажоры, и выбранные миноры; всем остальным навыкам огонька нет.  
        public static readonly List<BarbosinaCharacterPreset> Characters = new List<BarbosinaCharacterPreset>
        {  
            // 0: Кумар — Кумаростан. Слабак и трус, реально полезен только на грядках и кухне.  
            new BarbosinaCharacterPreset(
                "Кумар", "Кумар", "Кумаростан",
                "BB_KumarChild", "BB_KumarAdult",
                new List<(string, int)> { ("Wimp", 0), ("Nerves", -1) },
                new Dictionary<string, int> {
                    {"Shooting", 0}, {"Melee", 0}, {"Construction", 1}, {"Mining", 0},
                    {"Cooking", 4}, {"Plants", 6}, {"Animals", 2}, {"Crafting", 1},
                    {"Artistic", 1}, {"Medicine", 1}, {"Social", 3}, {"Intellectual", 1}
                },
                new Dictionary<string, Passion> {
                    {"Plants", Passion.Major}, {"Cooking", Passion.Minor}
                }
            ),  
  
            // 1: Фостер — Солнечное королевство. Слабак, но дрессировщик и художник.  
            new BarbosinaCharacterPreset(
                "Фостер", "Фостер", "Фостерия",
                "BB_FosterChild", "BB_FosterAdult",
                new List<(string, int)> { ("Gay", 0), ("NaturalMood", 2), ("Beauty", 1) },
                new Dictionary<string, int> {
                    {"Shooting", 1}, {"Melee", 1}, {"Construction", 0}, {"Mining", 0},
                    {"Cooking", 1}, {"Plants", 1}, {"Animals", 7}, {"Crafting", 1},
                    {"Artistic", 5}, {"Medicine", 0}, {"Social", 4}, {"Intellectual", 1}
                },
                new Dictionary<string, Passion> {
                    {"Animals", Passion.Major}, {"Artistic", Passion.Minor}
                }
            ),  
  
            // 2: Акаси — апостол и любовник Фостера. Слабак, но доктор.  
            new BarbosinaCharacterPreset(
                "Акаси", "Акаси", "Фостерия",
                "BB_AkasiChild", "BB_AkasiAdult",
                new List<(string, int)> { ("Gay", 0), ("Nerves", 2) },
                new Dictionary<string, int> {
                    {"Shooting", 0}, {"Melee", 0}, {"Construction", 1}, {"Mining", 0},
                    {"Cooking", 1}, {"Plants", 1}, {"Animals", 1}, {"Crafting", 1},
                    {"Artistic", 1}, {"Medicine", 7}, {"Social", 4}, {"Intellectual", 1}
                },
                new Dictionary<string, Passion> {
                    {"Medicine", Passion.Major}, {"Social", Passion.Minor}
                }
            ),  
  
            // 3: Барбос/Барсик — Барсеговина. Боевой универсал, средние статки.  
            new BarbosinaCharacterPreset(
                "Барбос", "Барсик", "Барсеговина",
                "BB_BarbosChild", "BB_BarbosAdult",
                new List<(string, int)> { ("Nerves", 1), ("Industriousness", 1) },
                new Dictionary<string, int> {
                    {"Shooting", 5}, {"Melee", 5}, {"Construction", 4}, {"Mining", 3},
                    {"Cooking", 3}, {"Plants", 3}, {"Animals", 3}, {"Crafting", 4},
                    {"Artistic", 3}, {"Medicine", 3}, {"Social", 5}, {"Intellectual", 5}
                },
                new Dictionary<string, Passion> {
                    {"Shooting", Passion.Major},
                    {"Social", Passion.Minor}, {"Intellectual", Passion.Minor}
                }
            ),  
  
            // 4: Киткат — Respectable. Рукастый пропагандист, держит троллефабрику.  
            new BarbosinaCharacterPreset(
                "Киткат", "Respectable", "",
                "BB_KitkatChild", "BB_KitkatAdult",
                new List<(string, int)> { ("Undergrounder", 0), ("NaturalMood", 1) },
                new Dictionary<string, int> {
                    {"Shooting", 4}, {"Melee", 4}, {"Construction", 4}, {"Mining", 3},
                    {"Cooking", 3}, {"Plants", 3}, {"Animals", 3}, {"Crafting", 5},
                    {"Artistic", 5}, {"Medicine", 3}, {"Social", 5}, {"Intellectual", 5}
                },
                new Dictionary<string, Passion> {
                    {"Crafting", Passion.Major},
                    {"Social", Passion.Minor}, {"Intellectual", Passion.Minor}
                }
            ),  
  
            // 5: Хмурый / muederatte — Хмуростан. Средний бой, топ строитель.  
            new BarbosinaCharacterPreset(
                "Хмурый", "muederatte", "Хмуростан",
                "BB_HmuryChild", "BB_HmuryAdult",
                new List<(string, int)> { ("NaturalMood", -2), ("Industriousness", 2) },
                new Dictionary<string, int> {
                    {"Shooting", 4}, {"Melee", 4}, {"Construction", 5}, {"Mining", 5},
                    {"Cooking", 3}, {"Plants", 3}, {"Animals", 3}, {"Crafting", 5},
                    {"Artistic", 3}, {"Medicine", 3}, {"Social", 5}, {"Intellectual", 5}
                },
                new Dictionary<string, Passion> {
                    {"Construction", Passion.Major},
                    {"Social", Passion.Minor}, {"Intellectual", Passion.Minor}
                }
            ),
        };

        // Маленький хелпер для скриптовых событий (GameComponent_BarbosinaEvents):
        // ищет заспавненного колониста игрока по нику среди всех карт.
        // Не трогает остальную логику пресетов.
        public static Pawn FindColonistByNick(string nick)
        {
            if (string.IsNullOrEmpty(nick)) return null;

            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.Name is NameTriple nt && nt.Nick == nick)
                    {
                        return pawn;
                    }
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    public static class Patch_NewGeneratedStartingPawn
    {
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

            // Возраст: случайный 19..30 (единственное, что меняется при перекате)  
            RandomizeAge(pawn);

            // Пол: всем мужской, с починкой тела и головы  
            ForceMale(pawn);

            // Чистим все случайные связи. Фостер и Акаси связываются ниже отдельно.  
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

            // Навыки: проходим ВСЕ скиллы, чтобы ничего не осталось от рандома.  
            // Уровень берём из словаря (не указан = 0, но у нас заданы все 12).  
            // Страсть: берётся из passions (мажор или минор), у остальных - None.  
            foreach (SkillDef skillDef in DefDatabase<SkillDef>.AllDefsListForReading)
            {
                SkillRecord record = pawn.skills.GetSkill(skillDef);
                if (record == null) continue;

                int level = 0;
                data.skillLevels.TryGetValue(skillDef.defName, out level);
                record.Level = level;

                record.passion = data.passions.TryGetValue(skillDef.defName, out Passion passion)
                    ? passion
                    : Passion.None;

                record.xpSinceLastLevel = 0f;
                record.xpSinceMidnight = 0f;
            }
        }

        // Случайный возраст 19..30 лет (биологический и хронологический).  
        private static void RandomizeAge(Pawn pawn)
        {
            try
            {
                long years = Rand.RangeInclusive(BarbosinaPresetData.MinAge, BarbosinaPresetData.MaxAge);
                long ticks = years * 3600000L; // GenDate.TicksPerYear  
                pawn.ageTracker.AgeBiologicalTicks = ticks;
                pawn.ageTracker.AgeChronologicalTicks = ticks;
            }
            catch (System.Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось выставить возраст: {e.Message}");
            }
        }

        // Принудительно делает пешку мужчиной и чинит тело/голову.  
        private static void ForceMale(Pawn pawn)
        {
            try
            {
                pawn.gender = Gender.Male;

                if (pawn.story != null)
                {
                    if (pawn.story.bodyType == BodyTypeDefOf.Female)
                    {
                        pawn.story.bodyType = BodyTypeDefOf.Male;
                    }

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

        private static void TryLinkFosterAkasi()
        {
            if (!GeneratedPawns.TryGetValue(BarbosinaPresetData.FosterIndex, out Pawn foster)) return;
            if (!GeneratedPawns.TryGetValue(BarbosinaPresetData.AkasiIndex, out Pawn akasi)) return;
            if (foster == null || akasi == null || foster == akasi) return;
            if (foster.relations == null || akasi.relations == null) return;

            // Сносим ЛЮБЫЕ старые Lover-связи у ОБОИХ партнёров перед пересборкой.
            // Это чинит баг: при рероле только Фостера или только Акаси у второго,
            // нерерольного партнёра могла остаться Lover-связь на уже выброшенный
            // объект пешки (ApplyPreset чистит relations только у текущей пешки),
            // из-за чего DirectRelationExists не находил совпадение и добавлял
            // вторую, "осиротевшую" запись вместо замены старой.
            RemoveAllOfDef(foster, PawnRelationDefOf.Lover);
            RemoveAllOfDef(akasi, PawnRelationDefOf.Lover);

            foster.relations.AddDirectRelation(PawnRelationDefOf.Lover, akasi);
        }

        private static void RemoveAllOfDef(Pawn pawn, PawnRelationDef def)
        {
            var stale = pawn.relations.DirectRelations.Where(r => r.def == def).ToList();
            foreach (var r in stale) pawn.relations.RemoveDirectRelation(r);
        }

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

    // ============================================================  
    // Автоприменение пресетов при открытии редактора стартовых пешек.  
    // Без этого шестёрка появляется только после ручного клика "случайно".  
    // ============================================================  
    [HarmonyPatch(typeof(Page_ConfigureStartingPawns), "PreOpen")]
    public static class Patch_ConfigureStartingPawns_PreOpen
    {
        public static void Postfix()
        {
            if (Find.Scenario == null) return;

            ScenarioDef scenDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(BarbosinaPresetData.ScenarioDefName);
            if (scenDef == null || Find.Scenario != scenDef.scenario) return;
            if (Find.GameInitData == null) return;

            try
            {
                var pawns = Find.GameInitData.startingAndOptionalPawns;
                int count = System.Math.Min(BarbosinaPresetData.Characters.Count, pawns.Count);

                for (int i = 0; i < count; i++)
                {
                    pawns[i] = StartingPawnUtility.NewGeneratedStartingPawn(i);
                }
            }
            catch (System.Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось перегенерировать стартовых пешек на входе: {e.Message}");
            }
        }
    }
}