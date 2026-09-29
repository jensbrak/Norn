//
// Vade - a simple tool to extract data from Valheim to be used with Norn.
// See readme for context.
//

using CsvHelper;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

// Debug directories for source/destination, if not null take precedence over command line arguments
string? SourceDirectoryDebug = null;
string? DestinationDirectoryDebug = null;


// Directory and files used for input and output
const string LOCALIZATION_DIRECTORY = "Resources";
const string PREFAB_DIRECTORY = "PrefabInstance";
const string PREFAB_FILE_EXTENSION = ".prefab";
const string RECIPE_DIRECTORY = "MonoBehaviour";
const string RECIPE_FILE_EXTENSION = ".asset";
const string METAFILE_EXTENSION = ".meta"; // Not recipe-specific: every Unity asset (recipes and items alike) gets one
const string RECIPE_FILENAME_PREFIX = "Recipe_";

// Holds ObjectDB, the authority for both lists that matter here: m_items (every prefab
// the game registers as an inventory item) and m_recipes.
const string ACTIVE_AUTHORITY_FILENAME = "_GameMain.prefab";
const string PLAYER_PREFAB_FILENAME = "Player.prefab"; // Lists the seasons (Yule, Midsummer, ...)

// A player-facing item's m_name is a localization token ("$item_amber"); an internal one
// is a plain literal ("Club" on GoblinClub, "Swingattack" on Abomination_attack1). That
// prefix is what separates them - NOT whether the token happens to resolve, which is a
// different question with a different answer whenever a localization file is missing.
const string LOCALIZATION_TOKEN_PREFIX = "$";
const string ITEM_DATA_EXPORT_FILE = "SharedItemData.csv";
const string LOCALIZATION_DATA_EXPORT_FILE = "LocalizationData.csv";
const string RECIPE_DATA_EXPORT_FILE = "RecipeData.json";
const string PIECE_DATA_EXPORT_FILE = "PieceData.json";
// The Feaster's table is left out: its "pieces" are food set down on a table, and the only
// resource is the food item itself.
string[] PIECE_TABLES = ["_HammerPieceTable", "_HoePieceTable", "_CultivatorPieceTable"];
const string FALLBACK_DIRECTORY = @".\";
string[] LOCALIZATION_FILES = ["localization.txt", "localization_extra.txt", "localization_celebrationupdate.txt", "localization_deepnorth.txt"];

// Command line argument switch definitions
string[] SwitchHelp = ["/?", "/h", "--help"];
string[] SwitchVerbose = ["/v", "--verbose"];
// Every output is written by default - Norn needs all of them - so the switches opt out.
string[] SwitchNoLocalization = ["/n", "--no-localization"];
string[] SwitchNoRecipes = ["/r", "--no-recipes"];
string[] SwitchNoPieces = ["/p", "--no-pieces"];

// Sanity check constants. A silent collapse is the failure mode worth guarding: an
// extraction that returns far too little still writes a well-formed CSV, and every
// consumer downstream then quietly degrades instead of failing.
int MIN_EXPECTED_RECIPE_COUNT = 300;
int MIN_EXPECTED_ITEM_COUNT = 900;
int MIN_EXPECTED_PIECE_COUNT = 350;

// -----------------------------------------------------------------------------------------------
// Main 
// -----------------------------------------------------------------------------------------------

// Setup paths to files and directories needed
var SourceDirectory = SourceDirectoryDebug ?? (args.Length > 0 ? args[0] : FALLBACK_DIRECTORY);
var DestinationDirectory = DestinationDirectoryDebug ?? (args.Length > 1 ? args[1] : FALLBACK_DIRECTORY);
var LocalizationFiles = LOCALIZATION_FILES.Select(f => Path.GetFullPath(Path.Combine(SourceDirectory, LOCALIZATION_DIRECTORY, f))).ToArray() ?? [];
var PrefabDirectory = Path.GetFullPath(Path.Combine(SourceDirectory, PREFAB_DIRECTORY));
var RecipeDirectory = Path.GetFullPath(Path.Combine(SourceDirectory, RECIPE_DIRECTORY));
var ActiveAuthorityFile = Path.GetFullPath(Path.Combine(PrefabDirectory, ACTIVE_AUTHORITY_FILENAME));

// Setup containers for data extraction and output
var LocalizationData = new Dictionary<string, string>();
var SharedItemData = new List<ItemData>();
var Recipes = new List<Recipe>();
var Pieces = new List<Piece>();
var PrefabsByGuid = new Dictionary<string, Prefab?>();


// Needs to go first since console printing depends on it
var VerboseMode = SwitchEnabled(SwitchVerbose);

// Print help results in no further processing and program exit
if (SwitchEnabled(SwitchHelp))
{
    ShowHelpThenExit();
}

// Make sure we have what we need to start the extraction
EnsureArguments();
EnsureDirectory(SourceDirectory);
EnsureFiles(LocalizationFiles);
EnsureDirectory(PrefabDirectory);
EnsureDirectory(DestinationDirectory);
EnsureDirectory(RecipeDirectory);
EnsureFile(ActiveAuthorityFile);
var PrefabPathsByGuid = BuildGuidIndex(PrefabDirectory);
var Seasons = SeasonsByGuid();

// Data extration steps. Everything is validated before anything is written, so a failed
// run never leaves a mix of fresh and stale files behind.
Inform("Extracting item data...");
ExtractLocalizationData();
ExtractItemData();
ExtractRecipeData();
ApplyCrafterTagData();
ExtractPieceData();
ValidateRecipeData();
ValidatePieceData();
SaveItemData();
SaveLocalizationData();
SaveRecipeData();
SavePieceData();
Inform("Item data extracted successfully");

// -----------------------------------------------------------------------------------------------
// Functions
// -----------------------------------------------------------------------------------------------

// Switch enabled or not?
bool SwitchEnabled(string[] sw) => args.Length > 2 && args[2..].Any(a => sw.Any(s => a.ToLower().Equals(s)));

// Print information message as verbose or normal
void Inform(string message, bool isVerbose = false, bool noNewline = false)
{
    if (!VerboseMode && isVerbose)
    {
        return;
    }
    if (noNewline)
    {
        Console.Write(message);
    }
    else
    {
        Console.WriteLine(message);
    }
}

// Exit program prematurely after printing reason (error message if exit code represents an error, ie is non zero)
void Exit(string reason, int exitCode = 0)
{
    Inform(exitCode != 0 ? $"ERROR! {reason}\nExtraction aborted!" : reason);
    Environment.Exit(exitCode);
}

// Show help message and then exit program gracefully
void ShowHelpThenExit() =>
    Exit("Extracts Valheim data from unpacked Unity assets.\n\n" +
        $"{AppDomain.CurrentDomain.FriendlyName} source destination [{SwitchVerbose[0]}] [{SwitchNoLocalization[0]}] [{SwitchNoRecipes[0]}] [{SwitchNoPieces[0]}]\n\n" +
        $"  {"source",-12} Specifies the source directory, ie the assets root directory to extract data from.\n" +
        $"  {"destination",-12} Specifies the destination directory, ie the directory to write data to.\n" +
        $"  {SwitchVerbose[0],-12} Verbose mode: print additional info\n" +
        $"  {SwitchNoLocalization[0],-12} Skip the localization data file, which is written by default\n" +
        $"  {SwitchNoRecipes[0],-12} Skip the recipe data file, which is written by default\n" +
        $"  {SwitchNoPieces[0],-12} Skip the piece data file, which is written by default\n" +
        $"\nAsset files has to be unpacked from the game beforehand, using some other tool.\n" +
        $"AssetRipper has been verified to work for this.\n\n");

// Verify that we have proper arguments given. No validation, just that they are provided as needed
void EnsureArguments()
{
    if (args.Length >= 2 || SourceDirectoryDebug != null && DestinationDirectoryDebug != null)
    {
        // We need both source and destination directories, from command line or by debug variables
        return;
    }
    Exit("Missing arguments: both source and destination directory required", 1);
}

// Verify that a directory exists/is accessible and if not exit program immediately with error message
void EnsureDirectory(string path)
{
    if (Directory.Exists(path))
    {
        return;
    }
    Exit($"Directory does not exist or is inaccessible: '{path}", 2);
}

// Verify that a file exists/is accessible and if not exit program immediately with error message
void EnsureFile(string path)
{
    if (File.Exists(path))
    {
        return;
    }
    Exit($"File does not exist or is inaccessible: '{path}'", 3);
}

void EnsureFiles(string[] paths)
{
    foreach (var path in paths)
    {
        EnsureFile(path);
    }
}

// Load English localization from main localization files into dictionary with names as keys and corresponding translations as values
void ExtractLocalizationData()
{
    Inform($"Extracting localization data from {LocalizationFiles.Length} files in {Path.GetDirectoryName(LocalizationFiles[0])}...", isVerbose: true);
    var translationCount = 0; ;
    for (int i = 0; i < LocalizationFiles.Length; i++)
    {
        string localizationFile = LocalizationFiles[i];
        Inform($"{"",2}[{i + 1,4}] Processing '{Path.GetFileName(localizationFile)}'...", noNewline: true, isVerbose: true);

        // Seems localization file is a CSV (but with plenty of duplicates and empty values)
        using (var reader = new StreamReader(localizationFile))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            while (csv.Read())
            {
                var key = csv[0];
                var value = csv[1];
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || LocalizationData.ContainsKey(key))
                {
                    continue;
                }
                LocalizationData.Add(key, value);
                translationCount++;
            }
        }
        Inform($"{translationCount} translations added.", isVerbose: true);
        translationCount = 0;
    }
    Inform($"{LocalizationData.Count} t ranslations loaded.");
}

// Extract every item ObjectDB registers, classifying each as player-facing or internal
// by its m_name. Three separate questions get asked in order, and keeping them separate
// is the whole point of this pass:
//
//   1. Is it a registered item?     -> it's in ObjectDB.m_items. Nothing else qualifies.
//   2. Is it player-facing?         -> its m_name is a localization token ($-prefixed).
//   3. What is it called?           -> look the token up; fall back to the token itself.
//
// Answering all three with "has a resolvable translation" conflates "is an item" with
// "can be named": a missing localization file then silently deletes content.
void ExtractItemData()
{
    var itemGuids = AuthoritiveItemGuids();

    string[] keys = ["m_name", "m_teleportable", "m_useDurability", "m_maxDurability", "m_durabilityPerLevel", "m_maxStackSize", "m_maxQuality", "m_itemType", "m_weight", "m_scaleWeightByQuality"];

    var internalCount = 0;
    var unresolvedGuidCount = 0;
    var untranslatedCount = 0;
    var ignoredCount = 0;
    var tempData = new Dictionary<string, string>(keys.Length);

    Inform($"{itemGuids.Count} registered items to process...", isVerbose: true);

    for (int i = 0; i < itemGuids.Count; i++)
    {
        // An authoritive guid with no prefab on disk is a broken rip, not a game fact -
        // worth saying out loud rather than quietly producing a smaller catalog.
        if (!PrefabPathsByGuid.TryGetValue(itemGuids[i], out var filename))
        {
            unresolvedGuidCount++;
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: registered item guid {itemGuids[i]} has no prefab under {PREFAB_DIRECTORY}");
            continue;
        }

        var itemIdCandidate = Path.GetFileNameWithoutExtension(filename); // We will get item id from within the file, but only if it's an item data candidate
        var fileText = File.ReadAllText(filename);
        var itemDataSections = fileText.Split("m_itemData:\n");
        if (itemDataSections.Length < 2)
        {
            // No item data in this prefab file means not an inventory item at all. Skip to next
            ignoredCount++;
            Inform($"{"",2}[{i + 1,4}] {"Ignored",9}: {itemIdCandidate} [Reason: no item data]", isVerbose: true);
            continue;
        }

        // Extract data for this item
        tempData.Clear();
        foreach (var line in itemDataSections[1].Split("\n").Where(line => line.Contains(':') && keys.Any(id => line.Contains($"{id}:"))))
        {
            var parts = line.Split(':', 2); // Some values are strings with ':' in them. We don't want to split them, take first split only
            var key = parts[0].Trim();  // Localization key, not item id
            var value = parts[1].Trim([' ', '\t']); // The '$' on m_name is kept: it's the classification signal, stripped at lookup instead
            if (tempData.TryGetValue(key, out string? valueInData) && value != valueInData)
            {
                // Only first occurence; as of 0.221.4 (Call To Arms) multiple m_Name occurs, where succeeding "" would break localization check
                // Also: isVerbose -> true since this item is skipped
                Inform($"{"",2}[{i + 1,4}] {"Ignored",9}: {itemIdCandidate} [Reason: DUPE key {key} with '{value}' != '{valueInData}'", isVerbose: true);
                continue;
            }
            tempData[key] = value;
        }

        // m_name is the localization reference for player-facing items and a plain
        // internal label for everything else - see LOCALIZATION_TOKEN_PREFIX.
        var itemNameToken = tempData[keys[0]];
        if (!itemNameToken.StartsWith(LOCALIZATION_TOKEN_PREFIX))
        {
            // Registered, but not something a player ever sees named: creature gear
            // (GoblinClub, DvergerStaffFire) and attack prefabs (Abomination_attack1).
            internalCount++;
            Inform($"{"",2}[{i + 1,4}] {"Internal",9}: {itemIdCandidate} ({itemNameToken}) [Reason: m_name is not a localization token]", isVerbose: true);
            continue;
        }

        // A token with no translation is still a real item (the game ships a few, e.g.
        // TrophyDeerWhite). Fall back to the raw token: distinct and searchable.
        var itemText = ResolveTokenText(itemNameToken);
        if (itemText == null)
        {
            untranslatedCount++;
            itemText = itemNameToken;
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: {itemIdCandidate} ({itemNameToken}) has no translation; using the token as its name");
        }

        // Now get the actual item name - the root GameObject's m_Name, which is what the
        // game hashes. See RootGameObjectName.
        var rootName = RootGameObjectName(fileText);
        if (rootName == null)
        {
            Inform($"{"",2}[{i + 1,4}] {"Discared",9}:  {itemIdCandidate} ({itemNameToken}) [Reason: no root GameObject found]", isVerbose: true);
            continue;
        }

        // Informational only
        if (rootName != itemIdCandidate)
        {
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: Root GameObject name '{rootName}' does not match prefab filename '{itemIdCandidate}'", isVerbose: false);
        }

        // We have the name. This is the name the game itself hashes (m_dropPrefab.name),
        // so it must come from the GameObject and never from the filename - the rip
        // suffixes colliding filenames (Amber.prefab / Amber_0.prefab) and only one of
        // the pair is the registered item.
        var itemName = rootName;

        var item = new ItemData
        {
            ItemName = itemName,
            IsTeleportable = Convert.ToInt32(tempData[keys[1]]) != 0,
            UsesDurability = Convert.ToInt32(tempData[keys[2]]) != 0,
            MaxDurability = Convert.ToInt32(tempData[keys[3]]),
            DurabilityPerLevel = Convert.ToInt32(tempData[keys[4]]),
            MaxStack = Convert.ToInt32(tempData[keys[5]]),
            DisplayName = itemText,
            MaxQuality = Convert.ToInt32(tempData[keys[6]]),
            ItemType = Convert.ToInt32(tempData[keys[7]]),
            Weight = Convert.ToDecimal(tempData[keys[8]]),
            ScaleWeightByQuality = Convert.ToDecimal(tempData[keys[9]]),
            CanHaveCrafterTag = false // Will be set in a later pass
        };
        SharedItemData.Add(item);
        Inform($"{"",2}[{i + 1,4}] {"Added",9}: {itemName} ({itemText})", isVerbose: true);
    }
    Inform($"{itemGuids.Count} registered items processed: {SharedItemData.Count} player-facing, {internalCount} internal, {ignoredCount} without item data.");
    if (untranslatedCount > 0)
    {
        Inform($"WARNING: {untranslatedCount} player-facing item(s) had no translation and are named by their token. A localization file may be missing from LOCALIZATION_FILES.");
    }
    if (unresolvedGuidCount > 0)
    {
        Inform($"WARNING: {unresolvedGuidCount} registered item guid(s) had no prefab on disk. The asset rip may be incomplete.");
    }
    if (SharedItemData.Count < MIN_EXPECTED_ITEM_COUNT)
    {
        Exit($"Only {SharedItemData.Count} items extracted, expected at least {MIN_EXPECTED_ITEM_COUNT}.", 9);
    }
}

// The item half of ObjectDB, read from the same authority file and the same MonoBehaviour
// section as AuthoritiveRecipeUids. m_items is emitted immediately before m_recipes, which
// is what bounds the split - an open-ended read would run on into the recipe guids.
List<string> AuthoritiveItemGuids()
{
    var fileText = File.ReadAllText(ActiveAuthorityFile);
    var fileSections = GetUnityFileSections(fileText);
    var objectDb = fileSections.Values.FirstOrDefault(b => b.ClassId == "114" && b.Body.Contains("m_recipes"));
    var itemLines = objectDb.Body.Split("m_items:")[1].Split("m_recipes:")[0].Split('\n').Where(line => line.Contains("guid:"));

    var itemGuids = itemLines.Select(line =>
    {
        var values = line.Split([' ', '-', '{', '}', ':', ','], StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 4 || values[3].Trim().Length != 32) // UID is 32 characters long
        {
            Exit($"Could not parse item guid from '{ActiveAuthorityFile}'.", 10);
            return ""; // Won't be used, but needed to satisfy compiler
        }
        return values[3].Trim();
    });

    // Distinct: a prefab may legitimately appear twice in m_items, and processing it twice
    // would write the same item to the CSV twice.
    var distinctGuids = itemGuids.Distinct().ToList();
    Inform($"{distinctGuids.Count} items registered in ObjectDB.");
    return distinctGuids;
}

string[] CollectRecipeFiles()
{
    string recipeFilePattern = $"{RECIPE_FILENAME_PREFIX}*{RECIPE_FILE_EXTENSION}";
    Inform($"Collecting recipe files in '{Path.Combine(RECIPE_DIRECTORY, recipeFilePattern)}'...");
    var recipeFiles = Directory.GetFiles(RecipeDirectory, recipeFilePattern, SearchOption.TopDirectoryOnly);
    if (recipeFiles.Length == 0)
    {
        Exit($"No recipe files ({recipeFilePattern}) found in: '{RecipeDirectory}'.", 7);
    }
    return recipeFiles;
}

// The recipe half of ObjectDB: the guid of every recipe the game registers.
List<string> AuthoritiveRecipeUids()
{
    var fileText = File.ReadAllText(ActiveAuthorityFile);
    var fileSections = GetUnityFileSections(fileText);
    var recipeGameObject = fileSections.Values.FirstOrDefault(b => b.ClassId == "114" && b.Body.Contains("m_recipes"));
    var recipeLines = recipeGameObject.Body.Split("m_recipes:")[1].Split("m_terrainOps")[0].Split('\n').Where(line => line.Contains("guid:"));

    // Quick sanity check
    if (recipeLines.Count() < MIN_EXPECTED_RECIPE_COUNT)
    {
        Inform($"WARNING: Only {recipeLines.Count()} recipes found in authority file '{ActiveAuthorityFile}', no recipe data extracted.");
        return [];
    }

    // Early fail on bad recipe guid data
    var recipeUids = recipeLines.Select(line =>
    {
        var values = line.Split([' ', '-', '{', '}', ':', ','], StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 4 || values[3].Trim().Length != 32) // UID is 32 characters long
        {
            Exit($"Could not parse recipe guid from '{ActiveAuthorityFile}'.", 6);
            return ""; // Won't be used, but needed to satisfy compiler
        }
        return values[3].Trim();
    });

    return [.. recipeUids];
}

// Every enabled recipe ObjectDB registers, with each prefab reference resolved to a name.
// Guids are never kept: AssetRipper assigns new ones on every rip.
void ExtractRecipeData()
{
    var authoritiveRecipeUids = AuthoritiveRecipeUids();
    if (authoritiveRecipeUids.Count == 0)
    {
        return; // AuthoritiveRecipeUids already warned
    }

    var recipeFiles = CollectRecipeFiles();
    if (recipeFiles.Length < authoritiveRecipeUids.Count)
    {
        Exit($"Too few recipe files ({recipeFiles.Length}) found in: '{RecipeDirectory}', expected at least {authoritiveRecipeUids.Count}", 8);
    }

    var disabledCount = 0;
    var noItemCount = 0;
    var unresolvedCount = 0;

    for (var i = 0; i < recipeFiles.Length; i++)
    {
        var recipeFile = recipeFiles[i];
        var recipeName = Path.GetFileNameWithoutExtension(recipeFile);

        // Remove() doubles as the membership check and as bookkeeping: whatever is left
        // afterward is an authoritive guid that never matched a file.
        var recipeGuid = MetaGuid($"{recipeFile}{METAFILE_EXTENSION}");
        if (!authoritiveRecipeUids.Remove(recipeGuid))
        {
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {recipeName} [Reason: not in authoritive list]", isVerbose: true);
            continue;
        }

        var (header, requirementBlocks) = SplitRequirements(File.ReadAllText(recipeFile));

        // The Recipe script's own m_enabled (lowercase), not the MonoBehaviour header's m_Enabled.
        if (FieldValue(header, "m_enabled") != "1" && !Seasons.ContainsKey(recipeGuid))
        {
            disabledCount++;
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {recipeName} [Reason: recipe disabled]", isVerbose: true);
            continue;
        }

        var itemGuid = ReferencedGuid(FieldValue(header, "m_item"));
        if (itemGuid == null)
        {
            noItemCount++;
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {recipeName} [Reason: m_item is a null reference]", isVerbose: true);
            continue;
        }

        var item = ResolvePrefab(itemGuid);
        var stationGuid = ReferencedGuid(FieldValue(header, "m_craftingStation"));
        var station = ResolvePrefab(stationGuid);
        var requirementItems = requirementBlocks.Select(b => ResolvePrefab(ReferencedGuid(FieldValue(b, "m_resItem", depth: 2)))).ToList();
        if (item == null || (stationGuid != null && station == null) || requirementItems.Any(r => r == null))
        {
            unresolvedCount++;
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: {recipeName} [Reason: a prefab reference did not resolve under {PREFAB_DIRECTORY}]");
            continue;
        }

        Recipes.Add(new Recipe(
            Name: FieldValue(header, "m_Name"),
            Item: item.Name,
            Amount: int.Parse(FieldValue(header, "m_amount")),
            Station: station?.Name,
            StationToken: station?.Token,
            MinStationLevel: int.Parse(FieldValue(header, "m_minStationLevel")),
            Craftable: FieldValue(header, "m_noCraftOnlyUpgrade") != "1",
            RequireOnlyOneIngredient: FieldValue(header, "m_requireOnlyOneIngredient") == "1",
            Season: Seasons.GetValueOrDefault(recipeGuid),
            Resources: [.. requirementBlocks.Select((b, r) => new Requirement(
                Item: requirementItems[r]!.Name,
                Amount: int.Parse(FieldValue(b, "m_amount", depth: 2)),
                AmountPerLevel: int.Parse(FieldValue(b, "m_amountPerLevel", depth: 2)),
                Upgrader: FieldValue(b, "m_upgraderResource", depth: 2) == "1"))]));
        Inform($"{"",2}[{i + 1,4}] {"Added",9}: {recipeName}", isVerbose: true);
    }

    if (authoritiveRecipeUids.Count > 0)
    {
        Inform($"WARNING: {authoritiveRecipeUids.Count} authoritive recipe guid(s) never matched a file on disk.");
    }
    Inform($"{Recipes.Count} recipes extracted: {disabledCount} disabled, {noItemCount} without an item, {unresolvedCount} unresolved.");
}

// An item can carry a crafter tag iff some extracted (enabled, registered) recipe produces it.
void ApplyCrafterTagData()
{
    var taggableItemNames = Recipes.Select(r => r.Item).ToHashSet();

    // Index-based read-modify-write: ItemData is a struct, so SharedItemData[i] is a copy.
    var taggedCount = 0;
    for (var i = 0; i < SharedItemData.Count; i++)
    {
        var item = SharedItemData[i];
        if (taggableItemNames.Contains(item.ItemName))
        {
            item.CanHaveCrafterTag = true;
            SharedItemData[i] = item;
            taggedCount++;
        }
    }
    Inform($"{taggedCount} of {SharedItemData.Count} items flagged as CanHaveCrafterTag ({taggableItemNames.Count} distinct recipe outputs).");
}

// Every buildable piece the Hammer, Hoe and Cultivator offer, references resolved to names.
void ExtractPieceData()
{
    if (SwitchEnabled(SwitchNoPieces))
    {
        return;
    }

    var disabledCount = 0;
    var noResourcesCount = 0;
    var unresolvedCount = 0;

    foreach (var table in PIECE_TABLES)
    {
        var tableFile = Path.Combine(PrefabDirectory, $"{table}{PREFAB_FILE_EXTENSION}");
        EnsureFile(tableFile);
        var tool = table.Trim('_').Replace("PieceTable", "");
        var pieceTable = GetUnityFileSections(File.ReadAllText(tableFile)).Values.First(s => s.Body.Contains("\n  m_pieces:"));
        var pieceGuids = SplitList(pieceTable.Body, "m_pieces").Select(ReferencedGuid).ToList();

        foreach (var guid in pieceGuids)
        {
            var piece = ResolvePrefab(guid);
            if (piece == null)
            {
                unresolvedCount++;
                Inform($"{"",2}{"WARNING",9}: {tool} piece {guid} [Reason: no prefab with a root GameObject under {PREFAB_DIRECTORY}]");
                continue;
            }

            // The Piece component: the section carrying the build fields.
            var pieceSection = GetUnityFileSections(File.ReadAllText(PrefabPathsByGuid[guid!])).Values
                .First(s => s.Body.Contains("\n  m_resources:") && s.Body.Contains("\n  m_craftingStation:") && s.Body.Contains("\n  m_category:"));
            var (fields, requirementBlocks) = SplitRequirements(pieceSection.Body);

            if (FieldValue(fields, "m_enabled") != "1" && !Seasons.ContainsKey(guid!))
            {
                disabledCount++;
                Inform($"{"",2}{"Discarded",9}: {piece.Name} [Reason: piece disabled]", isVerbose: true);
                continue;
            }
            if (requirementBlocks.Count == 0)
            {
                noResourcesCount++;
                Inform($"{"",2}{"Discarded",9}: {piece.Name} [Reason: no resources]", isVerbose: true);
                continue;
            }

            var stationGuid = ReferencedGuid(FieldValue(fields, "m_craftingStation"));
            var station = ResolvePrefab(stationGuid);
            var requirementItems = requirementBlocks.Select(b => ResolvePrefab(ReferencedGuid(FieldValue(b, "m_resItem", depth: 2)))).ToList();
            if ((stationGuid != null && station == null) || requirementItems.Any(r => r == null))
            {
                unresolvedCount++;
                Inform($"{"",2}{"WARNING",9}: {piece.Name} [Reason: a prefab reference did not resolve under {PREFAB_DIRECTORY}]");
                continue;
            }

            Pieces.Add(new Piece(
                Name: piece.Name,
                NameToken: FieldValue(fields, "m_name"),
                Tool: tool,
                Category: int.Parse(FieldValue(fields, "m_category")),
                Station: station?.Name,
                StationToken: station?.Token,
                Season: Seasons.GetValueOrDefault(guid!),
                Resources: [.. requirementBlocks.Select((b, r) => new PieceResource(
                    Item: requirementItems[r]!.Name,
                    Amount: int.Parse(FieldValue(b, "m_amount", depth: 2))))]));
            Inform($"{"",2}{"Added",9}: {piece.Name} ({tool})", isVerbose: true);
        }
    }
    Inform($"{Pieces.Count} pieces extracted: {disabledCount} disabled, {noResourcesCount} without resources, {unresolvedCount} unresolved.");
}

// Norn names every recipe item through SharedItemData, so a name missing there is a
// broken extraction, not a recipe to ship.
void ValidateRecipeData()
{
    if (SwitchEnabled(SwitchNoRecipes))
    {
        return;
    }
    if (Recipes.Count < MIN_EXPECTED_RECIPE_COUNT)
    {
        Exit($"Only {Recipes.Count} recipes extracted, expected at least {MIN_EXPECTED_RECIPE_COUNT}.", 12);
    }

    var missing = MissingItemNames(Recipes.SelectMany(r => r.Resources.Select(req => req.Item).Prepend(r.Item)));
    if (missing.Count > 0)
    {
        Exit($"{missing.Count} recipe item name(s) missing from {ITEM_DATA_EXPORT_FILE}: {string.Join(", ", missing)}", 13);
    }
}

// Same reasoning as ValidateRecipeData.
void ValidatePieceData()
{
    if (SwitchEnabled(SwitchNoPieces))
    {
        return;
    }
    if (Pieces.Count < MIN_EXPECTED_PIECE_COUNT)
    {
        Exit($"Only {Pieces.Count} pieces extracted, expected at least {MIN_EXPECTED_PIECE_COUNT}.", 14);
    }

    var missing = MissingItemNames(Pieces.SelectMany(p => p.Resources.Select(r => r.Item)));
    if (missing.Count > 0)
    {
        Exit($"{missing.Count} piece resource name(s) missing from {ITEM_DATA_EXPORT_FILE}: {string.Join(", ", missing)}", 15);
    }
}

List<string> MissingItemNames(IEnumerable<string> names)
{
    var itemNames = SharedItemData.Select(i => i.ItemName).ToHashSet();
    return [.. names.Where(name => !itemNames.Contains(name)).Distinct()];
}

// A section's own top-level fields, and its m_resources list as one block per requirement.
// The list is bounded by the next top-level field, so it needn't be the last one.
(string Fields, List<string> Requirements) SplitRequirements(string section)
{
    var parts = section.Split("\n  m_resources:");
    var end = NextTopLevelField(parts[1]);
    var rest = end < 0 ? "" : parts[1][end..];
    return (parts[0] + rest, SplitList(section, "m_resources"));
}

// The entries of a top-level YAML list field, each re-indented so its fields sit at
// depth 2 (the "  - " marker otherwise leaves the first field at depth 0).
List<string> SplitList(string section, string key)
{
    var list = section.Split($"\n  {key}:")[1];
    var end = NextTopLevelField(list);
    return [.. (end < 0 ? list : list[..end]).Split("\n  - ").Skip(1).Select(entry => $"    {entry}")];
}

// Where the next top-level field starts (depth 1, not a list entry), or -1.
int NextTopLevelField(string yaml) => Regex.Match(yaml, @"\n  [^ -]") is { Success: true } m ? m.Index : -1;

// The season (Yule, Midsummer, ...) that switches on each seasonal recipe and piece, by
// guid — authoritive list is the Player prefab's own m_seasonalItemGroups. The game offers
// a disabled recipe or piece whenever the current season lists it
// (Player.GetAvailableRecipes, PieceTable), so Norn treats "listed by a season" the same
// as enabled, and records which season.
Dictionary<string, string> SeasonsByGuid()
{
    var playerFile = Path.Combine(PrefabDirectory, PLAYER_PREFAB_FILENAME);
    EnsureFile(playerFile);
    var player = GetUnityFileSections(File.ReadAllText(playerFile)).Values.First(s => s.Body.Contains("\n  m_seasonalItemGroups:"));
    var seasonGuids = SplitList(player.Body, "m_seasonalItemGroups").Select(ReferencedGuid).ToHashSet();

    var seasonFiles = Directory.GetFiles(RecipeDirectory, $"*{RECIPE_FILE_EXTENSION}{METAFILE_EXTENSION}", SearchOption.TopDirectoryOnly)
        .Where(meta => seasonGuids.Contains(MetaGuid(meta)))
        .Select(meta => File.ReadAllText(meta[..^METAFILE_EXTENSION.Length]))
        .ToList();
    if (seasonFiles.Count != seasonGuids.Count)
    {
        Inform($"WARNING: {seasonGuids.Count - seasonFiles.Count} season(s) listed by {PLAYER_PREFAB_FILENAME} not found under {RECIPE_DIRECTORY}.");
    }

    var seasons = new Dictionary<string, string>();
    foreach (var season in seasonFiles)
    {
        var name = FieldValue(season, "m_Name");
        foreach (var guid in SplitList(season, "Pieces").Concat(SplitList(season, "Recipes")).Select(ReferencedGuid).OfType<string>())
        {
            seasons[guid] = name;
        }
    }
    Inform($"{seasonFiles.Count} seasons switch on {seasons.Count} recipes and pieces.");
    return seasons;
}

// A referenced prefab's name (root GameObject) and its first localization-token m_name.
// Cached: recipes reference the same few hundred prefabs over and over.
Prefab? ResolvePrefab(string? guid)
{
    if (guid == null || !PrefabPathsByGuid.TryGetValue(guid, out var path))
    {
        return null;
    }
    if (!PrefabsByGuid.TryGetValue(guid, out var prefab))
    {
        var fileText = File.ReadAllText(path);
        var name = RootGameObjectName(fileText);
        var tokenLine = fileText.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith($"m_name: {LOCALIZATION_TOKEN_PREFIX}"));
        prefab = name == null ? null : new Prefab(name, tokenLine?["m_name: ".Length..]);
        PrefabsByGuid[guid] = prefab;
    }
    return prefab;
}

// The value of the first "key: value" line at the given depth in a block of Unity YAML.
// Depth matters: a component nests lists whose entries reuse field names (m_enabled).
string FieldValue(string yaml, string key, int depth = 1)
{
    var prefix = $"{new string(' ', 2 * depth)}{key}:";
    var line = yaml.Split('\n').FirstOrDefault(l => l.StartsWith(prefix));
    if (line == null)
    {
        Exit($"Field '{key}' not found.", 11);
    }
    return line![prefix.Length..].Trim();
}

// The guid of a "{fileID: ..., guid: ..., type: ...}" reference, or null for a null
// reference ("{fileID: 0}").
string? ReferencedGuid(string reference) =>
    reference.Contains("guid:") ? reference.Split("guid:")[1].Split(',')[0].Trim() : null;

string MetaGuid(string metaFile) => File.ReadAllText(metaFile).Split("guid: ")[1].Split('\n')[0].Trim();

// guid -> full prefab path, from every *.prefab.meta sibling in the directory.
Dictionary<string, string> BuildGuidIndex(string directory)
{
    var metaFiles = Directory.GetFiles(directory, $"*{PREFAB_FILE_EXTENSION}{METAFILE_EXTENSION}", SearchOption.TopDirectoryOnly);
    var index = new Dictionary<string, string>(metaFiles.Length);
    foreach (var metaFile in metaFiles)
    {
        index[MetaGuid(metaFile)] = metaFile[..^METAFILE_EXTENSION.Length]; // strip .meta, leaving the .prefab path
    }
    return index;
}

// The display text for an m_name, or null when nothing in it could be translated.
//
// Not always a single token: the Upgrader* items compose their name from three
// ("$item_upgrader_tier5 $item_upgrader_weapon $item_upgrader_name"), and the game
// resolves each one independently, so this does too. A token that has no translation is
// left in place rather than failing the whole name - a partly-resolved name still tells a
// reader more than the raw composite does.
string? ResolveTokenText(string tokenText)
{
    var parts = tokenText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var resolvedAny = false;

    for (var i = 0; i < parts.Length; i++)
    {
        if (!parts[i].StartsWith(LOCALIZATION_TOKEN_PREFIX))
        {
            continue;
        }
        if (LocalizationData.TryGetValue(parts[i][LOCALIZATION_TOKEN_PREFIX.Length..], out var text))
        {
            parts[i] = text;
            resolvedAny = true;
        }
    }

    return resolvedAny ? string.Join(' ', parts) : null;
}

// The prefab's root GameObject name, or null when the file has no parentless Transform to
// follow back. This is the name the game hashes (m_dropPrefab.name), so it is the only
// correct identity for an item - the filename is not, because the rip appends a suffix to
// whichever of a colliding pair it writes second (Amber.prefab / Amber_0.prefab).
string? RootGameObjectName(string fileText)
{
    var fileSections = GetUnityFileSections(fileText);

    // Root Transform = class "4", parentless
    var rootTransform = fileSections.Values.FirstOrDefault(b => b.ClassId == "4" && b.Body.Contains("m_Father: {fileID: 0}"));
    if (rootTransform.Body == null)
    {
        return null;
    }

    // ...follow its own m_GameObject back-reference
    var fileId = rootTransform.Body.Split("m_GameObject: {fileID: ")[1].Split('}')[0].Trim();
    if (!fileSections.ContainsKey(fileId))
    {
        return null;
    }

    // Look up that GameObject's own m_Name
    return fileSections[fileId].Body.Split('\n').First(l => l.Contains("m_Name:")).Split(':', 2)[1].Trim();
}

Dictionary<string, UnityFileSection> GetUnityFileSections(string fileText)
{
    // Get all sections
    var fileSections = fileText.Split("\n--- !u!")
        .Skip(1) // drops the %YAML/%TAG header
        .Select(b =>
        {
            var lines = b.Split('\n');
            var header = lines[0].Split([' ', '&'], StringSplitOptions.RemoveEmptyEntries);
            return new UnityFileSection { ClassId = header[0], FileId = header[1], Body = string.Join("\n", lines.Skip(1)) };
        }).ToDictionary(x => x.FileId, x => x);
    return fileSections;
}

// Save extracted item data to a CSV ; path by cmd line args, filename hard coded
void SaveItemData() => SaveDataAsCsvFile(SharedItemData, DestinationDirectory, ITEM_DATA_EXPORT_FILE);

// Save extracted localization data to a CSV file; path by cmd line args, filename hard coded
void SaveLocalizationData()
{
    if (SwitchEnabled(SwitchNoLocalization))
    {
        return;
    }
    var localizationData = LocalizationData.Select(kvp => new { Name = kvp.Key, Text = kvp.Value });
    SaveDataAsCsvFile(localizationData, DestinationDirectory, LOCALIZATION_DATA_EXPORT_FILE);
}

// Save extracted recipe/piece data to JSON files, each sorted by name.
void SaveRecipeData()
{
    if (!SwitchEnabled(SwitchNoRecipes))
    {
        SaveDataAsJsonFile(new { Recipes = Recipes.OrderBy(r => r.Name, StringComparer.Ordinal) }, Recipes.Count, "recipes", RECIPE_DATA_EXPORT_FILE);
    }
}

void SavePieceData()
{
    if (!SwitchEnabled(SwitchNoPieces))
    {
        SaveDataAsJsonFile(new { Pieces = Pieces.OrderBy(p => p.Name, StringComparer.Ordinal) }, Pieces.Count, "pieces", PIECE_DATA_EXPORT_FILE);
    }
}

// Save data to an indented JSON file with LF line endings regardless of host, so a
// patch-day diff shows only what changed.
void SaveDataAsJsonFile(object data, int count, string noun, string filename)
{
    try
    {
        var options = new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(DestinationDirectory, filename), JsonSerializer.Serialize(data, options) + "\n");
        Inform($"{count} {noun} saved to '{filename}'.");
    }
    catch (Exception ex)
    {
        Exit($"Failed to save file '{filename}'. {ex.Message}", 5);
    }
}

// Save a collection of data records to a CSV file
void SaveDataAsCsvFile<T>(IEnumerable<T> data, string path, string filename)
{
    try
    {
        var filePath = Path.Combine(path, filename);
        using (var writer = new StreamWriter(filePath))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(data);
        }
        Inform($"{data.Count()} items saved to '{filename}'.");
    }
    catch (Exception ex)
    {
        Exit($"Failed to save file '{filename}'. {ex.Message}", 5);
    }
}

struct UnityFileSection
{
    public string ClassId;
    public string FileId;
    public string Body;
}

struct ItemData
{
    // Properties, not fields: CsvHelper's default WriteRecords only maps properties,
    // and silently writes zero columns for a type that only has public fields.
    public string ItemName { get; set; }
    public bool IsTeleportable { get; set; }
    public bool UsesDurability { get; set; }
    public int MaxDurability { get; set; }
    public int DurabilityPerLevel { get; set; }
    public int MaxStack { get; set; }
    public string DisplayName { get; set; }
    public int MaxQuality { get; set; }
    public int ItemType { get; set; }
    public decimal Weight { get; set; }
    public decimal ScaleWeightByQuality { get; set; }
    public bool CanHaveCrafterTag { get; set; }
}

// Recipe data as written to RecipeData.json: game facts only, every reference a name.
record Recipe(string Name, string Item, int Amount, string? Station, string? StationToken, int MinStationLevel, bool Craftable, bool RequireOnlyOneIngredient, string? Season, IReadOnlyList<Requirement> Resources);
record Requirement(string Item, int Amount, int AmountPerLevel, bool Upgrader);

// Piece data as written to PieceData.json, same rules. A piece has no quality levels, so its
// requirements carry only the amount.
record Piece(string Name, string NameToken, string Tool, int Category, string? Station, string? StationToken, string? Season, IReadOnlyList<PieceResource> Resources);
record PieceResource(string Item, int Amount);

record Prefab(string Name, string? Token);
