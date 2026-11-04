using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

// Exercise the actual game localization code and compiled AFD strings without
// starting Unity. Run each legacy locale in a fresh process: old LocStr freezes.
if (args.Length < 3)
    throw new ArgumentException("Arguments: Managed directory, AFD DLL, AFD root, [legacy locale filename].");
string managed = Path.GetFullPath(args[0]), dll = Path.GetFullPath(args[1]), root = Path.GetFullPath(args[2]);
AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
{
    foreach (string dir in new[] { managed, Path.GetDirectoryName(dll)! })
    {
        string path = Path.Combine(dir, new AssemblyName(request.Name).Name + ".dll");
        if (File.Exists(path)) return Assembly.LoadFrom(path);
    }
    return null;
};
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
const string modId = "AutoForestryDesignations";
Assembly mafi = Assembly.LoadFrom(Path.Combine(managed, "Mafi.dll"));
Type lm = mafi.GetType("Mafi.Localization.LocalizationManager", true)!;
bool native = mafi.GetName().Version >= new Version(0, 8, 8, 0);
string dirPath = Path.Combine(root, "translations");
Dictionary<string, string> Read(string file) => JsonDocument.Parse(File.ReadAllText(file)).RootElement
    .EnumerateArray().ToDictionary(e => e[0].GetString()!, e => e[1].GetString()!);
var english = Read(Path.Combine(dirPath, "en.json"));
Check(english.Count == 93, "Expected the full AFD string set.");
foreach (string file in Directory.GetFiles(dirPath, "*.json").Where(f => !Path.GetFileName(f).StartsWith('.')))
{
    var entries = Read(file);
    Check(entries.Keys.ToHashSet().SetEquals(english.Keys), "Incomplete bundle: " + file);
    foreach (var entry in entries)
    {
        Check(Regex.IsMatch(entry.Key, @"\AAutoForestryDesignations\.[a-zA-Z_][a-zA-Z0-9_]{2,62}[a-zA-Z0-9]\z"), "Invalid ID: " + entry.Key);
        // Compare complete format items, including alignment and format suffixes.
        string[] Tokens(string value) => Regex.Matches(value, @"(?<!\{)\{\d+(?:,[^}:]+)?(?::[^}]+)?\}(?!\})")
            .Select(m => m.Value).Order().ToArray();
        Check(Tokens(entry.Value).SequenceEqual(Tokens(english[entry.Key])), "Placeholder mismatch: " + file + ": " + entry.Key);
    }
}

object Lang(string filename)
{
    object languages = lm.GetField("LanguagesAvailable")!.GetValue(null)!;
    return ((IEnumerable)languages.GetType().GetMethod("AsEnumerable")!.Invoke(languages, null)!).Cast<object>().Single(l => (string)l.GetType().GetField("FileName")!.GetValue(l)! == filename);
}
void Load(string filename)
{
    MethodInfo method = lm.GetMethod("TryLoadTranslationsFrom")!;
    object?[] parameters = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
    parameters[0] = "[]"; parameters[1] = ""; parameters[2] = Lang(filename);
    Check((bool)method.Invoke(null, parameters)!, "Cannot load language: " + filename);
}
// Select the initial language before fields exist, reproducing the real old-game order.
string first = native ? "en.json" : args[3];
Load(first);
Assembly afd = Assembly.LoadFrom(dll);
Type strings = afd.GetType("AutoForestryDesignations.AfdLocalization", true)!;
var fields = strings.GetFields(BindingFlags.Public | BindingFlags.Static);
var samples = fields.Select(f => (field: f, value: f.GetValue(null)!)).ToArray();
Check(samples.Length == english.Count, "Missing localization fields.");
string Id(object value) => (string)(value.GetType().GetProperty("Id")?.GetValue(value) ?? value.GetType().GetField("Id")!.GetValue(value))!;
string Text(object value) => (string)(value.GetType().GetProperty("TranslatedString")?.GetValue(value) ?? value.GetType().GetField("TranslatedString")!.GetValue(value))!;
foreach (var sample in samples)
    Check(Text(sample.value) == english[Id(sample.value)], $"Expected English before translations merge: {sample.field.Name}: actual={Text(sample.value)}, expected={english[Id(sample.value)]}");

if (native)
{
    Check(afd.GetType("CoI.AutoHelpers.Localization.CoILocalizationRuntimeAdapter") == null, "Native payload still includes legacy reflection adapter.");
    // Work only on temporary translation files for fallback and cache tests.
    string fixture = Path.Combine(Path.GetTempPath(), "afd-localization-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(fixture);
    try
    {
        foreach (string file in Directory.GetFiles(dirPath, "*.json")) File.Copy(file, Path.Combine(fixture, Path.GetFileName(file)));
        lm.GetMethod("RegisterModTranslations")!.Invoke(null, new object[] { modId, fixture });
        foreach (string filename in new[] { "sv.json", "de.json", "es.json", "it.json", "pt_BR.json", "ru.json", "zh_Hans.json" })
        {
            Load(filename);
            var expected = Read(Path.Combine(fixture, filename));
            // Check copies taken before the merge as well as freshly accessed static fields.
            foreach (var sample in samples)
            {
                Check(Text(sample.value) == expected[Id(sample.value)], "Early LocStr stayed stale: " + filename + ": " + sample.field.Name);
                Check(Text(sample.field.GetValue(null)!) == expected[Id(sample.value)], "Static field mismatch.");
            }
        }
        Load("sv.json");
        object sampleValue = samples.First(s => s.field.Name == "ForestryDesignationsTitle").value;
        Type preparedType = mafi.GetType("Mafi.Localization.LocStrPrepared", true)!;
        object prepared = preparedType.GetMethods().Single(m => m.Name == "Of" && m.GetParameters().Length == 1).Invoke(null, new[] { sampleValue })!;
        object frozen = preparedType.GetMethod("Format")!.Invoke(prepared, null)!;
        string frozenText = (string)frozen.GetType().GetField("Value")!.GetValue(frozen)!;
        Load("de.json");
        object refreshed = preparedType.GetMethod("Format")!.Invoke(prepared, null)!;
        Check(frozenText != Text(sampleValue), "Need distinct languages for captured-text regression.");
        Check((string)frozen.GetType().GetField("Value")!.GetValue(frozen)! == frozenText, "Formatted text unexpectedly changed.");
        Check((string)refreshed.GetType().GetField("Value")!.GetValue(refreshed)! == Text(sampleValue), "Deferred formatting is stale.");
        var swedish = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "sv.json"))).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        swedish.RemoveAll(e => e[0].GetString() == Id(sampleValue));
        File.WriteAllText(Path.Combine(fixture, "sv.json"), JsonSerializer.Serialize(swedish));
        Load("sv.json");
        Check(Text(sampleValue) == english[Id(sampleValue)], "Missing key must fall back to English.");
        foreach (string method in new[] { "GetLegacyStringIdsOf", "GetMalformedStringIdsOf" })
        {
            var result = (IEnumerable)lm.GetMethod(method)!.Invoke(null, new object[] { modId })!;
            Check(!result.Cast<object>().Any(), method + " must be empty.");
        }
        lm.GetMethod("ClearLoadedTranslations")!.Invoke(null, null);
        foreach (var sample in samples) Check(Text(sample.value) == english[Id(sample.value)], "English switch failed.");
    }
    finally { Directory.Delete(fixture, true); }
}
else
{
    Type runtime = afd.GetType("CoI.AutoHelpers.Localization.ModTranslations", true)!;
    Type optionsType = afd.GetType("CoI.AutoHelpers.Localization.ModTranslationsApplyOptions", true)!;
    ConstructorInfo ctor = optionsType.GetConstructors().Single();
    object policy = ctor.GetParameters()[3].DefaultValue!;
    string fixture = Path.Combine(Path.GetTempPath(), "afd-legacy-localization-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(fixture);
    try
    {
        foreach (string file in Directory.GetFiles(dirPath, "*.json")) File.Copy(file, Path.Combine(fixture, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(fixture, "source.json"), "{\"metadata\":{}}");
        File.WriteAllText(Path.Combine(fixture, "template.json"), "[]");
        object options = ctor.Invoke(new object[] { fixture, afd, new[] { modId + "." }, policy });
        object outcome = runtime.GetMethod("Apply")!.Invoke(Activator.CreateInstance(runtime), new[] { options })!;
        Check(!(bool)outcome.GetType().GetProperty("HasErrors")!.GetValue(outcome)!, "Native authoring exports must not be parsed as locale bundles.");
        Check((int)outcome.GetType().GetProperty("ReboundFieldCount")!.GetValue(outcome)! == samples.Length, "Legacy rebind did not cover all fields.");
        Check((int)outcome.GetType().GetProperty("FailedFieldCount")!.GetValue(outcome)! == 0, "Legacy field writes failed.");
        var expected = Read(Path.Combine(dirPath, first));
        foreach (var sample in samples)
            Check(Text(sample.field.GetValue(null)!) == expected[Id(sample.value)], "Legacy translation mismatch: " + first + ": " + sample.field.Name);
    }
    finally { Directory.Delete(fixture, true); }
}
Console.WriteLine($"PASS: {checks} localization checks; game {mafi.GetName().Version}; {(native ? "native merge/language changes/fallback/captured text" : "legacy " + first)}.");
