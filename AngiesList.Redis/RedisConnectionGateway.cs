using System;
using System.Net.Sockets;
using BookSleeve;
using AngiesList.Redis;

namespace Redis
{
    public sealed class RedisConnectionGateway
    {
        private const string RedisConnectionFailed = "Redis connection failed.";
        private RedisConnection _connection;
        private static volatile RedisConnectionGateway _instance;
        private static RedisSessionStateConfiguration redisConfig;

        private static object syncLock = new object();
        private static object syncConnectionLock = new object();

        public static RedisConnectionGateway Current
        {
            get
            {
                if (_instance == null)
                {
                    lock (syncLock)
                    {
                        if (_instance == null)
                        {
                            redisConfig = RedisSessionStateConfiguration.GetConfiguration();
                            _instance = new RedisConnectionGateway(redisConfig.Host, redisConfig.Port);
                        }
                    }
                }

                return _instance;
            }
        }

        private RedisConnectionGateway(string host, int port)
        {
            _connection = getNewConnection(host, port);
        }

        private static RedisConnection getNewConnection(string host, int port)
        {
            return new RedisConnection(host, port, syncTimeout: 5000, ioTimeout: 5000);
        }

        public RedisConnection GetConnection()
        {
            lock (syncConnectionLock)
            {
                redisConfig = RedisSessionStateConfiguration.GetConfiguration();
                if (_connection == null)
                    _connection = getNewConnection(redisConfig.Host, redisConfig.Port);

                if (_connection.State == RedisConnectionBase.ConnectionState.Opening)
                    return _connection;

                if (_connection.State == RedisConnectionBase.ConnectionState.Closing || _connection.State == RedisConnectionBase.ConnectionState.Closed)
                {
                    try
                    {
                        _connection = getNewConnection(redisConfig.Host, redisConfig.Port);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(RedisConnectionFailed, ex);
                    }
                }

                if (_connection.State == RedisConnectionBase.ConnectionState.Shiny)
                {
                    try
                    {
                        var openAsync = _connection.Open();
                        _connection.Wait(openAsync);
                    }
                    catch (SocketException ex)
                    {
                        throw new Exception(RedisConnectionFailed, ex);
                    }
                }

                return _connection;
            }
        }
    }
}
