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
    // Как это сделано (четыре патча):
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
    //    №1). PreOpen вызывается КАЖДЫЙ раз, когда страница попадает в
    //    WindowStack - в том числе повторно, если игрок нажал "Назад"
    //    со страницы настройки персонажей, - поэтому реролл тайла
    //    срабатывает заново и при возврате. Больше ничего, кроме
    //    простановки тайла, этот патч не делает: сам переход дальше
    //    вынесен в патч №4, потому что вызывать Page.DoNext() прямо из
    //    PreOpen ненадёжно (окно на этот момент ещё не встало в
    //    WindowStack).
    //
    // 3) Patch_SelectStartingSite_CanDoNext — Prefix на
    //    Page_SelectStartingSite.CanDoNext(). В нашем сценарии сразу
    //    форсит __result = true и пропускает оригинальный метод (return
    //    false), убирая диалог-подтверждение о близости к другим
    //    фракциям и любые прочие блокирующие проверки на этой странице.
    //    Вне нашего сценария не трогает ничего.
    //
    // 4) Patch_SelectStartingSite_PostOpen — Postfix на
    //    Page_SelectStartingSite.PostOpen(). В отличие от PreOpen, этот
    //    метод вызывается уже ПОСЛЕ того, как страница реально попала в
    //    WindowStack, поэтому именно отсюда безопасно инициировать
    //    переход на следующую страницу. Сам вызов CanDoNext()/DoNext()
    //    откладывается через LongEventHandler.ExecuteWhenFinished (так
    //    же поступают другие моды, патчащие этот метод), а сами методы
    //    достаются рефлексией по фактическому типу страницы - если
    //    что-то не находится или падает, тайл всё равно уже выбран
    //    патчем №2, и игроку останется нажать "Далее" самому.
    //
    // ВАЖНО (см. README_BUILD.txt в этой же папке): этот файл написан
    // и вычитан по документации/декомпилированным исходникам 1.6, но
    // НЕ прогнан через реальную компиляцию с настоящими сборками игры
    // (тут просто нет доступа к ним). Патчи №3 и №4 резолвятся по
    // строковому имени метода через Harmony/рефлексию, так что сами по
    // себе они не дадут ошибку компиляции, даже если сигнатура
    // CanDoNext/PostOpen в твоей сборке 1.6 будет другой - но тогда они
    // тихо не сработают (см. лог: "не нашёл ... через рефлексию") и
    // автопролистывание откатится на ручное. Если dotnet build выдаст
    // ошибку CS#### именно по этому файлу - пришли её мне текстом,
    // поправлю прицельно. Подозрительные места на этот случай:
    //   - Find.GameInitData.startingTile - имя поля стабильно из
    //     старых версий, но его ТИП в 1.6 сменился с int на PlanetTile;
    //     если у тебя вдруг всё ещё int - просто убери .Tile-обёртки
    //     ниже и работай с int напрямую.
    //   - Page_SelectStartingSite - класс страницы выбора места
    //     высадки; если в 1.6 он переименован/убран - подскажет
    //     ошибка компиляции "не найден тип".
    //   - CanDoNext()/PostOpen() - имена методов стабильны в известных
    //     мне декомпилированных источниках 1.6, но если в логе игры при
    //     старте будет "Patching exception... Method 'CanDoNext' not
    //     found" (или PostOpen) - скинь точный текст ошибки и/или
    //     сигнатуру метода из декомпилятора (dnSpy/ILSpy), поправлю.
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
    // самому. PreOpen вызывается заново при каждом попадании страницы в
    // WindowStack, в том числе при возврате назад со страницы настройки
    // персонажей - поэтому реролл тайла происходит и при возврате тоже.
    // Сам переход на следующую страницу сюда намеренно не добавлен (см.
    // Patch_SelectStartingSite_PostOpen ниже) - на момент PreOpen окно
    // ещё не встало в WindowStack, и вызов DoNext() отсюда ненадёжен.
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
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось автоматически выбрать стартовый тайл: {e}");
            }
        }
    }

    // Убирает блокирующие диалоги/проверки на странице выбора места
    // высадки (в первую очередь - подтверждение "это место отстоит от
    // поселений других фракций всего на N или менее клеток, всё равно
    // высадиться здесь?"), только в нашем сценарии. Работает как
    // Prefix: если сценарий наш, сразу форсит __result = true и
    // пропускает оригинальный CanDoNext() (return false из Prefix'а
    // означает "не выполнять оригинальный метод"). Вне нашего сценария
    // ничего не меняет - оригинальный CanDoNext() отрабатывает как
    // обычно.
    [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
    public static class Patch_SelectStartingSite_CanDoNext
    {
        public static bool Prefix(object __instance, ref bool __result)
        {
            try
            {
                if (!(__instance is Page_SelectStartingSite)) return true;
                if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return true;

                __result = true;
                return false;
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Ошибка в патче CanDoNext страницы выбора места высадки, использую ванильную проверку: {e}");
                return true;
            }
        }
    }

    // Надёжно пролистывает страницу выбора места высадки вперёд, только
    // в нашем сценарии. В отличие от PreOpen, PostOpen вызывается уже
    // ПОСЛЕ того, как страница реально попала в WindowStack - это
    // безопасная точка, откуда можно инициировать переход дальше.
    // Сам переход откладывается через LongEventHandler.ExecuteWhenFinished
    // (тот же приём используют и другие моды, патчащие этот метод), а
    // CanDoNext()/DoNext() достаются рефлексией по фактическому типу
    // страницы. Если что-то не находится или падает - не критично: тайл
    // уже выбран патчем Patch_SelectStartingSite_PreOpen, и игроку
    // останется нажать "Далее" самому.
    [HarmonyPatch(typeof(Page_SelectStartingSite), "PostOpen")]
    public static class Patch_SelectStartingSite_PostOpen
    {
        public static void Postfix(object __instance)
        {
            try
            {
                if (!(__instance is Page_SelectStartingSite)) return;
                if (!GameComponent_BarbosinaBugs.IsBarbosinaScenario()) return;
                if (Find.GameInitData == null) return;

                Page page = (Page)__instance;
                LongEventHandler.ExecuteWhenFinished(() => TryAutoAdvance(page));
            }
            catch (Exception e)
            {
                Log.Warning($"[BarbosinaStory] Не удалось запланировать автоматическое пролистывание страницы выбора места высадки: {e}");
            }
        }

        // Дублирует то, что делает клик по кнопке "Далее": сначала
        // спрашивает CanDoNext() (в нашем сценарии он уже форсирован в
        // true патчем Patch_SelectStartingSite_CanDoNext, но на случай
        // непредвиденной ещё одной блокирующей проверки честно
        // уважаем false, а не долбим DoNext() силой) и только потом
        // вызывает DoNext().
        private static void TryAutoAdvance(Page page)
        {
            try
            {
                MethodInfo canDoNext = AccessTools.Method(page.GetType(), "CanDoNext");
                if (canDoNext != null)
                {
                    object canDoNextResult = canDoNext.Invoke(page, null);
                    if (canDoNextResult is bool allowed && !allowed)
                    {
                        Log.Warning("[BarbosinaStory] CanDoNext() вернул false несмотря на патч - страницу выбора места высадки придётся пролистать вручную (\"Далее\").");
                        return;
                    }
                }

                MethodInfo doNext = AccessTools.Method(page.GetType(), "DoNext");
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
}
