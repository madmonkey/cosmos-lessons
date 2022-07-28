using System;
using Microsoft.Azure.Cosmos;

namespace DCI.SystemEvents.Settings
{
    public class ThrottlingHandlerSettings
    {
        private int randomizedMinThresholdInMilliseconds;
        private int randomizedMaxThresholdInMilliseconds;
        private int requestTimeoutInSeconds;
        private int maxIdleTimeoutMinutes;
        private int maxRequestsPerTcpConnection;
        private int openTcpConnectionTimeoutSec;
        private int maxTcpConnectionsPerEndpoint;
        private int portMode;
        public ThrottlingHandlerSettings()
        {
            ExponentialRetryInMilliseconds = 2000; // <--base retry number
            MaximumExponentialRetries = 5;
            RandomizedMinThresholdInMilliseconds = 250; // <-- min variance added to retry +/-
            RandomizedMaxThresholdInMilliseconds = 1250; // <-- max variance added to retry +/-
            RequestTimeoutInSeconds = 300;
            MaxIdleTimeoutMinutes = 10;
            /* The default settings allow 30 simultaneous requests per connection.
             Do not set this value lower than 4 requests per connection or higher than 50-100 requests per connection. 
            The former can lead to a large number of connections to be created. 
            The latter can lead to head of line blocking, high latency and timeouts.
            */
            MaxRequestsPerTcpConnection = 30; // the default
            OpenTcpConnectionTimeoutSec = 5; // the default
            MaxTcpConnectionsPerEndpoint = 65535; // the default
            PortMode = (int)PortReuseMode.ReuseUnicastPort; // the default
        }
        /// <summary>
        /// The value in milliseconds used to exponentially back-off from retry attempts
        /// </summary>
        public int ExponentialRetryInMilliseconds { get; set; }
        /// <summary>
        /// How many attempts to retry persisting
        /// </summary>
        public byte MaximumExponentialRetries { get; set; }
        /// <summary>
        /// Minimum value in milliseconds used to generate +/- offset
        /// </summary>
        public int RandomizedMinThresholdInMilliseconds
        {
            get => randomizedMinThresholdInMilliseconds;
            set
            {
                if (value > 0)
                {
                    randomizedMinThresholdInMilliseconds = value;
                }
            }
        }
        /// <summary>
        /// Maximum value in milliseconds used to generate +/- offset
        /// </summary>
        public int RandomizedMaxThresholdInMilliseconds
        {
            get => randomizedMaxThresholdInMilliseconds;
            set
            {
                if (value > 0)
                {
                    randomizedMaxThresholdInMilliseconds = value;
                }
            }
        }
        /// <summary>
        /// Tcp connection timeout (Cosmos)
        /// </summary>
        public int OpenTcpConnectionTimeoutSec
        {
            get => openTcpConnectionTimeoutSec;
            set
            {
                if (value > 0)
                {
                    openTcpConnectionTimeoutSec = value;
                }
            }
        }
        /// <summary>
        /// Maximum requests per open connection (Cosmos)
        /// </summary>
        public int MaxRequestsPerTcpConnection
        {
            get => maxRequestsPerTcpConnection;
            set
            {
                if (value >= 4 && value <= 100)
                {
                    maxRequestsPerTcpConnection = value;
                }
            }
        }
        /// <summary>
        /// Request timeout in seconds (Cosmos)
        /// </summary>
        public int RequestTimeoutInSeconds
        {
            get => requestTimeoutInSeconds;
            set
            {
                if (value > 0)
                {
                    requestTimeoutInSeconds = value;
                }
            }
        }
        /// <summary>
        /// Maximum idle open connection timeout in minutes (Cosmos)
        /// </summary>
        public int MaxIdleTimeoutMinutes
        {
            get => maxIdleTimeoutMinutes;
            set
            {
                if (value >= 10)
                {
                    maxIdleTimeoutMinutes = value;
                }
            }
        }
        /// <summary>
        /// Maximum connections allowed per endpoint (Cosmos)
        /// </summary>
        public int MaxTcpConnectionsPerEndpoint
        {
            get => maxTcpConnectionsPerEndpoint;
            set
            {
                if (value >= 16) // minimum recommended
                {
                    maxTcpConnectionsPerEndpoint = value;
                }
            }
        }
        /// <summary>
        /// How the underlying socket is handled at the O/S level (Cosmos)
        /// </summary>
        public int PortMode
        {
            get => portMode;
            set
            {
                if (Enum.IsDefined(typeof(PortReuseMode), value))
                {
                    portMode = value;
                }
            }
        }
        /// <summary>
        /// How the underlying socket is handled at the O/S level (Cosmos)
        /// </summary>
        public PortReuseMode PortReuseMode => (PortReuseMode)PortMode;
        
        public string ToDebugStatement()
        {
            return $"ThrottleSettings initialized with settings:" +
                   $"ExponentialRetryInMilliseconds: {ExponentialRetryInMilliseconds} {Environment.NewLine}" +
                   $"MaximumExponentialRetries: {MaximumExponentialRetries} {Environment.NewLine}" +
                   $"RandomizedMinThresholdInMilliseconds: {RandomizedMinThresholdInMilliseconds} {Environment.NewLine}" +
                   $"RandomizedMaxThresholdInMilliseconds: {RandomizedMaxThresholdInMilliseconds} {Environment.NewLine}" +
                   $"RequestTimeoutInSeconds: {RequestTimeoutInSeconds} {Environment.NewLine}" +
                   $"MaxIdleTimeoutMinutes: {MaxIdleTimeoutMinutes} {Environment.NewLine}" +
                   $"MaxRequestsPerTcpConnection: {MaxRequestsPerTcpConnection} {Environment.NewLine}" +
                   $"OpenTcpConnectionTimeoutSec: {OpenTcpConnectionTimeoutSec} {Environment.NewLine}" +
                   $"MaxTcpConnectionsPerEndpoint: {MaxTcpConnectionsPerEndpoint} {Environment.NewLine}" +
                   $"PortReuseMode: {PortReuseMode} {Environment.NewLine}";
        }
    }
}