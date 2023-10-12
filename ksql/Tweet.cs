namespace ksql
{
    using ksqlDB.RestApi.Client.KSql.Query;
    
    public class Tweet : Record
    {
        public int Id { get; set; }

        public string? Message { get; set; }
    }
}