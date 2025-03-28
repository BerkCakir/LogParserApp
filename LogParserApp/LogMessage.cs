namespace LogParserApp
{
    public class LogMessage
    {
        public string PipelineId { get; init; } = "";
        public string Id { get; init; } = "";
        public EncodingType Encoding { get; init; } = EncodingType.Unknown;
        public string RawBody { get; init; } = "";
        public string DecodedBody { get; set; } = "";
        public string NextId { get; init; } = "";
    }
}

