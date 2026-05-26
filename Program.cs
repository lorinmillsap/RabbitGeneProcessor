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