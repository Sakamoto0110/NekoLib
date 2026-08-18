namespace NekoLib.Pipes
{
    /// <summary>
    /// Framework-generated error codes returned in unsuccessful RPC responses.
    /// Application handlers may return additional codes.
    /// </summary>
    public static class PipeErrorCodes
    {
        public const string NotFound = "not_found";
        public const string Exception = "exception";
        public const string ResponseTooLarge = "response_too_large";
        public const string ConnectionClosed = "connection_closed";
    }
}
