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
    //   3) Хмурый редко пропадает надолго (неделя-месяц).  
    //  
    // Стиль и хелперы переиспользуют GameComponent_BarbosinaBugs:  
    // тот же guard на сценарий (IsBarbosinaScenario), тот же паттерн  
    // try/catch + Log.Warning("[BarbosinaStory] ..."), тот же способ  
    // искать PawnKindDef/MentalStateDef через DefDatabase.GetNamedSilentFail,  
    // чтобы не падать на несовпадении сигнатур между версиями игры.  
    //  
    // Структура рассчитана на расширение: каждое событие — это  
    // BarbosinaScriptedEvent с методом TryFire() и своим интервалом  
    // (в днях). Чтобы добавить новое событие, достаточно написать ещё  
    // один класс-наследник и добавить его в список Events ниже.  
    // ============================================================  
    public class GameComponent_BarbosinaEvents : GameComponent
    {
        private readonly List<BarbosinaScriptedEvent> events = new List<BarbosinaScriptedEvent>
        {
            new BarbosDisappearEvent(),
            new FosterMeltdownEvent(),
            new HmuryLongDisappearEvent(),
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
    // (например, возврат пропавшей пешки).  
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
            // таймера ролла (например: "пора вернуть пропавшую пешку").  
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
                Log.Warning($"[BarbosinaStory] Событие '{Id}' упало при срабatывании: {e.Message}");
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

        // Переопределяется событиями с отложенным действием  
        // (Барбос/Хмурый: нужно каждый тик проверять, не пора ли вернуться).  
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
        // Не трогаем hediff'ы, которые def считает не "плохими"  
        // (импланты, генетические особенности и подобное) - их лечить не нужно.  
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
    }

    // ============================================================  
    // Событие 1: "Барбос пропадает" - пара раз в месяц, на 1-2 дня.  
    // ============================================================  
    public class BarbosDisappearEvent : BarbosinaScriptedEvent
    {
        private const string Nick = "Барсик";

        // Интервал между попытками ~5-15 дней, шанс сработать при попытке -  
        // вместе это в среднем даёт "пару раз в месяц".  
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
            // Scribe_References обязателен: без него после сейва/лоада  
            // во время отлучки ссылка на пешку потеряется.  
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
    // Реакция Акаси: с шансом X грустит, с меньшим шансом Y тоже срывается.  
    // ============================================================  
    public class FosterMeltdownEvent : BarbosinaScriptedEvent
    {
        private const string FosterNick = "Фостер";
        private const string AkasiNick = "Акаси";

        private const int MinDays = 15;
        private const int MaxDays = 30;
        private const float Chance = 0.4f;

        // Порядок предпочтений ментального срыва: пробуем безобидный  
        // "психотичное блуждание", если defName в этой версии игры  
        // не найден - падаем на Berserk.  
        private static readonly string[] PreferredMentalStates = { "Wander_Psychotic", "Berserk" };

        // Акаси грустит с шансом побольше, срывается сам - с шансом поменьше.  
        private const float AkasiSadChance = 0.5f;
        private const float AkasiMeltdownChance = 0.15f;

        private const string AkasiSadThoughtDefName = "BB_AkasiSadAboutFoster";

        protected override string Id => "foster_meltdown";
        protected override int MinIntervalDays => MinDays;
        protected override int MaxIntervalDays => MaxDays;
        protected override float FireChance => Chance;

        protected override void TryFire(Map map)
        {
            Pawn foster = BarbosinaPresetData.FindColonistByNick(FosterNick);
            if (foster == null || foster.Dead || !foster.Spawned) return;
            if (foster.mindState.mentalStateHandler != null && foster.mindState.mentalStateHandler.InMentalState) return;

            MentalStateDef stateDef = FindFirstAvailable(PreferredMentalStates);
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

            ReactAkasi(map);
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
                        MentalStateDef stateDef = FindFirstAvailable(PreferredMentalStates);
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

        private static MentalStateDef FindFirstAvailable(string[] defNames)
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
    // Событие 3: "Хмурый пропадает надолго" - реже Барбоса, на 7-30 дней.  
    // ============================================================  
    public class HmuryLongDisappearEvent : BarbosinaScriptedEvent
    {
        private const string Nick = "muederatte";

        // Реже, чем у Барбоса, и с меньшим шансом сработать при попытке.  
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
}