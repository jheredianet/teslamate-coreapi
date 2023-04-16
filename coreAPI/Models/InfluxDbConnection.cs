namespace coreAPI.Models
{
    public class InfluxDbConnection
    {
        public string Url { get; }
        public string Token { get; }
        public string Bucket { get; }
        public string Organization { get; }

        public InfluxDbConnection(string url, string token, string bucket, string organization)
        {
            Url = url;
            Token = token;
            Bucket = bucket;
            Organization = organization;
        }
    }

}

