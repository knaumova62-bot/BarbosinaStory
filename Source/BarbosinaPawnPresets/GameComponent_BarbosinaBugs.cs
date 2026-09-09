using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace BarbosinaStory
{
    // ============================================================  
    // Скриптовое событие "баги из трюма": через пару игровых часов  
    // после старта на карту с края прибегает небольшая стайка слабых  
    // жуков (Megascarab) и идёт на колонию. Разово, один раз за игру.  
    // Работает только в сценарии BarbosinaStory_Crash.  
    // ============================================================  
    public class GameComponent_BarbosinaBugs : GameComponent
    {
        // ~2 игровых часа. В RimWorld 2500 тиков = 1 час (GenDate.TicksPerHour).  
        private const int DelayTicks = 5000;

        // Сколько жуков в набеге.  
        private const int MinBugs = 3;
        private const int MaxBugs = 5;

        private int ticksUntilSwarm = -1;
        private bool swarmDone = false;

        public GameComponent_BarbosinaBugs(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ticksUntilSwarm, "barbosinaTicksUntilSwarm", -1);
            Scribe_Values.Look(ref swarmDone, "barbosinaSwarmDone", false);
        }

        public override void FinalizeInit()
        {
            // Запускаем таймер только один раз, в нашем сценарии.  
            if (!swarmDone && ticksUntilSwarm < 0 && IsBarbosinaScenario())
            {
                ticksUntilSwarm = DelayTicks;
            }
        }

        public override void GameComponentTick()
        {
            if (swarmDone || ticksUntilSwarm < 0) return;

            ticksUntilSwarm--;
            if (ticksUntilSwarm > 0) return;

            SpawnBugSwarm();
            swarmDone = true;
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
                    "Баги из трюма",
                    "Из обломков корабля выбралась стайка багов и рванула прямо к вам. Тварей немного и они хлипкие, но зубы у них есть. Встречайте гостей.",
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