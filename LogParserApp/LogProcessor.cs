using System.Text;

namespace LogParserApp
{
    public class LogProcessor
    {
        public async Task ProcessAndWriteToFileAsync(string inputFilePath, string outputFilePath)
        {
            var pipelines = await ProcessLogFileAsync(inputFilePath);

            // Write the processed logs to the output file
            await using var writer = new StreamWriter(outputFilePath);
            foreach (var pipeline in pipelines)
            {
                await writer.WriteLineAsync($"Pipeline {pipeline.Key}");
                foreach (var message in pipeline.Value)
                {
                    await writer.WriteLineAsync($"    {message.Id}| {message.DecodedBody}");
                }
            }
        }

        public async Task<Dictionary<string, List<LogMessage>>> ProcessLogFileAsync(string inputFilePath)
        {
            var lines = await File.ReadAllLinesAsync(inputFilePath);

            var logMessages = ParseLogMessages(lines);

            return ProcessLogs(logMessages);
        }

        private Dictionary<string, List<LogMessage>> ProcessLogs(List<LogMessage> logMessages)
        {
            // Group messages by pipeline id
            var pipelineGroups = logMessages.GroupBy(m => m.PipelineId).OrderByDescending(m => m.Key).ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<string, List<LogMessage>>();

            foreach (var pipeline in pipelineGroups)
            {
                var pipelineId = pipeline.Key;
                var messages = pipeline.Value;

                try
                {
                    // Build chain by following next_id references
                    var chains = BuildChains(messages, pipelineId);
                    if (chains.Any())
                    {
                        // Flatten all chains into a single list
                        result[pipelineId] = chains.SelectMany(c => c).ToList();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: Exception while processing pipeline {pipelineId}: {ex.Message}");
                }
            }

            return result;
        }

        private List<List<LogMessage>> BuildChains(List<LogMessage> messages, string pipelineId)
        {
            // Create a dictionary of next_ids for a fast lookup
            var messagesByNextId = new Dictionary<string, LogMessage>();
            foreach (var msg in messages)
            {
                if (msg.NextId != "-1" && !messagesByNextId.ContainsKey(msg.NextId)) // duplicate next ids should not exits but it's better to check it in case of malformed files
                {
                    messagesByNextId[msg.NextId] = msg;
                }
            }

            // Find the last messages with next_id == -1, handling the case of multiple message chains
            var lastMessages = messages.Where(m => m.NextId == "-1").OrderByDescending(m => m.Id).ToList();

            // Find messages it's next_id not referencing an id from our id set, can be multiple if there is an missing or malformed message in between
            var otherPossibleLastMessages = messagesByNextId.Where(n => !messages.Select(m => m.Id).Contains(n.Key)).Select(n => n.Value).OrderByDescending(n => n.Id).ToList();

            var endPoints = new List<LogMessage>();
            if (lastMessages is not null && lastMessages.Any())
            {
                endPoints.AddRange(lastMessages);
            }

            if (otherPossibleLastMessages is not null && otherPossibleLastMessages.Any())
            {
                endPoints.AddRange(otherPossibleLastMessages);
            }

            // If still no last message found, possible circular dependency
            if (!endPoints.Any())
            {
                Console.WriteLine($"Error: No last message found as a starting point, operation terminated for pipeline:{pipelineId}");
                return new List<List<LogMessage>>();
            }

            // Build chains for each end point
            var allChains = new List<List<LogMessage>>();
            foreach (var endPoint in endPoints)
            {
                var chain = new List<LogMessage> { endPoint };
                string currentId = endPoint.Id;

                while (true)
                {
                    // Find a message that points to the current one in messagesByNextId list
                    if (messagesByNextId.TryGetValue(currentId, out var previousMessage))
                    {
                        chain.Add(previousMessage);
                        currentId = previousMessage.Id;
                    }
                    else
                    {   // when it's the end of current chain
                        break;
                    }
                }

                allChains.Add(chain);
            }

            return allChains;
        }

        private List<LogMessage> ParseLogMessages(string[] lines)
        {
            var logMessages = new List<LogMessage>();

            foreach (var line in lines)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Split into at most 5 parts
                    string[] parts = line.Trim().Split(' ', 5);

                    if (parts.Length < 5)
                    {
                        Console.WriteLine($"Warning: Skipping malformed log entry, not enough parts: {line}");
                        continue;
                    }

                    if (!ExtractMessageBody(line, parts, out string rawBody)) continue;

                    EncodingType encoding = ExtractEncoding(line, parts);

                    var message = new LogMessage
                    {
                        PipelineId = parts[0],
                        Id = parts[1],
                        Encoding = encoding,
                        RawBody = rawBody,
                        NextId = parts[4].Trim()
                    };

                    message.DecodedBody = DecodeMessageBody(message.RawBody, message.Encoding);

                    if (!logMessages.Any(l => l.PipelineId == message.PipelineId && l.Id == message.Id))
                    {   // do not insert duplicate message ids per pipeline
                        logMessages.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: Exception while parsing log entry '{line}': {ex.Message}");
                }
            }

            return logMessages;
        }

        private bool ExtractMessageBody(string line, string[] parts, out string rawBody)
        {
            // Extract the body part which is enclosed in square brackets
            string fullBodyPart = parts[3];
            rawBody = "";

            // Check if the body part has no spaces and extracted correctly
            if (fullBodyPart.StartsWith("[") && fullBodyPart.EndsWith("]"))
            {
                rawBody = fullBodyPart.TrimStart('[').TrimEnd(']');
            }
            else
            {
                // The body contains spaces, try to find the body in the full line
                int startIndex = line.IndexOf('[');
                int endIndex = line.LastIndexOf(']');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    rawBody = line.Substring(startIndex + 1, endIndex - startIndex - 1);

                    // Adjust parts array to get the next_id which comes after the body
                    string afterBody = line.Substring(endIndex + 1).Trim();
                    parts[4] = afterBody;
                }
                else
                {
                    Console.WriteLine($"Warning: Skipping malformed log entry, log body not found in {line}");
                    return false;
                }
            }

            return true;
        }

        private EncodingType ExtractEncoding(string line, string[] parts)
        {
            if (int.TryParse(parts[2], out int encodingValue) && Enum.IsDefined(typeof(EncodingType), encodingValue))
            {
                return (EncodingType)encodingValue;
            }

            Console.WriteLine($"Warning: Unknown encoding type in {line}");
            return EncodingType.Unknown;
        }

        private string DecodeMessageBody(string rawBody, EncodingType encoding)
        {
            return encoding switch
            {
                EncodingType.ASCII => rawBody,
                EncodingType.Hexadecimal => ConvertHexadecimalToString(rawBody),
                _ => rawBody
            };
        }

        private static string ConvertHexadecimalToString(string rawBody)
        {
            try
            {
                byte[] bytes = new byte[rawBody.Length / 2];
                for (int i = 0; i < rawBody.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(rawBody.Substring(i, 2), 16);
                }
                return Encoding.ASCII.GetString(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Hex message text could not be decoded '{rawBody}': {ex.Message}");
                return rawBody;
            }
        }
    }
}

