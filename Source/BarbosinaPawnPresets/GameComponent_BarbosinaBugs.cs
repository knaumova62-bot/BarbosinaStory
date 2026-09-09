using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace BarbosinaStory
{
    // ============================================================  
    // Скриптовое событие "баги из пульта управления": в первые часы  
    // после старта на карту накатывает несколько волн слабых жуков  
    // (Megascarab), с интервалом между волнами, а затем всё стихает.  
    // Всего пара-тройка волн за игру, только в первые сутки с небольшим.  
    // Работает только в сценарии BarbosinaStory_Crash.  
    // ============================================================  
    public class GameComponent_BarbosinaBugs : GameComponent
    {
        // ~2 игровых часа до первой волны. 2500 тиков = 1 час.  
        private const int FirstDelayTicks = 5000;

        // Интервал между волнами (~8 игровых часов).  
        private const int BetweenWavesTicks = 20000;

        // Всего волн за игру.  
        private const int TotalWaves = 3;

        // Сколько жуков в одной волне.  
        private const int MinBugs = 2;
        private const int MaxBugs = 4;

        private int ticksUntilWave = -1;
        private int wavesDone = 0;

        public GameComponent_BarbosinaBugs(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ticksUntilWave, "barbosinaTicksUntilWave", -1);
            Scribe_Values.Look(ref wavesDone, "barbosinaWavesDone", 0);
        }

        public override void FinalizeInit()
        {
            // Заводим таймер первой волны только один раз, в нашем сценарии.  
            if (wavesDone < TotalWaves && ticksUntilWave < 0 && IsBarbosinaScenario())
            {
                ticksUntilWave = FirstDelayTicks;
            }
        }

        public override void GameComponentTick()
        {
            if (wavesDone >= TotalWaves || ticksUntilWave < 0) return;

            ticksUntilWave--;
            if (ticksUntilWave > 0) return;

            SpawnBugSwarm();
            wavesDone++;

            // Если волны ещё остались - заводим таймер на следующую,  
            // иначе выключаем событие насовсем.  
            ticksUntilWave = wavesDone < TotalWaves ? BetweenWavesTicks : -1;
        }

        // internal (не private), чтобы GameComponent_BarbosinaEvents мог переиспользовать  
        // ту же проверку сценария и не дублировать логику.  
        internal static bool IsBarbosinaScenario()
        {
            ScenarioDef scenDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(BarbosinaPresetData.ScenarioDefName);
            return scenDef != null && Find.Scenario == scenDef.scenario;
        }

        private void SpawnBugSwarm()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;

                Faction insects = Faction.OfInsects;
                if (insects == null) return;

                // Слабый жук. Через DefDatabase, чтобы не падать, если поля нет в PawnKindDefOf.  
                PawnKindDef bugKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Megascarab");
                if (bugKind == null) return;

                int count = Rand.RangeInclusive(MinBugs, MaxBugs);
                List<Pawn> spawned = new List<Pawn>();

                for (int i = 0; i < count; i++)
                {
                    if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 cell, map,
                            CellFinder.EdgeRoadChance_Animal))
                    {
                        continue;
                    }

                    Pawn bug = PawnGenerator.GeneratePawn(bugKind, insects);
                    GenSpawn.Spawn(bug, cell, map, WipeMode.Vanish);
                    spawned.Add(bug);
                }

                if (spawned.Count == 0) return;

                // Стравливаем стайку на колонию.  
                LordMaker.MakeNewLord(
                    insects,
                    new LordJob_AssaultColony(insects, canKidnap: false, canTimeoutOrFlee: false),
                    map,
                    spawned);

                Find.LetterStack.ReceiveLetter(
                    "Баги из пульта управления",
                    "Из обломков пульта управления снова полезли баги и рванули прямо к вам. Тварей немного и они хлипкие, но зубы у них есть. Встречайте гостей.",
                    LetterDefOf.ThreatBig,
                    new LookTargets(spawned));
            }
            catch (System.Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось запустить набег багов: {e.Message}");
            }
        }
    }
}