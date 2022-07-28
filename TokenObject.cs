namespace DCI.SystemEvents
{
    internal class TokenObject
    {
        public TokenObject() { }
        public TokenObject(string continuationToken, int totalCount)
        {
            ContinuationToken = continuationToken;
            TotalCount = totalCount;
        }
        public string ContinuationToken { get; set; }
        public int TotalCount { get; set; }
    }
}