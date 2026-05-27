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

// Demonstrate new CalculateGenotypeFromDescription method
Console.WriteLine("\n--- CalculateGenotypeFromDescription ---");
string descWithBreed = "Broken VM Chestnut Rex";
string genotypeFromDesc1 = VarietyService.CalculateGenotypeFromDescription(descWithBreed);
Console.WriteLine($"Description: {descWithBreed} -> Genotype: {genotypeFromDesc1}");

string descWithoutBreed = "Broken VM Chestnut";
string genotypeFromDesc2 = VarietyService.CalculateGenotypeFromDescription(descWithoutBreed);
Console.WriteLine($"Description: {descWithoutBreed} -> Genotype: {genotypeFromDesc2}");

string complexDesc = "Self Chin Martenized Black Mini Rex";
string genotypeFromDesc3 = VarietyService.CalculateGenotypeFromDescription(complexDesc);
Console.WriteLine($"Description: {complexDesc} -> Genotype: {genotypeFromDesc3}");

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

// Demonstrate Chinchilla
string g8Str = "A_,B_,cchd_,D_,E_";
var g8 = RabbitGenotype.Parse(g8Str);
string desc8 = VarietyService.IdentifyDescription(g8);
Console.WriteLine($"Genotype: {g8} -> Description (No Breed): {desc8}");

string g9Str = "A_,B_,cchd_,dd,E_"; // Squirrel
var g9 = RabbitGenotype.Parse(g9Str);
string desc9 = VarietyService.IdentifyDescription(g9);
Console.WriteLine($"Genotype: {g9} -> Description (No Breed): {desc9}");

// Demonstrate Steel
string g10Str = "A_,B_,C_,D_,EsE"; // Black Steel
var g10 = RabbitGenotype.Parse(g10Str);
string desc10 = VarietyService.IdentifyDescription(g10);
Console.WriteLine($"Genotype: {g10} -> Description (No Breed): {desc10}");

string g11Str = "A_,bb,C_,dd,EsE"; // Lilac Steel
var g11 = RabbitGenotype.Parse(g11Str);
string desc11 = VarietyService.IdentifyDescription(g11);
Console.WriteLine($"Genotype: {g11} -> Description (No Breed): {desc11}");

// Demonstrate Supersteel
string g12Str = "A_,B_,C_,D_,EsEs"; // Supersteel Chestnut
var g12 = RabbitGenotype.Parse(g12Str);
string desc12 = VarietyService.IdentifyDescription(g12);
Console.WriteLine($"Genotype: {g12} -> Description (No Breed): {desc12}");

// Demonstrate Gold Tipped and Silver Tipped Steel
string g13Str = "A_,B_,C_,D_,EsE"; // Gold Tipped Steel
var g13 = RabbitGenotype.Parse(g13Str);
string desc13 = VarietyService.IdentifyDescription(g13);
Console.WriteLine($"Genotype: {g13} -> Description (No Breed): {desc13}");

string g14Str = "A_,B_,cchd_,D_,EsE"; // Silver Tipped Steel
var g14 = RabbitGenotype.Parse(g14Str);
string desc14 = VarietyService.IdentifyDescription(g14);
Console.WriteLine($"Genotype: {g14} -> Description (No Breed): {desc14}");

// Demonstrate Gold Tipped Steel variety
string description3 = "Gold Tipped Black Steel";
string pg3String = VarietyService.CalculateGenotypeFromDescription(description3);
var pg3 = RabbitGenotype.Parse(pg3String);
Console.WriteLine($"\nDescription: {description3}");
Console.WriteLine($"Genotype: {pg3}");

// Demonstrate Postfix Modifier: Tri
string description4 = "Black Tri";
string pg4String = VarietyService.CalculateGenotypeFromDescription(description4);
var pg4 = RabbitGenotype.Parse(pg4String);
Console.WriteLine($"\nDescription: {description4}");
Console.WriteLine($"Genotype: {pg4}");

string g15Str = "aa,B_,C_,D_,ej_,En_"; // Black Tri
var g15 = RabbitGenotype.Parse(g15Str);
string desc15 = VarietyService.IdentifyDescription(g15);
Console.WriteLine($"Genotype: {g15} -> Description (No Breed): {desc15}");

// Demonstrate Magpie
string g16Str = "A_,B_,cchd_,D_,ej_"; // Black Magpie
var g16 = RabbitGenotype.Parse(g16Str);
string desc16 = VarietyService.IdentifyDescription(g16);
Console.WriteLine($"Genotype: {g16} -> Description (No Breed): {desc16}");

string g17Str = "A_,bb,cchd_,dd,ej_"; // Lilac Magpie
var g17 = RabbitGenotype.Parse(g17Str);
string desc17 = VarietyService.IdentifyDescription(g17);
Console.WriteLine($"Genotype: {g17} -> Description (No Breed): {desc17}");

// Demonstrate Tortoise
string g18Str = "aa,B_,C_,D_,ee"; // Black Tortoise
var g18 = RabbitGenotype.Parse(g18Str);
string desc18 = VarietyService.IdentifyDescription(g18);
Console.WriteLine($"Genotype: {g18} -> Description (No Breed): {desc18}");

string g19Str = "aa,bb,C_,dd,ee"; // Lilac Tortoise
var g19 = RabbitGenotype.Parse(g19Str);
string desc19 = VarietyService.IdentifyDescription(g19);
Console.WriteLine($"Genotype: {g19} -> Description (No Breed): {desc19}");

// Demonstrate Orange
string g20Str = "A_,bb,C_,D_,ee";
var g20 = RabbitGenotype.Parse(g20Str);
string desc20 = VarietyService.IdentifyDescription(g20);
Console.WriteLine($"Genotype: {g20} -> Description (No Breed): {desc20}");

// Demonstrate Postfix Modifier: Chin
string description5 = "Black Chin";
string pg5String = VarietyService.CalculateGenotypeFromDescription(description5);
var pg5 = RabbitGenotype.Parse(pg5String);
Console.WriteLine($"\nDescription: {description5}");
Console.WriteLine($"Genotype: {pg5}");

string g21Str = "aa,B_,cchd_,D_,E_"; // Black Chin (Same as Black Chinchilla but uses modifier)
var g21 = RabbitGenotype.Parse(g21Str);
string desc21 = VarietyService.IdentifyDescription(g21);
Console.WriteLine($"Genotype: {g21} -> Description (No Breed): {desc21}");

// Simulation test: Broken Chestnut Tiddywider
string simulationDesc = "Broken Chestnut Tiddywider";
string simulationGenotype = VarietyService.CalculateGenotypeFromDescription(simulationDesc);
Console.WriteLine($"\nSimulation: {simulationDesc}");
Console.WriteLine($"Calculated Genotype: {simulationGenotype}");

var parsedGenotype = RabbitGenotype.Parse(simulationGenotype);
string identifiedDesc = VarietyService.IdentifyDescription(parsedGenotype);
Console.WriteLine($"Identified Description: {identifiedDesc}");

// Rhinelander test
Console.WriteLine("\n--- Rhinelander Breed-Specific Varieties ---");
string rhinelanderBlack = "Black Rhinelander";
string rbGenotype = VarietyService.CalculateGenotypeFromDescription(rhinelanderBlack);
Console.WriteLine($"Description: {rhinelanderBlack} -> Genotype: {rbGenotype}");
Console.WriteLine($"Identified: {VarietyService.IdentifyDescription(RabbitGenotype.Parse(rbGenotype), "Rhinelander")}");

string rhinelanderBlue = "Blue Rhinelander";
string rblGenotype = VarietyService.CalculateGenotypeFromDescription(rhinelanderBlue);
Console.WriteLine($"Description: {rhinelanderBlue} -> Genotype: {rblGenotype}");
Console.WriteLine($"Identified: {VarietyService.IdentifyDescription(RabbitGenotype.Parse(rblGenotype), "Rhinelander")}");

string rhinelanderChocolate = "Chocolate Rhinelander";
string rchGenotype = VarietyService.CalculateGenotypeFromDescription(rhinelanderChocolate);
Console.WriteLine($"Description: {rhinelanderChocolate} -> Genotype: {rchGenotype}");
Console.WriteLine($"Identified: {VarietyService.IdentifyDescription(RabbitGenotype.Parse(rchGenotype), "Rhinelander")}");