Как собрать BarbosinaPawnPresets.dll
=====================================

1. Установи .NET SDK (если ещё нет) — dotnet.microsoft.com, версия 6/7/8, любая с net472 таргетом в наличии.

2. Открой Source/BarbosinaPawnPresets/BarbosinaPawnPresets.csproj и поправь три HintPath под свою систему:
   - Assembly-CSharp.dll и UnityEngine.CoreModule.dll — лежат в
     <папка игры>/RimWorldWin64_Data/Managed/
   - 0Harmony.dll — лежит в папке подписанного мода Harmony (workshop id 2009463077),
     обычно steamapps/workshop/content/294100/2009463077/Current/Assemblies/

3. В терминале:
   cd Source/BarbosinaPawnPresets
   dotnet build -c Release

   Собранный BarbosinaPawnPresets.dll сам ляжет в 1.6/Assemblies/ (это прописано
   в csproj через OutputPath) — руками копировать никуда не нужно.

4. Убедись, что в списке модов игры включены (в таком порядке):
   Harmony -> BarbosinaStory: Дипломатический крах

5. Запусти игру, выбери сценарий "Барбосина: Дипломатический крах",
   на экране "Настроить стартовых персонажей" все 6 слотов должны сразу
   показывать готовых Кумара/Фостера/Акаси/Барбоса/Киткат/Хмурого с
   именами, чертами, навыками и биографиями из скрипта.

Если игра при старте пишет в консоли/логе ошибку от Harmony вида
"Patching exception... Method 'NewGeneratedStartingPawn' not found" —
значит в твоей сборке 1.6 сигнатура метода отличается. Скинь мне:
 - точный текст ошибки из Player.log, или
 - сигнатуру метода StartingPawnUtility.NewGeneratedStartingPawn из
   декомпилятора (dnSpy/ILSpy, открой Assembly-CSharp.dll ->
   RimWorld -> StartingPawnUtility) —
и я поправлю патч под неё.

6. Про StartingTilePatch.cs (автовыбор стартового тайла, добавлен отдельно):
   этот файл НЕ прогонялся через реальную компиляцию с настоящими
   сборками игры (сборка Assembly-CSharp.dll есть только у тебя локально).
   Он написан по документации/декомпилированным исходникам 1.6 и должен
   собраться, но если dotnet build выдаст ошибку CS#### именно по этому
   файлу — скинь мне точный текст ошибки, поправлю прицельно. Особо
   подозрительные места (см. комментарий в начале файла):
   - Find.GameInitData.startingTile — тип поля в 1.6 сменился с int на
     PlanetTile; если у тебя вдруг всё ещё int, скажи — уберу лишние
     обёртки под PlanetTile;
   - класс Page_SelectStartingSite (страница выбора места высадки) —
     если переименован, компилятор укажет "тип не найден".

7. Про Patch_WorldRendererUtility_WorldRendered (глушит глобус на фоне
   визарда создания игры): патчит геттер WorldRendererUtility.WorldRendered.
   Если после сборки глобус всё равно где-то мелькает (например на
   странице настройки идеологии, если у тебя активен DLC Ideology, или
   в конкретный момент самой генерации карты) — напиши, на каком именно
   экране/шаге это видно, и я добавлю туда точечный патч, а не буду
   гадать заранее.
