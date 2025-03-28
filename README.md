
## LOG PARSER APPLICATION

### OVERVIEW
This is a cross-platform .NET console application built with .NET 9.0. <br />
The application expects log entries in the following format:<br />
<em>pipeline_id id encoding [body] next_id </em>

### USAGE
1. Place your log file named "unparsedlogs.txt" in the InputOutput folder within the project directory
2. Run the application
3. The parsed and reconstructed logs will be written to "parsedlogs.txt" in the same InputOutput folder

### IMPLEMENTATION NOTES
Performance Optimization: <br /> 
    For better performance, the application processes messages in reverse order, starting with the end of each chain and working backwards. 
    This eliminates an additional reverse operation that would otherwise be needed.

### CONSTRAINTS AND LIMITATIONS (ASSUMPTIONS)
#### 1. Message Integrity: 
    The application can handle cases where log messages in the middle of a chain are missing or corrupted. 
    In such cases, the application will create multiple separate chains for the same pipeline. For example, in the following scenario:
    
    1 A 0 [first message] B
    # Message B is missing
    1 C 0 [third message] D
    1 D 0 [fourth message] -1
    
    The application will create two separate chains:
    - Chain 1: D - C
    - Chain 2: A
    
    Both chains will be displayed in the output file without any separator between them.
    
    Message Ordering:
        In the output file, chains are ordered as follows:
        - The chain ending with next_id = -1 will be displayed first
        - Any unlinked chains (those with missing connections) will follow
        - Within each chain, messages are ordered from the end of the chain to the beginning
        - For unlinked chains, message chains will be displayed in descending order based on the message ID of their last message.

#### 2. Multiple Chains Per Pipeline:
    The application can handle multiple independent chains within the same pipeline.
    This includes scenarios where multiple messages have next_id = -1. For example:
    
    1 A 0 [first chain, first message] B
    1 B 0 [first chain, second message] -1
    1 C 0 [second chain, first message] D
    1 D 0 [second chain, second message] -1
    
    In this case, the application will process both chains and display them in the output file without any separator between them.
    Message chains will be displayed in descending order based on the message ID of their last message.

    - Chain 1: D - C
    - Chain 2: B - A

#### 3. No Circular Dependencies: 
    The log messages in a pipeline should not have circular dependencies. A circular dependency would look like:
    
    1 A 0 [first message] B
    1 B 0 [second message] C
    1 C 0 [third message] A  # Points back to A, creating a circular reference
    
    This pipeline will be ignored with an error message on the console.

#### 4. Multiple Messages Pointing to the Same Next ID:
    Multiple messages should not point to the same message with their next_id for the same pipeline. 
    If such a case occurs, only the first message will be considered as pointing to the next message. For example:
    
    1 A 0 [first message] C
    1 B 0 [second message] C  # Both A and B point to C
    1 C 0 [third message] -1
    
    In this case, only message A will be considered as pointing to C, and message B will be ignored.

#### 5. Unknown Encoding Format:
    Messages with unsupported encoding types (not 0 or 1) will be processed as ASCII without decoding.    

#### 6. Duplicate Message IDs:
    If duplicate message IDs are encountered for the same pipeline, the application will use the first occurrence 
    and ignore subsequent ones.

#### 7. Ill-formatted Log Entries:
    Malformed log entries will be skipped and won't appear in the output file.
    Warning messages will be displayed in the console for these entries.
    
## PROJECT STRUCTURE
Program.cs:      Entry point and console UI <br /> 
LogProcessor.cs: Core logic for processing log files <br /> 
LogMessage.cs:   Data model for log messages <br /> 
EncodingType.cs: Enum for supported encoding <br />

Unit Tests: LogProcessorTests.cs, contains unit tests for the LogProcessor class <br /> 

## ERROR HANDLING
The application handles various error conditions: <br /> 
- Missing input files <br /> 
- Ill-formatted log entries <br /> 
- Circular dependencies <br /> 
- Invalid encoding types <br /> 
- Duplicate message IDs  <br />

## POSSIBLE IMPROVEMENTS
Output Format for Multiple Chains: <br /> 
    Currently, when a pipeline has multiple chains, they are displayed in the output file without any separator between them. 
    This can make it difficult to identify where one chain ends and another begins. 
    A possible improvement would be to add a visual separator between different chains within the same pipeline to improve readability. 

## ACKNOWLEDGMENTS
Parts of this readme file and unit test cases were enriched with assistance from AI tools, which helped organize and formalize my ideas and implementation approach.
