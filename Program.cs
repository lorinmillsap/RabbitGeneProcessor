using RabbitGeneProcessor.Core;

GeneticParser.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "LociDefinitions.json"));

Console.WriteLine("Rabbit Gene Processor Initialized.");