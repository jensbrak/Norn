//
// Vade - a simple tool to extract data from Valheim to be used with Norn.
// See readme for context.
//

using CsvHelper;
using System.Globalization;

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

// Holds ObjectDB, the authority for BOTH lists that matter here: m_items (every prefab
// the game registers as an inventory item) and m_recipes. Was recipe-only until items
// moved onto it too, so the name lost its "RECIPE" qualifier.
const string ACTIVE_AUTHORITY_FILENAME = "_GameMain.prefab";

// A player-facing item's m_name is a localization token ("$item_amber"); an internal one
// is a plain literal ("Club" on GoblinClub, "Swingattack" on Abomination_attack1). That
// prefix is what separates them - NOT whether the token happens to resolve, which is a
// different question with a different answer whenever a localization file is missing.
const string LOCALIZATION_TOKEN_PREFIX = "$";
const string ITEM_DATA_EXPORT_FILE = "SharedItemData.csv";
const string LOCALIZATION_DATA_EXPORT_FILE = "LocalizationData.csv";
const string FALLBACK_DIRECTORY = @".\";
string[] LOCALIZATION_FILES = ["localization.txt", "localization_extra.txt", "localization_celebrationupdate.txt", "localization_deepnorth.txt"];

// Command line argument switch definitions
string[] SwitchHelp = ["/?", "/h", "--help"];
string[] SwitchVerbose = ["/v", "--verbose"];
// Localization is written by DEFAULT and this switch turns it OFF. It was opt-in
// while this tool served several consumers; Norn needs both CSVs, so producing only
// one of them is now the unusual case and the one that has to be asked for.
string[] SwitchNoLocalization = ["/n", "--no-localization"];

// Sanity check constants. A silent collapse is the failure mode worth guarding: an
// extraction that returns far too little still writes a well-formed CSV, and every
// consumer downstream then quietly degrades instead of failing.
int MIN_EXPECTED_RECIPE_COUNT = 300;
int MIN_EXPECTED_ITEM_COUNT = 900;

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

// Data extration steps
Inform("Extracting item data...");
ExtractLocalizationData();
ExtractItemData();
ApplyCrafterTagData();
SaveItemData();
SaveLocalizationData();
Inform("Item data extracted successfully");

// -----------------------------------------------------------------------------------------------
// Functions
// -----------------------------------------------------------------------------------------------

// Switch enabled or not?
bool SwitchEnabled(string[] sw) => args.Length > 2 && args[^2..].Any(a => sw.Any(s => a.ToLower().Equals(s)));

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
        $"{AppDomain.CurrentDomain.FriendlyName} source destination [{SwitchVerbose[0]}] [{SwitchNoLocalization[0]}]\n\n" +
        $"  {"source",-12} Specifies the source directory, ie the assets root directory to extract data from.\n" +
        $"  {"destination",-12} Specifies the destination directory, ie the directory to write data to.\n" +
        $"  {SwitchVerbose[0],-12} Verbose mode: print additional info\n" +
        $"  {SwitchNoLocalization[0],-12} Skip the localization data file, which is written by default\n" +
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
// The previous version answered all three with question 3 alone: scan every prefab on
// disk, keep whatever had item data AND a resolvable translation. That conflates "is an
// item" with "can be named", and the two come apart in both directions - a missing
// localization file silently dropped an entire biome's content, while unregistered
// prefabs that happened to carry item data were picked up.
void ExtractItemData()
{
    var itemGuids = AuthoritiveItemGuids();
    var prefabPathsByGuid = BuildGuidIndex(PrefabDirectory);

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
        if (!prefabPathsByGuid.TryGetValue(itemGuids[i], out var filename))
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

        // A token with no translation is still a real item. Fall back to the raw token so
        // it stays in the catalog under a name that is at least distinct and searchable -
        // dropping it here is what previously cost TrophyDeerWhite, IceShoes and IceSkates,
        // all three of which the game itself ships without a translation.
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

        // Extra sanity check for informational purposes only, replaces old `_0` suffix removal
        if (rootName != itemIdCandidate)
        {
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: Root GameObject name '{rootName}' does not match prefab filename '{itemIdCandidate}'", isVerbose: false);
        }

        // We have the name. This is the name the game itself hashes (m_dropPrefab.name),
        // so it must come from the GameObject and never from the filename - the rip
        // suffixes colliding filenames (Amber.prefab / Amber_0.prefab) and only one of
        // the pair is the registered item.
        var itemName = rootName;

        // The old cave_/gobvill_ name-prefix filter is gone with this rewrite. It existed
        // to suppress location-piece variants a filesystem scan picked up; ObjectDB
        // registers none of them, so the filter now has nothing to match and could only
        // ever exclude a real item by accident.

        // We finally have a valid item data to add. Anonymous type objects will do as records
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

// CollectPrefabFiles is gone with this rewrite. Scanning the prefab directory was how
// items used to be found, and it asked the wrong question: the directory holds ~9700
// prefabs of which ObjectDB registers ~1500 as items, so the scan needed a heuristic
// filter to cut the rest back down. BuildGuidIndex still enumerates the same directory,
// but only to resolve guids the registry already vouched for.
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

List<string> AuthoritiveRecipeUids()
{
    // Consult authority recipe file to get a list of all recipes that are active (will give us UIDS for recipe files)
    var fileText = File.ReadAllText(ActiveAuthorityFile);
    var fileSections = GetUnityFileSections(fileText);
    var recipeGameObject = fileSections.Values.FirstOrDefault(b => b.ClassId == "114" && b.Body.Contains("m_recipes"));
    var recipeLines = recipeGameObject.Body.Split("m_recipes:")[1].Split("m_terrainOps")[0].Split('\n').Where(line => line.Contains("guid:"));

    // Quick sanity check
    if (recipeLines.Count() < MIN_EXPECTED_RECIPE_COUNT)
    {
        Inform($"WARNING: Only {recipeLines.Count()} recipes found in authority file '{ActiveAuthorityFile}', No CanHaveCrafterTag data extracted.");
        return [];
    }

    // Exctract recipe file UID's. Early fail on bad recipe UID data
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

// Determines which extracted items can legitimately carry a crafter tag, and flips
// ItemData.CanHaveCrafterTag for each. An item qualifies iff it's the m_item output
// of a recipe that is both (a) confirmed present in the authoritive recipe list and
// (b) actually enabled (m_enabled: 1, lowercase — the Recipe script's own field, not
// the generic Unity MonoBehaviour header's m_Enabled, which is unrelated boilerplate
// and always 1 for these assets; 51 of the 365 authoritive recipes fail this check).
void ApplyCrafterTagData()
{
    var authoritiveRecipeUids = AuthoritiveRecipeUids();
    if (authoritiveRecipeUids.Count == 0)
    {
        // AuthoritiveRecipeUids already warned; nothing to apply.
        return;
    }

    var recipesIn = CollectRecipeFiles();

    // Quick sanity check
    if (recipesIn.Length < authoritiveRecipeUids.Count)
    {
        Exit($"Too few recipe files ({recipesIn.Length}) found in: '{RecipeDirectory}', expected at least {authoritiveRecipeUids.Count}", 8);
    }

    // Build new list of recipes, discarding those that are not in the authoritive list.
    // Remove() doubles as the membership check and as bookkeeping: whatever's left in
    // authoritiveRecipeUids afterward is an authoritive guid that never matched a file.
    var recipesOut = new List<string>(recipesIn.Length);
    for (var i = 0; i < recipesIn.Length; i++)
    {
        var recipe = recipesIn[i];
        var metafile = $"{recipe}{METAFILE_EXTENSION}";
        var guid = File.ReadAllText(metafile).Split("guid: ")[1].Split('\n')[0].Trim();
        if (!authoritiveRecipeUids.Remove(guid))
        {
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {Path.GetFileNameWithoutExtension(recipe)} ({guid}) [Reason: not in authoritive list]", isVerbose: true);
        }
        else
        {
            recipesOut.Add(recipe);
        }
    }

    if (authoritiveRecipeUids.Count > 0)
    {
        Inform($"WARNING: {authoritiveRecipeUids.Count} authoritive recipe guid(s) never matched a file on disk.");
    }

    var taggableItemNames = ResolveTaggableItemNames(recipesOut);

    // Flip CanHaveCrafterTag for every extracted item that's a real, enabled recipe's
    // output. Index-based read-modify-write since ItemData is a struct: SharedItemData[i]
    // returns a copy, so mutating it in place and never writing it back would silently
    // do nothing.
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
    Inform($"{taggedCount} of {SharedItemData.Count} items flagged as CanHaveCrafterTag ({taggableItemNames.Count} distinct taggable item names resolved).");
}

// For each already authoritive-filtered, on-disk recipe file: skip if disabled or if
// its m_item is the null reference ({fileID: 0} — no guid at all, e.g. Recipe_Adze/
// Recipe_Chisel), otherwise resolve m_item's guid against the item-side guid index
// and collect the resulting item's ROOT GAMEOBJECT NAME. Returns the set of item names any
// enabled, authoritive recipe actually produces.
//
// The name has to come from inside the prefab, not from its filename, or it will not match
// what ExtractItemData recorded: a recipe producing Amber resolves to Amber_0.prefab, whose
// item name is Amber. Matching on the filename silently failed to tag 15 items.
HashSet<string> ResolveTaggableItemNames(List<string> recipeFiles)
{
    var itemGuidIndex = BuildGuidIndex(PrefabDirectory);
    var taggableNames = new HashSet<string>();
    var disabledCount = 0;
    var noItemCount = 0;
    var unresolvedCount = 0;

    for (var i = 0; i < recipeFiles.Count; i++)
    {
        var recipeFile = recipeFiles[i];
        var recipeName = Path.GetFileNameWithoutExtension(recipeFile);
        var recipeText = File.ReadAllText(recipeFile);

        // Real recipe-enabled flag, lowercase — see this function's own doc comment.
        if (!recipeText.Contains("m_enabled: 1"))
        {
            disabledCount++;
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {recipeName} [Reason: recipe disabled]", isVerbose: true);
            continue;
        }

        // Bounded to the {fileID: ...} value itself, not an open-ended split on "guid:" —
        // a null reference ("m_item: {fileID: 0}") has no guid at all, and scanning past
        // it would silently pick up some unrelated later field's guid instead.
        var itemRef = recipeText.Split("m_item: {fileID: ")[1].Split('}')[0];
        if (!itemRef.Contains("guid:"))
        {
            noItemCount++;
            Inform($"{"",2}[{i + 1,4}] {"Discarded",9}: {recipeName} [Reason: m_item is a null reference]", isVerbose: true);
            continue;
        }

        var itemGuid = itemRef.Split("guid:")[1].Split(',')[0].Trim();
        if (!itemGuidIndex.TryGetValue(itemGuid, out var itemPath))
        {
            unresolvedCount++;
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: {recipeName} [Reason: item guid {itemGuid} not found under {PREFAB_DIRECTORY}]");
            continue;
        }

        var itemName = RootGameObjectName(File.ReadAllText(itemPath));
        if (itemName == null)
        {
            unresolvedCount++;
            Inform($"{"",2}[{i + 1,4}] {"WARNING",9}: {recipeName} [Reason: item prefab {Path.GetFileName(itemPath)} has no root GameObject]");
            continue;
        }

        taggableNames.Add(itemName);
    }

    Inform($"{recipeFiles.Count} enabled/authoritive recipes processed: {disabledCount} disabled, {noItemCount} without an item, {unresolvedCount} unresolved.", isVerbose: true);
    return taggableNames;
}

// guid -> full prefab path, built from every *.prefab.meta sibling in the given
// directory. Only needed on the item side: recipe files are already resolved directly by
// filename (CollectRecipeFiles), so there's nothing to index there.
// Paths rather than bare names because both callers need to open the file - the item pass
// to read its data, the recipe pass only to name it, which it derives from the path.
Dictionary<string, string> BuildGuidIndex(string directory)
{
    var metaFiles = Directory.GetFiles(directory, $"*{PREFAB_FILE_EXTENSION}{METAFILE_EXTENSION}", SearchOption.TopDirectoryOnly);
    var index = new Dictionary<string, string>(metaFiles.Length);
    foreach (var metaFile in metaFiles)
    {
        var guid = File.ReadAllText(metaFile).Split("guid: ")[1].Split('\n')[0].Trim();
        index[guid] = metaFile[..^METAFILE_EXTENSION.Length]; // strip .meta, leaving the .prefab path
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

// Save a collection of data records (anonymous types or, now, ItemData) to a CSV file
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