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
