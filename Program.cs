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
        var targetArg = new Argument<string>("target", "The target offspring genotype string.");
        var p1Arg = new Argument<string>("parent1", "Parent 1 genotype string.");
        var p2Arg = new Argument<string>("parent2", "Parent 2 genotype string.");
        solveOffspringCommand.AddArgument(targetArg);
        solveOffspringCommand.AddArgument(p1Arg);
        solveOffspringCommand.AddArgument(p2Arg);
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
        }, targetArg, p1Arg, p2Arg);

        // Command: solve-parents
        var solveParentsCommand = new Command("solve-parents", "Resolves unknown alleles in parents based on offspring.");
        var sp1Arg = new Argument<string>("parent1", "Parent 1 genotype string.");
        var sp2Arg = new Argument<string>("parent2", "Parent 2 genotype string.");
        var offspringArg = new Argument<string[]>("offspring", "One or more offspring genotype strings.");
        solveParentsCommand.AddArgument(sp1Arg);
        solveParentsCommand.AddArgument(sp2Arg);
        solveParentsCommand.AddArgument(offspringArg);
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
        }, sp1Arg, sp2Arg, offspringArg);

        rootCommand.AddCommand(calculateCommand);
        rootCommand.AddCommand(identifyCommand);
        rootCommand.AddCommand(solveOffspringCommand);
        rootCommand.AddCommand(solveParentsCommand);

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

        Console.WriteLine("\nUse command line arguments for specific tasks. Try '--help' for more information.");
    }
}