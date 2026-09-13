using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BarbosinaStory
{
    // ============================================================  
    // Скриптовые события сценария BarbosinaStory_Crash:  
    //   1) Барбос периодически пропадает на 1-2 дня и возвращается.  
    //   2) Фостер периодически впадает в мем-психоз; Акаси реагирует.  
    //      Заодно на день ловит временный Bloodlust/Psychopath.  
    //   3) Хмурый редко пропадает надолго (неделя-месяц).  
    //   4) Барбос временно становится Cannibal ("остров дал о себе знать").  
    //   5) Кумар временно становится Gay ("солнечные соседи повлияли").  
    //  
    // Стиль и хелперы переиспользуют GameComponent_BarbosinaBugs:  
    // тот же guard на сценарий (IsBarbosinaScenario), тот же паттерн  
    // try/catch + Log.Warning("[BarbosinaStory] ..."), тот же способ  
    // искать Def через DefDatabase.GetNamedSilentFail.  
    //  
    // Структура рассчитана на расширение: каждое событие — это  
    // BarbosinaScriptedEvent с методом TryFire() и своим интервалом  
    // (в днях). Чтобы добавить новое событие, достаточно написать ещё  
    // один класс-наследник и добавить его в список events ниже.  
    // ============================================================  
    public class GameComponent_BarbosinaEvents : GameComponent
    {
        private readonly List<BarbosinaScriptedEvent> events = new List<BarbosinaScriptedEvent>
        {
            new BarbosDisappearEvent(),
            new FosterMeltdownEvent(),
            new HmuryLongDisappearEvent(),
            new BarbosCannibalEvent(),
            new KumarGayEvent(),
        };

        public GameComponent_BarbosinaEvents(Game game) { }

        public override void ExposeData()
        {
            // Порядок events фиксирован в коде (не зависит от сейва), поэтому  
            // достаточно каждому событию сохранять свои поля под своими же  
            // именами ключей — коллизий не будет.  
            foreach (BarbosinaScriptedEvent scriptedEvent in events)
            {
                scriptedEvent.ExposeData();
            }
        }

        public override void GameComponentTick()
        {
            // Работаем только в нашем сценарии - переиспользуем guard  
            // из GameComponent_BarbosinaBugs, чтобы не дублировать логику.  
            if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return;

            Map map = Find.CurrentMap;
            if (map == null) return;

            foreach (BarbosinaScriptedEvent scriptedEvent in events)
            {
                scriptedEvent.Tick(map);
            }
        }
    }

    // ============================================================  
    // Базовый класс скриптового события: сам считает случайный интервал  
    // между попытками (в игровых днях) и сам катает шанс срабатывания,  
    // плюс отдельный ExtraTick для событий с "отложенным" действием  
    // (например, возврат пропавшей пешки или снятие временного трейта).  
    // ============================================================  
    public abstract class BarbosinaScriptedEvent
    {
        protected abstract string Id { get; }
        protected abstract int MinIntervalDays { get; }
        protected abstract int MaxIntervalDays { get; }
        protected abstract float FireChance { get; }

        private int ticksUntilRoll = -1;

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref ticksUntilRoll, $"barbosina_{Id}_ticksUntilRoll", -1);
        }

        public void Tick(Map map)
        {
            if (ticksUntilRoll < 0)
            {
                ScheduleNextRoll();
            }
            else
            {
                ticksUntilRoll--;
                if (ticksUntilRoll == 0)
                {
                    RollAndMaybeFire(map);
                    ScheduleNextRoll();
                }
            }

            // Отдельный хук для действий, которые тикают независимо от  
            // таймера ролла (например: "пора вернуть пропавшую пешку"  
            // или "пора снять временный трейт").  
            ExtraTick(map);
        }

        private void RollAndMaybeFire(Map map)
        {
            try
            {
                if (Rand.Chance(FireChance))
                {
                    TryFire(map);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Событие '{Id}' упало при срабатывании: {e.Message}");
            }
        }

        private void ScheduleNextRoll()
        {
            int days = Rand.RangeInclusive(MinIntervalDays, MaxIntervalDays);
            ticksUntilRoll = days * GenDate.TicksPerDay;
        }

        // Основная логика события. Событие само решает, что делать,  
        // если целевой пешки нет на карте/она мертва - тогда просто  
        // ничего не происходит (попытка "сгорает").  
        protected abstract void TryFire(Map map);

        // Переопределяется событиями с отложенным действием.  
        protected virtual void ExtraTick(Map map) { }
    }

    // ============================================================  
    // Общие хелперы для событий "пешка пропадает и возвращается".  
    // ============================================================  
    internal static class BarbosinaEventUtility
    {
        // Убирает пешку с карты и передаёт её в WorldPawns "навсегда",  
        // чтобы игра не сочла её мусором и не удалила во время отлучки.  
        internal static void SendAway(Pawn pawn)
        {
            Map map = pawn.Map;
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            if (!Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            if (map == null)
            {
                Log.Warning($"[BarbosinaStory] У пешки {pawn?.Name} не было карты при отправке в отлучку.");
            }
        }

        // Возвращает ранее отправленную в WorldPawns пешку обратно на карту,  
        // у случайной клетки входа, и полностью её лечит.  
        internal static bool TryBringBack(Pawn pawn, Map map)
        {
            try
            {
                if (pawn == null || map == null) return false;

                if (Find.WorldPawns.Contains(pawn))
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }

                if (pawn.Dead)
                {
                    // Мёртвых не воскрешаем - событие просто не срабатывает.  
                    return false;
                }

                if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 cell, map,
                        CellFinder.EdgeRoadChance_Animal))
                {
                    cell = CellFinder.RandomEdgeCell(map);
                }

                GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
                HealCompletely(pawn);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось вернуть пешку {pawn?.Name}: {e.Message}");
                return false;
            }
        }

        // "Ни пылинки на нём": чиним недостающие части тела и снимаем  
        // все плохие hediff'ы (ранения, болезни, инфекции и т.п.).  
        internal static void HealCompletely(Pawn pawn)
        {
            try
            {
                if (pawn?.health?.hediffSet == null) return;

                List<Hediff_MissingPart> missingParts = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_MissingPart>()
                    .ToList();

                foreach (Hediff_MissingPart missing in missingParts)
                {
                    try
                    {
                        pawn.health.RestorePart(missing.Part);
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"[BarbosinaStory] Не удалось восстановить часть тела {missing.Part?.def?.defName}: {e.Message}");
                    }
                }

                List<Hediff> badHediffs = pawn.health.hediffSet.hediffs
                    .Where(h => h is Hediff_Injury || (h.def != null && h.def.isBad))
                    .ToList();

                foreach (Hediff hediff in badHediffs)
                {
                    try
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"[BarbosinaStory] Не удалось убрать hediff {hediff?.def?.defName}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось долечить пешку {pawn?.Name}: {e.Message}");
            }
        }

        // Ищет первый существующий TraitDef из списка предпочтений.  
        internal static TraitDef FindFirstAvailableTrait(string[] defNames)
        {
            foreach (string defName in defNames)
            {
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
                if (def != null) return def;
            }
            return null;
        }

        internal static MentalStateDef FindFirstAvailableMentalState(string[] defNames)
        {
            foreach (string defName in defNames)
            {
                MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail(defName);
                if (def != null) return def;
            }
            return null;
        }
    }

    // ============================================================  
    // Событие 1: "Барбос пропадает" - пара раз в месяц, на 1-2 дня.  
    // ============================================================  
    public class BarbosDisappearEvent : BarbosinaScriptedEvent
    {
        private const string Nick = "Барсик";

        private const int MinDays = 30;
        private const int MaxDays = 60;
        private const float Chance = 0.35f;

        private const int MinAwayDays = 1;
        private const int MaxAwayDays = 2;

        private Pawn awayPawn;
        private int returnAtTick = -1;

        protected override string Id => "barbos_disappear";
        protected override int MinIntervalDays => MinDays;
        protected override int MaxIntervalDays => MaxDays;
        protected override float FireChance => Chance;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref awayPawn, "barbosAwayPawn");
            Scribe_Values.Look(ref returnAtTick, "barbosReturnAtTick", -1);
        }

        protected override void TryFire(Map map)
        {
            if (awayPawn != null) return; // уже в отлучке  

            Pawn barbos = BarbosinaPresetData.FindColonistByNick(Nick);
            if (barbos == null || barbos.Dead || !barbos.Spawned) return;

            int awayDays = Rand.RangeInclusive(MinAwayDays, MaxAwayDays);
            returnAtTick = Find.TickManager.TicksGame + awayDays * GenDate.TicksPerDay;
            awayPawn = barbos;

            BarbosinaEventUtility.SendAway(barbos);

            Find.LetterStack.ReceiveLetter(
                "Барбос пропал",
                "Барбос пропал без следа. Никто не видел, куда он делся - был на карте, и вдруг его нет. Остаётся только ждать и надеяться, что он объявится сам.",
                LetterDefOf.NeutralEvent);
        }

        protected override void ExtraTick(Map map)
        {
            if (awayPawn == null || returnAtTick < 0) return;
            if (Find.TickManager.TicksGame < returnAtTick) return;

            Pawn pawn = awayPawn;
            bool ok = BarbosinaEventUtility.TryBringBack(pawn, map);

            awayPawn = null;
            returnAtTick = -1;

            if (ok)
            {
                Find.LetterStack.ReceiveLetter(
                    "Барбос вернулся",
                    "Барбос как ни в чём не бывало объявился на краю карты - целый, здоровый, будто и не пропадал. Где он был - молчит.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(pawn));
            }
        }
    }

    // ============================================================  
    // Событие 2: "Фостер сходит с ума" - пара раз в месяц.  
    // Заодно на день вешает временный Bloodlust/Psychopath.  
    // Реакция Акаси: с шансом X грустит, с меньшим шансом Y тоже срывается.  
    // ============================================================  
    public class FosterMeltdownEvent : BarbosinaScriptedEvent
    {
        private const string FosterNick = "Фостер";
        private const string AkasiNick = "Акаси";

        private const int MinDays = 15;
        private const int MaxDays = 30;
        private const float Chance = 0.4f;

        // Порядок предпочтений ментального срыва.  
        private static readonly string[] PreferredMentalStates = { "Wander_Psychotic", "Berserk" };

        // Временный трейт на день: сначала пробуем Bloodlust, если нет - Psychopath.  
        private static readonly string[] PreferredTraits = { "Bloodlust", "Psychopath" };
        private const int TraitDegree = 0;
        private const int TraitDays = 1;

        // Акаси грустит с шансом побольше, срывается сам - с шансом поменьше.  
        private const float AkasiSadChance = 0.5f;
        private const float AkasiMeltdownChance = 0.15f;

        private const string AkasiSadThoughtDefName = "BB_AkasiSadAboutFoster";

        // Отложенное снятие временного трейта у Фостера.  
        private Pawn traitPawn;
        private string appliedTraitDefName;
        private int removeTraitAtTick = -1;

        protected override string Id => "foster_meltdown";
        protected override int MinIntervalDays => MinDays;
        protected override int MaxIntervalDays => MaxDays;
        protected override float FireChance => Chance;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref traitPawn, "fosterTraitPawn");
            Scribe_Values.Look(ref appliedTraitDefName, "fosterAppliedTrait", null);
            Scribe_Values.Look(ref removeTraitAtTick, "fosterRemoveTraitAtTick", -1);
        }

        protected override void TryFire(Map map)
        {
            Pawn foster = BarbosinaPresetData.FindColonistByNick(FosterNick);
            if (foster == null || foster.Dead || !foster.Spawned) return;
            if (foster.mindState.mentalStateHandler != null && foster.mindState.mentalStateHandler.InMentalState) return;

            MentalStateDef stateDef = BarbosinaEventUtility.FindFirstAvailableMentalState(PreferredMentalStates);
            if (stateDef == null)
            {
                Log.Warning("[BarbosinaStory] Не найден ни один MentalStateDef для срыва Фостера.");
                return;
            }

            bool started = foster.mindState.mentalStateHandler.TryStartMentalState(
                stateDef,
                reason: "Фостер сходит с ума",
                forceWake: true);

            if (!started) return;

            Find.LetterStack.ReceiveLetter(
                "Фостер сорвался",
                "Фостер с криком \"Я ЗДЕСЬ СОЛНЕЧНЫЙ!\" носится кругами, пытается \"выкакать из себя багов\" и на бегу охаживает себя по голове чем-то подозрительно фаллосообразным. Остальные стараются держаться подальше.",
                LetterDefOf.NegativeEvent,
                new LookTargets(foster));

            TryApplyTempTrait(foster);
            ReactAkasi(map);
        }

        // Вешает временный трейт Фостеру, если у него его ещё нет постоянно.  
        private void TryApplyTempTrait(Pawn foster)
        {
            try
            {
                if (traitPawn != null) return; // уже висит с прошлого раза  
                if (foster.story?.traits == null) return;

                TraitDef traitDef = BarbosinaEventUtility.FindFirstAvailableTrait(PreferredTraits);
                if (traitDef == null) return;
                if (foster.story.traits.HasTrait(traitDef)) return; // не трогаем постоянный  

                foster.story.traits.GainTrait(new Trait(traitDef, TraitDegree, forced: true));

                traitPawn = foster;
                appliedTraitDefName = traitDef.defName;
                removeTraitAtTick = Find.TickManager.TicksGame + TraitDays * GenDate.TicksPerDay;
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось повесить временный трейт Фостеру: {e.Message}");
            }
        }

        protected override void ExtraTick(Map map)
        {
            if (traitPawn == null || removeTraitAtTick < 0) return;
            if (Find.TickManager.TicksGame < removeTraitAtTick) return;

            try
            {
                Pawn pawn = traitPawn;
                string defName = appliedTraitDefName;

                traitPawn = null;
                appliedTraitDefName = null;
                removeTraitAtTick = -1;

                if (pawn == null || pawn.Dead || pawn.story?.traits == null || defName == null) return;

                Trait trait = pawn.story.traits.allTraits
                    .FirstOrDefault(t => t.def != null && t.def.defName == defName);
                if (trait != null)
                {
                    pawn.story.traits.RemoveTrait(trait);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось снять временный трейт Фостера: {e.Message}");
            }
        }

        private void ReactAkasi(Map map)
        {
            try
            {
                Pawn akasi = BarbosinaPresetData.FindColonistByNick(AkasiNick);
                if (akasi == null || akasi.Dead || !akasi.Spawned) return;

                if (Rand.Chance(AkasiMeltdownChance))
                {
                    if (akasi.mindState.mentalStateHandler != null && !akasi.mindState.mentalStateHandler.InMentalState)
                    {
                        MentalStateDef stateDef = BarbosinaEventUtility.FindFirstAvailableMentalState(PreferredMentalStates);
                        if (stateDef != null &&
                            akasi.mindState.mentalStateHandler.TryStartMentalState(stateDef, "Акаси не выдержал", forceWake: true))
                        {
                            Find.LetterStack.ReceiveLetter(
                                "Акаси тоже не выдержал",
                                "Глядя на Фостера, Акаси срывается следом - истерика на истерике.",
                                LetterDefOf.NegativeEvent,
                                new LookTargets(akasi));
                            return;
                        }
                    }
                }

                if (Rand.Chance(AkasiSadChance))
                {
                    ThoughtDef sadThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(AkasiSadThoughtDefName);
                    if (sadThought != null && akasi.needs?.mood?.thoughts?.memories != null)
                    {
                        akasi.needs.mood.thoughts.memories.TryGainMemory(sadThought);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Реакция Акаси на срыв Фостера упала: {e.Message}");
            }
        }
    }

    // ============================================================  
    // Событие 3: "Хмурый пропадает надолго" - реже Барбоса, на 7-30 дней.  
    // ============================================================  
    public class HmuryLongDisappearEvent : BarbosinaScriptedEvent
    {
        private const string Nick = "muederatte";

        private const int MinDays = 45;
        private const int MaxDays = 90;
        private const float Chance = 0.3f;

        private const int MinAwayDays = 7;
        private const int MaxAwayDays = 30;

        private Pawn awayPawn;
        private int returnAtTick = -1;

        protected override string Id => "hmury_disappear";
        protected override int MinIntervalDays => MinDays;
        protected override int MaxIntervalDays => MaxDays;
        protected override float FireChance => Chance;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref awayPawn, "hmuryAwayPawn");
            Scribe_Values.Look(ref returnAtTick, "hmuryReturnAtTick", -1);
        }

        protected override void TryFire(Map map)
        {
            if (awayPawn != null) return;

            Pawn hmury = BarbosinaPresetData.FindColonistByNick(Nick);
            if (hmury == null || hmury.Dead || !hmury.Spawned) return;

            int awayDays = Rand.RangeInclusive(MinAwayDays, MaxAwayDays);
            returnAtTick = Find.TickManager.TicksGame + awayDays * GenDate.TicksPerDay;
            awayPawn = hmury;

            BarbosinaEventUtility.SendAway(hmury);

            Find.LetterStack.ReceiveLetter(
                "Хмурый пропал",
                "Хмурый ушёл и не вернулся. Говорят, последнее, что он сказал перед тем, как исчезнуть - тяжело вздохнув: \"Обидно, блять, обидно\". С тех пор о нём ни слуху ни духу.",
                LetterDefOf.NegativeEvent);
        }

        protected override void ExtraTick(Map map)
        {
            if (awayPawn == null || returnAtTick < 0) return;
            if (Find.TickManager.TicksGame < returnAtTick) return;

            Pawn pawn = awayPawn;
            bool ok = BarbosinaEventUtility.TryBringBack(pawn, map);

            awayPawn = null;
            returnAtTick = -1;

            if (ok)
            {
                Find.LetterStack.ReceiveLetter(
                    "Хмурый вернулся",
                    "Хмурый как ни в чём не бывало заходит на карту, будто отлучался на пять минут за хлебом, а не пропадал на неделю с лишним. О том, где он был, - ни слова.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(pawn));
            }
        }
    }

    // ============================================================  
    // Базовый класс временного трейта: вешает трейт на пешку на N дней,  
    // затем снимает. Не трогает трейт, если он у пешки постоянный.  
    // ============================================================  
    public abstract class TemporaryTraitEvent : BarbosinaScriptedEvent
    {
        protected abstract string TargetNick { get; }
        protected abstract string[] PreferredTraits { get; }
        protected abstract int TraitDegree { get; }
        protected abstract int MinTraitDays { get; }
        protected abstract int MaxTraitDays { get; }
        protected abstract string GainTitle { get; }
        protected abstract string GainText { get; }
        protected abstract string LoseTitle { get; }
        protected abstract string LoseText { get; }

        private Pawn affectedPawn;
        private string appliedTraitDefName;
        private int removeAtTick = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref affectedPawn, $"barbosina_{Id}_affectedPawn");
            Scribe_Values.Look(ref appliedTraitDefName, $"barbosina_{Id}_appliedTrait", null);
            Scribe_Values.Look(ref removeAtTick, $"barbosina_{Id}_removeAtTick", -1);
        }

        protected override void TryFire(Map map)
        {
            if (affectedPawn != null) return; // трейт уже висит  

            Pawn pawn = BarbosinaPresetData.FindColonistByNick(TargetNick);
            if (pawn == null || pawn.Dead || pawn.story?.traits == null) return;

            TraitDef traitDef = BarbosinaEventUtility.FindFirstAvailableTrait(PreferredTraits);
            if (traitDef == null)
            {
                Log.Warning($"[BarbosinaStory] Не найден ни один TraitDef для события '{Id}'.");
                return;
            }

            // Если трейт у пешки уже есть постоянно - не трогаем, иначе снимем чужой.  
            if (pawn.story.traits.HasTrait(traitDef)) return;

            pawn.story.traits.GainTrait(new Trait(traitDef, TraitDegree, forced: true));

            affectedPawn = pawn;
            appliedTraitDefName = traitDef.defName;
            int days = Rand.RangeInclusive(MinTraitDays, MaxTraitDays);
            removeAtTick = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;

            Find.LetterStack.ReceiveLetter(GainTitle, GainText, LetterDefOf.NeutralEvent, new LookTargets(pawn));
        }

        protected override void ExtraTick(Map map)
        {
            if (affectedPawn == null || removeAtTick < 0) return;
            if (Find.TickManager.TicksGame < removeAtTick) return;

            try
            {
                Pawn pawn = affectedPawn;
                string defName = appliedTraitDefName;

                affectedPawn = null;
                appliedTraitDefName = null;
                removeAtTick = -1;

                if (pawn == null || pawn.Dead || pawn.story?.traits == null || defName == null) return;

                Trait trait = pawn.story.traits.allTraits
                    .FirstOrDefault(t => t.def != null && t.def.defName == defName);
                if (trait != null)
                {
                    pawn.story.traits.RemoveTrait(trait);
                }

                if (pawn.Spawned)
                {
                    Find.LetterStack.ReceiveLetter(LoseTitle, LoseText, LetterDefOf.NeutralEvent, new LookTargets(pawn));
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(LoseTitle, LoseText, LetterDefOf.NeutralEvent);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось снять временный трейт события '{Id}': {e.Message}");
            }
        }
    }

    // ============================================================  
    // Событие 4: Барбос временно становится каннибалом.  
    // ============================================================  
    public class BarbosCannibalEvent : TemporaryTraitEvent
    {
        protected override string Id => "barbos_cannibal";
        protected override int MinIntervalDays => 20;
        protected override int MaxIntervalDays => 45;
        protected override float FireChance => 0.3f;

        protected override string TargetNick => "Барсик";
        protected override string[] PreferredTraits => new[] { "Cannibal" };
        protected override int TraitDegree => 0;
        protected override int MinTraitDays => 2;
        protected override int MaxTraitDays => 3;

        protected override string GainTitle => "Остров Барбоса дал о себе знать";
        protected override string GainText =>
            "На Барбоса что-то нашло. В глазах голодный блеск, на соседей он теперь поглядывает как на обед. Островные привычки, видимо, никуда не делись.";
        protected override string LoseTitle => "Барбос отпустило";
        protected override string LoseText =>
            "Барбос отошёл, аппетит к сокамерникам пропал. Снова смотрит на людей как на людей, а не как на закуску.";
    }

    // ============================================================  
    // Событие 5: Кумар временно становится геем.  
    // ============================================================  
    public class KumarGayEvent : TemporaryTraitEvent
    {
        protected override string Id => "kumar_gay";
        protected override int MinIntervalDays => 20;
        protected override int MaxIntervalDays => 45;
        protected override float FireChance => 0.3f;

        protected override string TargetNick => "Кумар";
        protected override string[] PreferredTraits => new[] { "Gay" };
        protected override int TraitDegree => 0;
        protected override int MinTraitDays => 2;
        protected override int MaxTraitDays => 4;

        protected override string GainTitle => "Солнечные соседи повлияли";
        protected override string GainText =>
            "Кумар слишком долго тёрся возле солнечных. Что-то в нём переключилось, и теперь он смотрит на боевых товарищей совсем другими глазами.";
        protected override string LoseTitle => "Кумар пришёл в себя";
        protected override string LoseText =>
            "Наваждение спало, солнечное влияние выветрилось. Кумар снова прежний и делает вид, что ничего не было.";
    }
}