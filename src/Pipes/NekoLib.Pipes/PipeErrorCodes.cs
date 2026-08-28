namespace NekoLib.Pipes
{
    /// <summary>
    /// Framework-generated error codes returned in unsuccessful RPC responses.
    /// Application handlers may return additional codes.
    /// </summary>
    public static class PipeErrorCodes
    {
        /// <summary>The requested operation has no mapped handler.</summary>
        public const string NotFound = "not_found";

        /// <summary>The mapped handler failed; exception details remain local to the server.</summary>
        public const string Exception = "exception";

        /// <summary>The handler response exceeded the configured RPC frame limit.</summary>
        public const string ResponseTooLarge = "response_too_large";

        /// <summary>The peer closed cleanly before a response frame began.</summary>
        public const string ConnectionClosed = "connection_closed";
    }
}
