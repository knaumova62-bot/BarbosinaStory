using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BarbosinaStory
{
    // ============================================================
    // Автовыбор стартового тайла мира для сценария "Барбосина:
    // Дипломатический крах" (BarbosinaStory_Crash).
    //
    // Идея: игрок не должен тыкать по карте мира руками, но и не должен
    // высадиться в океане, на леднике/морском льду или в экстремальной
    // пустыне, а также в зоне с неадекватной средней температурой.
    //
    // Как это сделано (два патча):
    //
    // 1) Patch_TileFinder_RandomStartingTile — Postfix на ванильный
    //    TileFinder.RandomStartingTile(). Этот метод и так уже умеет
    //    находить "settle-совместимый" тайл (расстояния до фракций,
    //    проходимость и т.д.) - мы не лезем в эту логику руками и не
    //    собираем PlanetTile из сырого int (в 1.6/Odyssey это ломко:
    //    появились слои планеты (PlanetLayer), и просто
    //    "new PlanetTile(случайный_индекс)" может указать на
    //    орбитальный/лунный слой, а не на поверхность). Вместо этого мы
    //    просто ПЕРЕСПРАШИВАЕМ у ванильного метода тайл до N раз (через
    //    рекурсивный вызов того же патченного метода, с защитой от
    //    повторного входа в свой же фильтр) и берём первый результат,
    //    прошедший наш фильтр IsHabitable(). Если за N попыток ничего
    //    подходящего не нашлось - молча остаёмся на том, что выдал
    //    ванильный метод последним (т.е. на дефолтном ванильном выборе).
    //
    // 2) Patch_SelectStartingSite_PreOpen — Postfix на
    //    Page_SelectStartingSite.PreOpen(). Здесь мы сами проставляем
    //    Find.GameInitData.startingTile пригодным тайлом (через тот же
    //    TileFinder.RandomStartingTile(), уже отфильтрованный патчем
    //    №1) и пытаемся программно пролистать страницу выбора места
    //    высадки (Page.DoNext()) через рефлексию, чтобы игроку не
    //    пришлось тыкать по карте руками. Если DoNext() через рефлексию
    //    не находится или падает - не критично: тайл уже выбран
    //    правильно, игроку останется только нажать "Далее" самому.
    //
    // ВАЖНО (см. README_BUILD.txt в этой же папке): этот файл написан
    // и вычитан по документации/декомпилированным исходникам 1.6, но
    // НЕ прогнан через реальную компиляцию с настоящими сборками игры
    // (тут просто нет доступа к ним). Если что-то не соберётся -
    // сборка dotnet build -c Release покажет точную ошибку (CS####),
    // пришли её мне текстом - поправлю прицельно, а не гадая.
    // Подозрительные места на этот случай:
    //   - Find.GameInitData.startingTile - имя поля стабильно из
    //     старых версий, но его ТИП в 1.6 сменился с int на PlanetTile;
    //     если у тебя вдруг всё ещё int - просто убери .Tile-обёртки
    //     ниже и работай с int напрямую.
    //   - Page_SelectStartingSite - класс страницы выбора места
    //     высадки; если в 1.6 он переименован/убран - подскажет
    //     ошибка компиляции "не найден тип".
    // ============================================================

    public static class BarbosinaTileFilter
    {
        // Разумный диапазон средней температуры тайла, °C.
        public const float MinAvgTemp = -25f;
        public const float MaxAvgTemp = 40f;

        // Сколько попыток даём случайному подбору, прежде чем сдаться
        // и остаться на дефолтном ванильном выборе.
        public const int MaxAttempts = 200;

        // Явно запрещённые биомы (по defName, чтобы не зависеть от того,
        // какие DLC активны и какие ссылки на *Of-классы доступны).
        private static readonly string[] BadBiomeDefNames =
        {
            "Ocean", "Lake", "IceSheet", "SeaIce", "ExtremeDesert"
        };

        /// <summary>
        /// Проверяет, пригоден ли тайл для высадки: не вода, не лёд,
        /// не экстремальная пустыня, средняя температура в разумных
        /// пределах. Любая ошибка обращения к API трактуется как
        /// "тайл не подходит" (и логируется), а не роняет игру.
        /// </summary>
        public static bool IsHabitable(PlanetTile planetTile)
        {
            try
            {
                if (!planetTile.Valid) return false;

                Tile tile = planetTile.Tile;
                if (tile == null) return false;

                // Вода (океан/озеро/морской лёд как WaterCovered-тайлы).
                if (tile.WaterCovered) return false;

                BiomeDef biome = tile.PrimaryBiome;
                if (biome == null) return false;
                if (!biome.canBuildBase) return false;
                if (BadBiomeDefNames.Contains(biome.defName)) return false;

                // Непроходимые тайлы (горы и т.п.) - тоже мимо.
                if (tile.hilliness == Hilliness.Impassable) return false;

                float avgTemp = tile.temperature;
                if (float.IsNaN(avgTemp)) return false;
                if (avgTemp < MinAvgTemp || avgTemp > MaxAvgTemp) return false;

                return true;
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Ошибка при проверке тайла на пригодность: {e.Message}");
                return false;
            }
        }
    }

    // Патчим сам ванильный метод выбора случайного стартового тайла.
    // Работает только в сценарии BarbosinaStory_Crash - в остальных
    // случаях (ванильные игры, другие сценарии) ведёт себя как обычно.
    [HarmonyPatch(typeof(TileFinder), nameof(TileFinder.RandomStartingTile))]
    public static class Patch_TileFinder_RandomStartingTile
    {
        // Защита от бесконечной рекурсии: пока мы сами перевызываем
        // TileFinder.RandomStartingTile() из своего же Postfix'а, второй
        // (и следующие) вход в этот же Postfix должен просто отдать то,
        // что вернул ванильный метод, без повторной фильтрации.
        [ThreadStatic]
        private static bool isRetrying;

        public static void Postfix(ref PlanetTile __result)
        {
            try
            {
                if (isRetrying) return;
                if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return;

                // Может, ванильному выбору и так повезло - тогда трогать нечего.
                if (BarbosinaTileFilter.IsHabitable(__result)) return;

                isRetrying = true;
                try
                {
                    for (int attempt = 0; attempt < BarbosinaTileFilter.MaxAttempts; attempt++)
                    {
                        PlanetTile candidate = TileFinder.RandomStartingTile();
                        if (BarbosinaTileFilter.IsHabitable(candidate))
                        {
                            __result = candidate;
                            return;
                        }
                    }

                    Log.Warning($"[BarbosinaStory] Не удалось найти пригодный для жизни тайл за {BarbosinaTileFilter.MaxAttempts} попыток, остаюсь на дефолтном ванильном выборе.");
                    // __result остаётся тем, что вернул самый первый (ванильный) вызов.
                }
                finally
                {
                    isRetrying = false;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Ошибка в патче выбора стартового тайла, оставляю ванильный результат: {e}");
            }
        }
    }

    // Автоматически проставляет стартовый тайл при открытии страницы
    // выбора места высадки, чтобы игроку не пришлось тыкать по карте
    // самому, и пытается сама пролистать эту страницу дальше.
    [HarmonyPatch(typeof(Page_SelectStartingSite), "PreOpen")]
    public static class Patch_SelectStartingSite_PreOpen
    {
        public static void Postfix(object __instance)
        {
            try
            {
                // Доп. проверка типа - на случай, если Harmony резолвнёт
                // унаследованный (не переопределённый) метод базового
                // класса Page и патч формально "зацепит" не ту страницу.
                if (!(__instance is Page_SelectStartingSite)) return;
                if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return;
                if (Find.GameInitData == null) return;

                PlanetTile tile = TileFinder.RandomStartingTile();

                Find.GameInitData.startingTile = tile;

                if (Find.WorldInterface != null)
                {
                    Find.WorldInterface.SelectedTile = tile;
                }

                TryAutoAdvance((Page)__instance);
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось автоматически выбрать стартовый тайл: {e}");
            }
        }

        // Пытаемся сами пролистать страницу выбора места высадки, чтобы
        // не заставлять игрока жать "Далее" руками. Метод внутренней
        // навигации может отличаться от билда к билду, поэтому лезем
        // через рефлексию по базовому классу Page и аккуратно
        // откатываемся, если что-то не найдено - в этом случае тайл
        // всё равно уже выбран правильно, и хватит одного клика "Далее".
        private static void TryAutoAdvance(Page page)
        {
            try
            {
                MethodInfo doNext = AccessTools.Method(typeof(Page), "DoNext");
                if (doNext != null)
                {
                    doNext.Invoke(page, null);
                    return;
                }

                Log.Warning("[BarbosinaStory] Не нашёл Page.DoNext() через рефлексию - тайл уже выбран автоматически, но страницу выбора места высадки придётся пролистать вручную (\"Далее\").");
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось автоматически пролистать страницу выбора места высадки: {e}. Тайл уже выбран правильно, нажмите \"Далее\" вручную.");
            }
        }
    }

    // ============================================================
    // Отдельная проблема, не связанная с самим выбором тайла: пока
    // игрок настраивает персонажей/идеологию и т.д., и даже пока идёт
    // генерация локальной карты после "Начать!", RimWorld по умолчанию
    // продолжает рисовать глобус на фоне (это ванильное поведение,
    // просто раньше игрок не замечал, пока сам неспешно тыкал по карте
    // мира). Раз мы теперь выбираем тайл автоматически и без паузы на
    // разглядывание глобуса - глушим его отрисовку на всё время визарда
    // создания игры, только для нашего сценария.
    //
    // Патчим WorldRendererUtility.WorldRendered - это единая точка,
    // которую опрашивает и сама отрисовка слоёв глобуса, и остальные
    // системы, решающие, показывать ли мир. Форсим false, только пока
    // мы всё ещё в "мастере создания игры" (ProgramState.Entry,
    // GameInitData уже существует) и сценарий - наш. На реальный
    // геймплей (ProgramState.Playing, когда игрок сам открывает карту
    // мира из колонии) это никак не влияет.
    // ============================================================
    [HarmonyPatch(typeof(WorldRendererUtility), "get_WorldRendered")]
    public static class Patch_WorldRendererUtility_WorldRendered
    {
        public static void Postfix(ref bool __result)
        {
            try
            {
                if (!__result) return;
                if (Current.ProgramState != ProgramState.Entry) return;
                if (Find.GameInitData == null) return;
                if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return;

                __result = false;
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Ошибка в патче скрытия глобуса на визарде создания игры: {e}");
            }
        }
    }
}
