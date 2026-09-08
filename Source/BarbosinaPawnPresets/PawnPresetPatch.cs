using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BarbosinaStory
{
    // ============================================================
    // ВАЖНО, прочитай перед компиляцией:
    //
    // Метод StartingPawnUtility.NewGeneratedStartingPawn(int index) существует
    // в RimWorld 1.5/1.6 и вызывается для каждого из стартовых слотов персонажей
    // (в т.ч. при нажатии "перекатать" на экране ConfigureStartingPawns).
    // Сигнатуру стоит перепроверить в твоей версии игры (через dnSpy/ILSpy,
    // Assembly-CSharp.dll -> RimWorld.StartingPawnUtility) — если у метода
    // другое имя параметра или он статический с другим количеством
    // аргументов, HarmonyPatch ниже нужно будет поправить под реальную
    // сигнатуру. Если что-то не совпадёт — Harmony при старте игры напишет
    // в лог ("Player.log") ошибку патча вида "Method X not found" — пришли
    // мне этот лог или сигнатуру из декомпилятора, поправлю.
    //
    // Что патч делает:
    //  - Только для сценария с defName == "BarbosinaStory_Crash".
    //  - Для стартовых слотов 0..5 жёстко выставляет имя, черты, навыки
    //    (с страстями) и биографии (Detstvo/Vzroslost) шести персонажей.
    //  - Внешность (пол, лицо, тело, причёска) НЕ трогает: пол/тело влияют
    //    на меш и текстуры персонажа, и подмена постфактум (уже после
    //    генерации) может визуально сломать пешку. Поэтому пол оставлен
    //    случайным — если нужно жёстко зафиксировать и его, это отдельная,
    //    более рискованная правка (патчить сам PawnGenerationRequest ДО
    //    генерации, а не постфактум).
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
        public Dictionary<string, Passion> passions; // необязательно, можно оставить пустым

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

        // Индекс в списке = индекс стартового слота (0..5).
        // Порядок можно менять местами как удобно.
        public static readonly List<BarbosinaCharacterPreset> Characters = new List<BarbosinaCharacterPreset>
        {
            // 0: Кумар — Кумаростан
            new BarbosinaCharacterPreset(
                "Кумар", "Кумар", "Кумаростан",
                "BB_KumarChild", "BB_KumarAdult",
                new List<(string, int)> { ("Abrasive", 0), ("Industriousness", 2) },
                new Dictionary<string, int> {
                    {"Social", 8}, {"Shooting", 6}, {"Intellectual", 5}, {"Melee", 3}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major}, {"Shooting", Passion.Minor} }
            ),

            // 1: Фостер — Солнечное королевство
            new BarbosinaCharacterPreset(
                "Фостер", "Фостер", "Фостерия",
                "BB_FosterChild", "BB_FosterAdult",
                new List<(string, int)> { ("NaturalMood", 2), ("Beauty", 1) },
                new Dictionary<string, int> {
                    {"Social", 10}, {"Artistic", 7}, {"Construction", 2}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major}, {"Artistic", Passion.Major} }
            ),

            // 2: Акаси — апостол Фостера
            new BarbosinaCharacterPreset(
                "Акаси", "Акаси", "Фостерия",
                "BB_AkasiChild", "BB_AkasiAdult",
                new List<(string, int)> { ("Nerves", 2) },
                new Dictionary<string, int> {
                    {"Intellectual", 8}, {"Social", 6}, {"Medicine", 5}
                },
                new Dictionary<string, Passion> { {"Intellectual", Passion.Major} }
            ),

            // 3: Барбос/Барсик — Барсеговина
            new BarbosinaCharacterPreset(
                "Барбос", "Барсик", "Барсеговина",
                "BB_BarbosChild", "BB_BarbosAdult",
                new List<(string, int)> { ("Nerves", 1), ("Industriousness", 1) },
                new Dictionary<string, int> {
                    {"Social", 7}, {"Shooting", 6}, {"Construction", 5}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major}, {"Shooting", Passion.Minor} }
            ),

            // 4: Киткат — Respectable
            new BarbosinaCharacterPreset(
                "Киткат", "Respectable", "",
                "BB_KitkatChild", "BB_KitkatAdult",
                new List<(string, int)> { ("Undergrounder", 0), ("NaturalMood", 1) },
                new Dictionary<string, int> {
                    {"Social", 8}, {"Intellectual", 6}, {"Artistic", 5}
                },
                new Dictionary<string, Passion> { {"Social", Passion.Major}, {"Artistic", Passion.Minor} }
            ),

            // 5: Хмурый / muederatte — Хмуростан
            new BarbosinaCharacterPreset(
                "Хмурый", "muederatte", "Хмуростан",
                "BB_HmuryChild", "BB_HmuryAdult",
                new List<(string, int)> { ("NaturalMood", -2), ("Industriousness", 2) },
                new Dictionary<string, int> {
                    {"Construction", 8}, {"Shooting", 5}, {"Intellectual", 5}, {"Mining", 4}
                },
                new Dictionary<string, Passion> { {"Construction", Passion.Major} }
            ),
        };
    }

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    public static class Patch_NewGeneratedStartingPawn
    {
        public static void Postfix(int index, ref Pawn __result)
        {
            if (__result == null) return;
            if (Find.Scenario == null) return;

            // У класса Scenario нет поля defName (defName есть только у ScenarioDef).
            // Поэтому берём ScenarioDef по имени и сравниваем именно с его полем .scenario —
            // это и есть тот самый объект Scenario, который использует игра.
            ScenarioDef scenDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(BarbosinaPresetData.ScenarioDefName);
            if (scenDef == null || Find.Scenario != scenDef.scenario) return;

            if (index < 0 || index >= BarbosinaPresetData.Characters.Count) return;

            ApplyPreset(__result, BarbosinaPresetData.Characters[index]);
        }

        private static void ApplyPreset(Pawn pawn, BarbosinaCharacterPreset data)
        {
            // Имя
            pawn.Name = new NameTriple(data.firstName, data.nickName, data.lastName);

            // Биографии (только текст/флейвор — навыки от них не пересчитываем,
            // выставляем явно ниже, чтобы не зависеть от внутреннего пересчёта)
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
    }
}