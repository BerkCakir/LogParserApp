using LogParserApp;

namespace LogParserAppTests
{
    [TestClass]
    public class LogProcessorTests
    {
        private LogProcessor _logProcessor = new LogProcessor();

        [TestInitialize]
        public void Initialize()
        {
            _logProcessor = new LogProcessor();
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_SimpleChain_ReturnsCorrectOrder()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 1 0 [second message] 2",
                "1 0 0 [first message] 1",
                "1 2 0 [third message] -1"
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));

            var pipeline = result["1"];
            Assert.AreEqual(3, pipeline.Count);
            // Chain should be in reverse order (from end to start)
            Assert.AreEqual("third message", pipeline[0].DecodedBody);
            Assert.AreEqual("second message", pipeline[1].DecodedBody);
            Assert.AreEqual("first message", pipeline[2].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }


        [TestMethod]
        public async Task ProcessLogFileAsync_MissingLastMessage_ReturnsCorrectOrder()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 3 0 [third message] 4",
                "1 1 0 [first message] 2",
                "1 2 0 [second message] 3",
                "1 4 0 [fourth..."
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));

            var pipeline = result["1"];
            Assert.AreEqual(3, pipeline.Count);
            // Chain should be in reverse order (from end to start)
            Assert.AreEqual("third message", pipeline[0].DecodedBody);
            Assert.AreEqual("second message", pipeline[1].DecodedBody);
            Assert.AreEqual("first message", pipeline[2].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_MissingLastMessagesOfMultiplePipelines_FindsEndPointCorrectly()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 1 0 [pipeline1 second message] 2",
                "1 0 0 [pipeline1 first message] 1",
                "1 2 0 [pipeline1 third message] 3",
                "2 3 0 [pipeline2 third message] 4",
                "2 1 0 [pipeline2 first message] 2",
                "2 2 0 [pipeline2 second message] 3",
                "2 4 0 [pipeline2 fourth..."
            };
            File.WriteAllLines(tempFile, input);

            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));
            Assert.IsTrue(result.ContainsKey("2"));

            var pipeline1 = result["1"];
            Assert.AreEqual(3, pipeline1.Count);
            var pipeline2 = result["2"];
            Assert.AreEqual(3, pipeline2.Count);
            // Chain should start with the message that points to a missing ID
            Assert.AreEqual("pipeline1 third message", pipeline1[0].DecodedBody);
            Assert.AreEqual("pipeline1 second message", pipeline1[1].DecodedBody);
            Assert.AreEqual("pipeline1 first message", pipeline1[2].DecodedBody);
            Assert.AreEqual("pipeline2 third message", pipeline2[0].DecodedBody);
            Assert.AreEqual("pipeline2 second message", pipeline2[1].DecodedBody);
            Assert.AreEqual("pipeline2 first message", pipeline2[2].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_MultiplePipelines_GroupsCorrectly()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 0 0 [pipeline1 first message] 1",
                "1 1 0 [pipeline1 second message] -1",
                "2 0 0 [pipeline2 first message] 1",
                "2 1 0 [pipeline2 second message] -1"
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));
            Assert.IsTrue(result.ContainsKey("2"));

            // Check pipeline 1
            var pipeline1 = result["1"];
            Assert.AreEqual(2, pipeline1.Count);
            Assert.AreEqual("pipeline1 second message", pipeline1[0].DecodedBody);
            Assert.AreEqual("pipeline1 first message", pipeline1[1].DecodedBody);

            // Check pipeline 2
            var pipeline2 = result["2"];
            Assert.AreEqual(2, pipeline2.Count);
            Assert.AreEqual("pipeline2 second message", pipeline2[0].DecodedBody);
            Assert.AreEqual("pipeline2 first message", pipeline2[1].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }


        [TestMethod]
        public async Task ProcessLogFileAsync_WhenCircularDependencyExists_LastMessageCouldNotDetectedAndReturnsNull()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 1 0 [first message] 2",
                "1 2 0 [second message] 3",
                "1 3 0 [third message] 1",
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.IsFalse(result.Any());

            // Clean up
            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_IllFormattedInput_SkipsInvalidLines()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 0 0 [valid message] 1",
                "1 2 0 [invalid line.",
                "1 1 0 [second valid message] -1"
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));

            var pipeline = result["1"];
            Assert.AreEqual(2, pipeline.Count);
            Assert.AreEqual("second valid message", pipeline[0].DecodedBody);
            Assert.AreEqual("valid message", pipeline[1].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }


        [TestMethod]
        public async Task ProcessLogFileAsync_HexEncoding_DecodesCorrectly()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 0 1 [74657374696E672068657820636F6E76657273696F6E] -1"
            };
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));

            var pipeline = result["1"];
            Assert.AreEqual(1, pipeline.Count);
            Assert.AreEqual("testing hex conversion", pipeline[0].DecodedBody);

            // Clean up
            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessAndWriteToFileAsync_ValidInput_WritesCorrectOutput()
        {
            // Arrange
            string inputFile = Path.GetTempFileName();
            string outputFile = Path.GetTempFileName();
            var input = new string[]
            {
                "1 0 0 [first message] 1",
                "1 1 0 [second message] -1",
                "2 0 0 [another pipeline] -1"
            };
            File.WriteAllLines(inputFile, input);

            // Act
            await _logProcessor.ProcessAndWriteToFileAsync(inputFile, outputFile);

            // Assert
            string[] outputLines = File.ReadAllLines(outputFile);
            Assert.AreEqual(5, outputLines.Length);
            Assert.AreEqual("Pipeline 2", outputLines[0]);
            Assert.AreEqual("    0| another pipeline", outputLines[1]);
            Assert.AreEqual("Pipeline 1", outputLines[2]);
            Assert.AreEqual("    1| second message", outputLines[3]);
            Assert.AreEqual("    0| first message", outputLines[4]);

            // Clean up
            File.Delete(inputFile);
            File.Delete(outputFile);
        }

        [TestMethod]
        public async Task ProcessAndWriteToFileAsync_EmptyInput_HandlesWithoutError()
        {
            // Arrange
            string inputFile = Path.GetTempFileName();
            string outputFile = Path.GetTempFileName();
            File.WriteAllText(inputFile, string.Empty);

            // Act
            await _logProcessor.ProcessAndWriteToFileAsync(inputFile, outputFile);

            // Assert
            string[] outputLines = File.ReadAllLines(outputFile);
            Assert.AreEqual(0, outputLines.Length);

            // Clean up
            File.Delete(inputFile);
            File.Delete(outputFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_ComplexExample_ProcessesCorrectly()
        {
            // Arrange
            var input = new string[]
            {
                "legacy-hex 2 1 [4d6f726269206c6f626f72746973206d6178696d757320766976657272612e20416c697175616d2065742068656e647265726974206e756c6c61] -1",
                "2 37620c47-da9b-4218-9c35-fdb5961d4239 0 [nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.] -1",
                "1 0 0 [Lorem ipsum dolor sit amet, consectetur adipiscing elit] -1",
                "2 04e28d3b-d945-4051-8eeb-6f049f391234 0 [Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea] 5352ab80-7b0a-421f-8ab4-5c840ae882ee",
                "3 1 0 [sed do eiusmod tempor incididunt ut labore et dolore magna aliqua] -1",
                "2 5352ab80-7b0a-421f-8ab4-5c840ae882ee 0 [commodo consequat. duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat] 37620c47-da9b-4218-9c35-fdb5961d4239",
                "legacy-hex 1 1 [566976616d75732072757472756d2069642065726174206e6563207665686963756c612e20446f6e6563206672696e67696c6c61206c6163696e696120656c656966656e642e] 2"
            };
            string tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));
            Assert.IsTrue(result.ContainsKey("2"));
            Assert.IsTrue(result.ContainsKey("3"));
            Assert.IsTrue(result.ContainsKey("legacy-hex"));

            var pipeline1 = result["1"];
            Assert.AreEqual(1, pipeline1.Count);

            var pipeline2 = result["2"];
            Assert.AreEqual(3, pipeline2.Count);

            var pipeline3 = result["3"];
            Assert.AreEqual(1, pipeline3.Count);

            var pipeline4 = result["legacy-hex"];
            Assert.AreEqual(2, pipeline4.Count);

            // Clean up
            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_MissingMiddleMessage_CreatesSeparateChains()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                "2 C 0 [first message] D",
                "2 D 0 [second message] E",
                // Message E is missing
                "2 F 0 [fourth message] G",
                "2 G 0 [fifth message] -1"
            };

            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("2"));

            var pipeline = result["2"];
            // Should have two separate chains: G->F and D->C
            Assert.AreEqual(4, pipeline.Count);

            Assert.AreEqual("fifth message", pipeline[0].DecodedBody);
            Assert.AreEqual("G", pipeline[0].Id);
            Assert.AreEqual("fourth message", pipeline[1].DecodedBody);
            Assert.AreEqual("F", pipeline[1].Id);
            Assert.AreEqual("second message", pipeline[2].DecodedBody);
            Assert.AreEqual("D", pipeline[2].Id);
            Assert.AreEqual("first message", pipeline[3].DecodedBody);
            Assert.AreEqual("C", pipeline[3].Id);

            File.Delete(tempFile);
        }

        [TestMethod]
        public async Task ProcessLogFileAsync_MultipleChains_WithMissingMiddleMessages()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var input = new string[]
            {
                // First chain with missing middle message
                "1 A 0 [first chain, first message] B",
                "1 B 0 [first chain, second message] C",
                // Message C is missing
                "1 D 0 [first chain, fourth message] E",
                "1 E 0 [first chain, fifth message] -1",
                
                // Second complete chain
                "1 F 0 [second chain, first message] G",
                "1 G 0 [second chain, second message] -1",
                
                // Third chain with missing middle message
                "1 H 0 [third chain, first message] I",
                // Message I is missing
                "1 J 0 [third chain, third message] K",
                "1 K 0 [third chain, fourth message] -1"
            };

            File.WriteAllLines(tempFile, input);

            // Act
            var result = await _logProcessor.ProcessLogFileAsync(tempFile);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.ContainsKey("1"));

            var pipeline = result["1"];
            Assert.AreEqual(9, pipeline.Count);

            // Chains with next_id = -1 should come first, in descending order by ID
            // First chain: K->J
            Assert.AreEqual("third chain, fourth message", pipeline[0].DecodedBody);
            Assert.AreEqual("K", pipeline[0].Id);
            Assert.AreEqual("third chain, third message", pipeline[1].DecodedBody);
            Assert.AreEqual("J", pipeline[1].Id);

            // Second chain: G->F
            Assert.AreEqual("second chain, second message", pipeline[2].DecodedBody);
            Assert.AreEqual("G", pipeline[2].Id);
            Assert.AreEqual("second chain, first message", pipeline[3].DecodedBody);
            Assert.AreEqual("F", pipeline[3].Id);

            // Third chain: E->D
            Assert.AreEqual("first chain, fifth message", pipeline[4].DecodedBody);
            Assert.AreEqual("E", pipeline[4].Id);
            Assert.AreEqual("first chain, fourth message", pipeline[5].DecodedBody);
            Assert.AreEqual("D", pipeline[5].Id);

            // Fourth chain: H
            Assert.AreEqual("third chain, first message", pipeline[6].DecodedBody);
            Assert.AreEqual("H", pipeline[6].Id);

            // Fifth chain: B->A
            Assert.AreEqual("first chain, second message", pipeline[7].DecodedBody);
            Assert.AreEqual("B", pipeline[7].Id);
            Assert.AreEqual("first chain, first message", pipeline[8].DecodedBody);
            Assert.AreEqual("A", pipeline[8].Id);

            // Clean up
            File.Delete(tempFile);
        }
    }
}

