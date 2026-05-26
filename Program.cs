using RabbitGeneProcessor.Core;

GeneticParser.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "LociDefinitions.json"));
VarietyService.Initialize(
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Varieties.json"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Modifiers.json"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Breeds.json"));

Console.WriteLine("Rabbit Gene Processor Initialized.");
Console.WriteLine($"Loaded {GeneticParser.Definitions.Count} loci definitions.");
Console.WriteLine($"Loaded {VarietyService.Breeds.Count} breed definitions.");
Console.WriteLine($"Loaded {VarietyService.Varieties.Count} variety definitions.");
Console.WriteLine($"Loaded {VarietyService.Modifiers.Count} modifier definitions.");

// Example using a breed and variety
string breedName = "Rex";
string varietyName = "Chestnut";
string fullGenotypeString = VarietyService.GetFullGenotypeString(breedName, varietyName);
var genotype = RabbitGenotype.Parse(fullGenotypeString);
Console.WriteLine($"Breed: {breedName}, Variety: {varietyName} -> Genotype: {genotype}");

string breedName2 = "Lionhead";
string varietyName2 = "Black";
string fullGenotypeString2 = VarietyService.GetFullGenotypeString(breedName2, varietyName2, new List<string> { "Double Mane" });
var genotype2 = RabbitGenotype.Parse(fullGenotypeString2);
Console.WriteLine($"Breed: {breedName2}, Variety: {varietyName2}, Modifiers: Double Mane -> Genotype: {genotype2}");

// Demonstrate English Angora Blue Otter
string breedName3 = "English Angora";
string varietyName3 = "Blue Otter";
string fullGenotypeString3 = VarietyService.GetFullGenotypeString(breedName3, varietyName3);
var genotype3 = RabbitGenotype.Parse(fullGenotypeString3);
Console.WriteLine($"Breed: {breedName3}, Variety: {varietyName3} -> Genotype: {genotype3}");

// Demonstrate parsing a full description
string description = "Broken VM Chestnut Rex";
var (parsedBreed, parsedVariety, parsedModifiers) = VarietyService.ParseDescription(description);
string parsedGenotypeString = VarietyService.GetFullGenotypeString(parsedBreed.Name, parsedVariety.Name, parsedModifiers.Select(m => m.Name).ToList());
var parsedGenotype = RabbitGenotype.Parse(parsedGenotypeString);
Console.WriteLine($"\nDescription: {description}");
Console.WriteLine($"Parsed - Breed: {parsedBreed.Name}, Variety: {parsedVariety.Name}, Modifiers: {string.Join(", ", parsedModifiers.Select(m => m.Name))}");
Console.WriteLine($"Genotype: {parsedGenotype}");

// Demonstrate multiple modifiers and override order
string description2 = "Self Chin Martenized Black Mini Rex";
var (pb2, pv2, pm2) = VarietyService.ParseDescription(description2);
string pg2String = VarietyService.GetFullGenotypeString(pb2.Name, pv2.Name, pm2.Select(m => m.Name).ToList());
var pg2 = RabbitGenotype.Parse(pg2String);
Console.WriteLine($"\nDescription: {description2}");
Console.WriteLine($"Parsed - Breed: {pb2.Name}, Variety: {pv2.Name}, Modifiers: {string.Join(", ", pm2.Select(m => m.Name))}");
Console.WriteLine($"Genotype: {pg2}");

// Demonstrate converting genotype back to variety description
Console.WriteLine("\n--- Genotype to Description ---");
string g1Str = "aa,bb,C_,dd,E_,En_,Vv,rr"; // Broken VM Lilac Rex
var g1 = RabbitGenotype.Parse(g1Str);
string desc1 = VarietyService.IdentifyDescription(g1, "Rex");
Console.WriteLine($"Genotype: {g1} -> Description (Breed: Rex): {desc1}");

string g2Str = "A_,B_,C_,D_,E_,ll"; // English Angora Chestnut
var g2 = RabbitGenotype.Parse(g2Str);
string desc2 = VarietyService.IdentifyDescription(g2, "English Angora");
Console.WriteLine($"Genotype: {g2} -> Description (Breed: English Angora): {desc2}");

string g3Str = "aa,B_,C_,D_,E_,MM"; // Double Mane Black Lionhead
var g3 = RabbitGenotype.Parse(g3Str);
string desc3 = VarietyService.IdentifyDescription(g3, "Lionhead");
Console.WriteLine($"Genotype: {g3} -> Description (Breed: Lionhead): {desc3}");

// Demonstrate identification without breed
string g4Str = "A_,B_,C_,dd,E_,En_"; // Broken Opal
var g4 = RabbitGenotype.Parse(g4Str);
string desc4 = VarietyService.IdentifyDescription(g4);
Console.WriteLine($"Genotype: {g4} -> Description (No Breed): {desc4}");

// Demonstrate Ruby Eyed White
string g5Str = "cc";
var g5 = RabbitGenotype.Parse(g5Str);
string desc5 = VarietyService.IdentifyDescription(g5);
Console.WriteLine($"Genotype: {g5} -> Description (No Breed): {desc5}");

// Demonstrate Blue Eyed White
string g6Str = "vv";
var g6 = RabbitGenotype.Parse(g6Str);
string desc6 = VarietyService.IdentifyDescription(g6);
Console.WriteLine($"Genotype: {g6} -> Description (No Breed): {desc6}");

// Demonstrate Pointed White
string g7Str = "aa,bb,ch_,dd,E_"; // Lilac Pointed White
var g7 = RabbitGenotype.Parse(g7Str);
string desc7 = VarietyService.IdentifyDescription(g7);
Console.WriteLine($"Genotype: {g7} -> Description (No Breed): {desc7}");