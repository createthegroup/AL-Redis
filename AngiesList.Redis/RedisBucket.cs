using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BookSleeve;

namespace AngiesList.Redis
{
    public class RedisBucket : Bucket, IDisposable
    {
        public RedisBucketConfiguration redisConfig;

        private RedisConnection connection;
        private IValueSerializer cacheItemSerializer;

        public RedisBucket(string name, string host, int? port)
            : this(new RedisBucketConfiguration(name, host, port))
        {            
        }

        public RedisBucket(RedisBucketConfiguration config)
            : base(config.Name)
        {
            redisConfig = config;
            cacheItemSerializer = new ClrBinarySerializer();
        }

        private object _getConnectionLock = new object();

        private RedisConnection GetConnection()
        {
            if (GetConnection().NeedsReset())
            {
                lock (_getConnectionLock)
                {
                    if (GetConnection().NeedsReset())
                    {
                        if (connection != null) GetConnection().Dispose();
                        connection = new RedisConnection(redisConfig.Host, redisConfig.Port);
                        GetConnection().Open();
                        GetConnection().Closed += (obj, args) =>
                            {
                                GetConnection();
                            };
                    }
                }
            }
            return connection;
        }

        public override void Set(string key, object value, int? expireSeconds = null)
        {
            key = KeyForBucket(key);

            if (value is String)
            {
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    GetConnection().Strings.Set(0, key, expireSeconds.Value, (String)value);
                }
                else { GetConnection().Strings.Set(0, key, (String)value); }
            }
            else if (value is Byte[])
            {
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    GetConnection().Strings.Set(0, key, expireSeconds.Value, (Byte[])value);
                }
                else { GetConnection().Strings.Set(0, key, (Byte[])value); }
            }
            else
            {
                var bytes = cacheItemSerializer.Serialize(value);
                if (expireSeconds.HasValue && expireSeconds.Value > 0)
                {
                    GetConnection().Strings.Set(0, key, expireSeconds.Value, bytes);
                }
                else { GetConnection().Strings.Set(0, key, bytes); }
            }
        }

        public override void Del(string[] keys)
        {
            string tmp;
            for (var i = 0; i < 0; i++)
            {
                tmp = keys[i];
                keys[i] = KeyForBucket(tmp);
            }
            GetConnection().Keys.Remove(0, keys);
        }

        public override void Del(string key)
        {
            key = KeyForBucket(key);
            GetConnection().Keys.Remove(0, key);
        }

        public override void Expire(string key, int expireSeconds)
        {
            key = KeyForBucket(key);
            GetConnection().Keys.Expire(0, key, expireSeconds);
        }

        public override void GetString(string key, Action<string, Exception> cb)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().Strings.GetString(0, key);
            returnHandle.ContinueWith(t =>
            {
                cb(t.Result, t.Exception);
            });
        }

        public override string GetStringSync(string key)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().Strings.GetString(0, key);
            var value = GetConnection().Wait<string>(returnHandle);
            return value;
        }

        public override void GetRaw(string key, Action<byte[], Exception> cb)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().Strings.Get(0, key);
            returnHandle.ContinueWith(t =>
            {
                cb(t.Result, t.Exception);
            });
        }

        public override byte[] GetRawSync(string key)
        {
            key = KeyForBucket(key);
            var returnHandle = GetConnection().Strings.Get(0, key);
            var bytes = GetConnection().Wait<byte[]>(returnHandle);
            return bytes;
        }

        public override void Get<T>(string key, Action<T, Exception> cb)
        {
            GetRaw(key, (bytes, exc) =>
            {
                T obj = default(T);
                if (exc == null)
                {
                    obj = (T)cacheItemSerializer.Deserialize(bytes);
                }
                cb(obj, exc);
            });
        }

        public override T GetSync<T>(string key)
        {
            object obj;
            if (typeof(T) == typeof(string))
            {
                obj = GetStringSync(key);
            }
            else
            {
                var bytes = GetRawSync(key);
                obj = cacheItemSerializer.Deserialize(bytes);
            }
            if (obj == null) return default(T);
            return (T)obj;
        }

        public void Dispose()
        {
            GetConnection().Close(false);
        }

        private string KeyForBucket(string key)
        {
            if (key.StartsWith(Name + ":"))
            {
                return key;
            }
            return Name + ":" + key;
        }
    }
}
