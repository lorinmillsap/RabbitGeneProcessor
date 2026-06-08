using System.CommandLine;
using RabbitGeneProcessor.Core;

namespace RabbitGeneProcessor;

class Program
{
    static async Task<int> Main(string[] args)
    {
        InitializeServices();

        var rootCommand = new RootCommand("Rabbit Gene Processor CLI");

        // Command: calculate
        var calculateCommand = new Command("calculate", "Calculates a genotype from a variety/breed description.");
        var descriptionArg = new Argument<string>("description", "The rabbit description (e.g., 'Broken VM Chestnut Rex').");
        calculateCommand.AddArgument(descriptionArg);
        calculateCommand.SetHandler((description) =>
        {
            var genotype = VarietyService.CalculateGenotypeFromDescription(description);
            Console.WriteLine($"Description: {description}");
            Console.WriteLine($"Calculated Genotype: {genotype}");
        }, descriptionArg);

        // Command: identify
        var identifyCommand = new Command("identify", "Identifies a variety description from a genotype string.");
        var genotypeArg = new Argument<string>("genotype", "The genetic string (e.g., 'aa,B_,C_,D_,E_').");
        var breedOption = new Option<string?>("--breed", "Optional breed name to refine identification.");
        identifyCommand.AddArgument(genotypeArg);
        identifyCommand.AddOption(breedOption);
        identifyCommand.SetHandler((genotypeStr, breed) =>
        {
            var genotype = RabbitGenotype.Parse(genotypeStr);
            var description = VarietyService.IdentifyDescription(genotype, breed);
            Console.WriteLine($"Genotype: {genotype}");
            if (!string.IsNullOrEmpty(breed)) Console.WriteLine($"Breed: {breed}");
            Console.WriteLine($"Identified Description: {description}");
        }, genotypeArg, breedOption);

        // Command: solve-offspring
        var solveOffspringCommand = new Command("solve-offspring", "Resolves unknown alleles in an offspring based on parents.");
        var targetOption = new Option<string>("--target", "The target offspring genotype string.") { IsRequired = true };
        var p1Option = new Option<string>("--p1", "Parent 1 genotype string.") { IsRequired = true };
        var p2Option = new Option<string>("--p2", "Parent 2 genotype string.") { IsRequired = true };
        solveOffspringCommand.AddOption(targetOption);
        solveOffspringCommand.AddOption(p1Option);
        solveOffspringCommand.AddOption(p2Option);
        solveOffspringCommand.SetHandler((target, p1, p2) =>
        {
            var targetG = RabbitGenotype.Parse(target);
            var p1G = RabbitGenotype.Parse(p1);
            var p2G = RabbitGenotype.Parse(p2);

            var solved = GenotypeSolver.Solve(targetG, p1G, p2G);
            Console.WriteLine($"Parent 1: {p1G}");
            Console.WriteLine($"Parent 2: {p2G}");
            Console.WriteLine($"Original Target: {targetG}");
            Console.WriteLine($"Solved Target: {solved}");
        }, targetOption, p1Option, p2Option);

        // Command: solve-parents
        var solveParentsCommand = new Command("solve-parents", "Resolves unknown alleles in parents based on offspring.");
        var sp1Option = new Option<string>("--p1", "Parent 1 genotype string.") { IsRequired = true };
        var sp2Option = new Option<string>("--p2", "Parent 2 genotype string.") { IsRequired = true };
        var offspringOption = new Option<string[]>("--offspring", "One or more offspring genotype strings.") { IsRequired = true, Arity = ArgumentArity.OneOrMore };
        solveParentsCommand.AddOption(sp1Option);
        solveParentsCommand.AddOption(sp2Option);
        solveParentsCommand.AddOption(offspringOption);
        solveParentsCommand.SetHandler((p1, p2, offspring) =>
        {
            var p1G = RabbitGenotype.Parse(p1);
            var p2G = RabbitGenotype.Parse(p2);
            var offspringGs = offspring.Select(RabbitGenotype.Parse).ToArray();

            var (solvedP1, solvedP2) = GenotypeSolver.SolveParents(p1G, p2G, offspringGs);
            Console.WriteLine($"Original Parent 1: {p1G}");
            Console.WriteLine($"Original Parent 2: {p2G}");
            Console.WriteLine("Offspring Evidence:");
            foreach (var o in offspringGs) Console.WriteLine($" - {o}");
            Console.WriteLine($"Solved Parent 1: {solvedP1}");
            Console.WriteLine($"Solved Parent 2: {solvedP2}");
        }, sp1Option, sp2Option, offspringOption);

        // Command: predict
        var predictCommand = new Command("predict", "Predicts possible offspring outcomes from pairing two genotypes.");
        var pp1Option = new Option<string>("--p1", "Parent 1 genotype string.") { IsRequired = true };
        var pp2Option = new Option<string>("--p2", "Parent 2 genotype string.") { IsRequired = true };
        var limitOption = new Option<int>("--limit", () => 100, "Maximum number of outcomes to return.");
        predictCommand.AddOption(pp1Option);
        predictCommand.AddOption(pp2Option);
        predictCommand.AddOption(limitOption);
        predictCommand.SetHandler((p1Input, p2Input, limit) =>
        {
            var (breed1, p1GStr) = VarietyService.ExtractBreedAndGenotype(p1Input);
            var (breed2, p2GStr) = VarietyService.ExtractBreedAndGenotype(p2Input);

            var p1G = RabbitGenotype.Parse(p1GStr);
            var p2G = RabbitGenotype.Parse(p2GStr);

            // Set PrimaryBreed if found
            if (breed1 != null) p1G.PrimaryBreed = breed1.Name;
            if (breed2 != null) p2G.PrimaryBreed = breed2.Name;

            // Also include breed-specific loci in parents if needed
            if (breed1 != null)
            {
                var breedG = RabbitGenotype.Parse(breed1.GenotypeString);
                foreach (var l in breedG.Loci)
                {
                    if (!p1G.Loci.Any(pl => pl.GetLocusSymbol() == l.GetLocusSymbol()))
                    {
                        p1G.Loci.Add(l);
                    }
                }
            }
            if (breed2 != null)
            {
                var breedG = RabbitGenotype.Parse(breed2.GenotypeString);
                foreach (var l in breedG.Loci)
                {
                    if (!p2G.Loci.Any(pl => pl.GetLocusSymbol() == l.GetLocusSymbol()))
                    {
                        p2G.Loci.Add(l);
                    }
                }
            }

            // Use the first parent's breed as the primary breed for identification, if available.
            var primaryBreed = breed1?.Name ?? breed2?.Name;

            Func<RabbitGenotype, string> identifyFunc = g => VarietyService.IdentifyDescription(g, primaryBreed);
            var predictions = GenotypeSolver.PredictOffspring(p1G, p2G, limit, identifyFunc);
            
            Console.WriteLine($"Parent 1: {(breed1 != null ? breed1.Name + " " : "")}{p1G}");
            Console.WriteLine($"Parent 2: {(breed2 != null ? breed2.Name + " " : "")}{p2G}");
            if (!string.IsNullOrEmpty(primaryBreed)) Console.WriteLine($"Primary Breed: {primaryBreed}");

            // Group predictions by category
            var activeLoci = predictions.SelectMany(p => p.Genotype.Loci).Select(l => l.GetLocusSymbol()).Distinct().ToList();
            var categoryNames = activeLoci
                .Select(s => GeneticParser.Definitions.FirstOrDefault(d => d.Symbol == s))
                .Where(d => d != null)
                .Select(d => d!.Category)
                .Distinct()
                .OrderBy(c => c == "Color" ? 0 : 1)
                .ThenBy(c => c)
                .ToList();

            foreach (var category in categoryNames)
            {
                if (category == "Color")
                {
                    Console.WriteLine($"\nPredicted Color Outcomes (Top {limit}):");
                    foreach (var p in predictions.Take(limit))
                    {
                        Console.WriteLine($"{p.Probability,6:P2} - {p.Genotype} ({identifyFunc(p.Genotype)})");
                    }
                }
                else
                {
                    Console.WriteLine($"\nPredicted {category} Outcomes:");
                    var catPredictions = predictions
                        .GroupBy(p => {
                            var catLoci = p.Genotype.Loci.Where(l => l.GetDefinition()?.Category == category).ToList();
                            return string.Join(",", catLoci.Select(l => l.ToString()));
                        })
                        .Select(g => {
                            var prob = g.Sum(x => x.Probability);
                            var sampleGenotype = g.First().Genotype;
                            var catLoci = sampleGenotype.Loci.Where(l => l.GetDefinition()?.Category == category).ToList();
                            
                            var descriptions = new List<string>();
                            foreach(var l in catLoci)
                            {
                                var def = l.GetDefinition();
                                if (def == null) continue;
                                var norm = l.Normalize();

                                if (def.Symbol == "Dw")
                                {
                                    if (norm.First.Symbol == "Dw" && norm.Second.Symbol == "Dw") descriptions.Add("Peanut (Lethal)");
                                    else if (norm.First.Symbol == "Dw") descriptions.Add("Dwarf");
                                    else descriptions.Add("Normal Size");
                                }
                                else
                                {
                                    var alleleDef = def.Alleles.FirstOrDefault(a => a.Symbol == norm.First.Symbol);
                                    if (alleleDef != null)
                                    {
                                        descriptions.Add(alleleDef.Name);
                                    }
                                }
                            }
                            var desc = descriptions.Count > 0 ? string.Join(", ", descriptions) : "Normal";
                            return new { Genotype = string.Join(",", catLoci), Description = desc, Probability = prob };
                        })
                        .OrderByDescending(x => x.Probability)
                        .ToList();

                    foreach (var cp in catPredictions)
                    {
                        Console.WriteLine($"{cp.Probability,6:P2} - {cp.Genotype} ({cp.Description})");
                    }
                }
            }
        }, pp1Option, pp2Option, limitOption);

        rootCommand.AddCommand(calculateCommand);
        rootCommand.AddCommand(identifyCommand);
        rootCommand.AddCommand(solveOffspringCommand);
        rootCommand.AddCommand(solveParentsCommand);
        rootCommand.AddCommand(predictCommand);

        // Default: Run existing demonstration if no args provided
        if (args.Length == 0)
        {
            RunDemonstration();
            return 0;
        }

        return await rootCommand.InvokeAsync(args);
    }

    private static void InitializeServices()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        GeneticParser.Initialize(Path.Combine(baseDir, "Data", "LociDefinitions.json"));
        VarietyService.Initialize(
            Path.Combine(baseDir, "Data", "Varieties.json"),
            Path.Combine(baseDir, "Data", "Modifiers.json"),
            Path.Combine(baseDir, "Data", "Breeds.json"));
    }

    private static void RunDemonstration()
    {
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

        // Demonstrate new CalculateGenotypeFromDescription method
        Console.WriteLine("\n--- CalculateGenotypeFromDescription ---");
        string descWithBreed = "Broken VM Chestnut Rex";
        string genotypeFromDesc1 = VarietyService.CalculateGenotypeFromDescription(descWithBreed);
        Console.WriteLine($"Description: {descWithBreed} -> Genotype: {genotypeFromDesc1}");

        // Demonstrate converting genotype back to variety description
        Console.WriteLine("\n--- Genotype to Description ---");
        string g1Str = "aa,bb,C_,dd,E_,En_,Vv,rr"; // Broken VM Lilac Rex
        var g1 = RabbitGenotype.Parse(g1Str);
        string desc1 = VarietyService.IdentifyDescription(g1, "Rex");
        Console.WriteLine($"Genotype: {g1} -> Description (Breed: Rex): {desc1}");

        // Rhinelander test
        Console.WriteLine("\n--- Rhinelander Breed-Specific Varieties ---");
        string rhinelanderBlack = "Black Rhinelander";
        string rbGenotype = VarietyService.CalculateGenotypeFromDescription(rhinelanderBlack);
        Console.WriteLine($"Description: {rhinelanderBlack} -> Genotype: {rbGenotype}");
        Console.WriteLine($"Identified: {VarietyService.IdentifyDescription(RabbitGenotype.Parse(rbGenotype), "Rhinelander")}");

        // Parental Resolution / Solving test
        Console.WriteLine("\n--- Parental Genotype Resolution (Solver) ---");
        var p1G = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Black"));
        var p2G = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Chestnut"));
        var targetG = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Chestnut"));

        var solvedG = GenotypeSolver.Solve(targetG, p1G, p2G);
        Console.WriteLine($"Parent 1 (Black): {p1G}");
        Console.WriteLine($"Parent 2 (Chestnut): {p2G}");
        Console.WriteLine($"Target (Chestnut): {targetG}");
        Console.WriteLine($"Solved Target: {solvedG}");

        // Reverse Solving
        Console.WriteLine("\n--- Reverse Genetic Solving (Parents from Offspring) ---");
        var p1Source = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Chestnut"));
        var p2Source = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Chestnut"));
        var child1 = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Black"));
        var child2 = RabbitGenotype.Parse(VarietyService.CalculateGenotypeFromDescription("Blue"));

        var (solvedP1, solvedP2) = GenotypeSolver.SolveParents(p1Source, p2Source, new[] { child1, child2 });
        Console.WriteLine($"Initial Parent 1: {p1Source}");
        Console.WriteLine($"Initial Parent 2: {p2Source}");
        Console.WriteLine($"Offspring: {child1}, {child2}");
        Console.WriteLine($"Solved Parent 1: {solvedP1}");
        Console.WriteLine($"Solved Parent 2: {solvedP2}");

        // Caret test
        Console.WriteLine("\n--- Caret (^) Optionality Test ---");
        string withCaret = "A^t_";
        string withoutCaret = "at_";
        var gWith = RabbitGenotype.Parse(withCaret);
        var gWithout = RabbitGenotype.Parse(withoutCaret);
        Console.WriteLine($"'{withCaret}' parses to: {gWith}");
        Console.WriteLine($"'{withoutCaret}' parses to: {gWithout}");
        Console.WriteLine($"Match: {gWith.ToString() == gWithout.ToString()}");

        Console.WriteLine("\nUse command line arguments for specific tasks. Try '--help' for more information.");
    }
}