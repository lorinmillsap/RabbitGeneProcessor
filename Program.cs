using RabbitGeneProcessor.Core;

GeneticParser.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "LociDefinitions.json"));
VarietyService.Initialize(
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Varieties.json"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Modifiers.json"));

Console.WriteLine("Rabbit Gene Processor Initialized.");
Console.WriteLine($"Loaded {GeneticParser.Definitions.Count} loci definitions.");
Console.WriteLine($"Loaded {VarietyService.Varieties.Count} variety definitions.");
Console.WriteLine($"Loaded {VarietyService.Modifiers.Count} modifier definitions.");

foreach (var variety in VarietyService.Varieties)
{
    // Demonstrate standard variety (implies enen)
    string standardGenotypeString = VarietyService.GetFullGenotypeString(variety.Name);
    var standardGenotype = RabbitGenotype.Parse(standardGenotypeString);
    Console.WriteLine($"Variety: {variety.Name} -> Genotype: {standardGenotype}");

    // Demonstrate Broken variety (applies En_)
    string brokenGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Broken" });
    var brokenGenotype = RabbitGenotype.Parse(brokenGenotypeString);
    Console.WriteLine($"Variety: Broken {variety.Name} -> Genotype: {brokenGenotype}");

    // Demonstrate Vienna Marked (modifier with blank exclusion)
    string vmGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Vienna Marked" });
    var vmGenotype = RabbitGenotype.Parse(vmGenotypeString);
    Console.WriteLine($"Variety: Vienna Marked {variety.Name} -> Genotype: {vmGenotype}");

    // Demonstrate using alternate name (VM)
    string vmAltGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "VM" });
    var vmAltGenotype = RabbitGenotype.Parse(vmAltGenotypeString);
    Console.WriteLine($"Variety: VM {variety.Name} -> Genotype: {vmAltGenotype}");

    // Demonstrate Vienna Carrier (VC)
    string vcGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "VC" });
    var vcGenotype = RabbitGenotype.Parse(vcGenotypeString);
    Console.WriteLine($"Variety: VC {variety.Name} -> Genotype: {vcGenotype}");

    // Demonstrate High Roufus
    string hrGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "High Roufus" });
    var hrGenotype = RabbitGenotype.Parse(hrGenotypeString);
    Console.WriteLine($"Variety: High Roufus {variety.Name} -> Genotype: {hrGenotype}");

    // Demonstrate Steeled
    string steeledGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Steeled" });
    var steeledGenotype = RabbitGenotype.Parse(steeledGenotypeString);
    Console.WriteLine($"Variety: Steeled {variety.Name} -> Genotype: {steeledGenotype}");

    // Demonstrate Harlequinized
    string harlequinGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Harlequinized" });
    var harlequinGenotype = RabbitGenotype.Parse(harlequinGenotypeString);
    Console.WriteLine($"Variety: Harlequinized {variety.Name} -> Genotype: {harlequinGenotype}");

    // Demonstrate Martenized
    string martenGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Martenized" });
    var martenGenotype = RabbitGenotype.Parse(martenGenotypeString);
    Console.WriteLine($"Variety: Martenized {variety.Name} -> Genotype: {martenGenotype}");

    // Demonstrate Self Chin
    string selfChinGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Self Chin" });
    var selfChinGenotype = RabbitGenotype.Parse(selfChinGenotypeString);
    Console.WriteLine($"Variety: Self Chin {variety.Name} -> Genotype: {selfChinGenotype}");

    // Demonstrate Non-Vienna
    string nonViennaGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Non-Vienna" });
    var nonViennaGenotype = RabbitGenotype.Parse(nonViennaGenotypeString);
    Console.WriteLine($"Variety: Non-Vienna {variety.Name} -> Genotype: {nonViennaGenotype}");

    // Demonstrate Breed Modifiers
    string rexGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Rex" });
    Console.WriteLine($"Variety: Rex {variety.Name} -> Genotype: {RabbitGenotype.Parse(rexGenotypeString)}");

    string angoraGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "English Angora" });
    Console.WriteLine($"Variety: English Angora {variety.Name} -> Genotype: {RabbitGenotype.Parse(angoraGenotypeString)}");

    string lionheadGenotypeString = VarietyService.GetFullGenotypeString(variety.Name, new List<string> { "Lionhead", "Double Mane" });
    Console.WriteLine($"Variety: Double Mane Lionhead {variety.Name} -> Genotype: {RabbitGenotype.Parse(lionheadGenotypeString)}");
}