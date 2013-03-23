namespace AngiesList.Redis
{
    using BookSleeve;

    internal static class BooksleeveExtensions
    {
        public static bool NeedsReset(this RedisConnectionBase connection)
        {
            return connection == null ||
                 (connection.State != RedisConnectionBase.ConnectionState.Open &&
                  connection.State != RedisConnectionBase.ConnectionState.Opening);
        }
    }
}
