namespace LogParserApp
{
    class Program
    {
        private const string InputOutputFolder = "InputOutput";
        private const string InputFileName = "unparsedlogs.txt";
        private const string OutputFileName = "parsedlogs.txt";
        static async Task Main()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Navigate up to the project directory from bin/Debug/net9.0
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", InputOutputFolder));

                string inputFilePath = Path.Combine(projectDir, InputFileName);
                string outputFilePath = Path.Combine(projectDir, OutputFileName);

                Console.WriteLine($"Info: Reading logs from {inputFilePath}");

                if (!File.Exists(inputFilePath))
                {
                    Console.WriteLine($"Error: Input file '{inputFilePath}' not found.");
                    WaitForExit();
                    return;
                }

                var logProcessor = new LogProcessor();
                await logProcessor.ProcessAndWriteToFileAsync(inputFilePath, outputFilePath);

                Console.WriteLine($"Info: Parsed logs written to {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Exception while processing log file: {ex.Message}");
            }

            WaitForExit();
        }

        private static void WaitForExit()
        {
            Console.WriteLine();
            Console.WriteLine("Press 'E' to exit...");

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
            } while (key.Key != ConsoleKey.E);
        }
    }
}

