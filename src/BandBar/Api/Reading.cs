namespace BandBar.Api
{
    internal enum LinkStatus
    {
        Connecting,
        Connected,
        NoService,
        AccessDenied,
        NotInstalled,
        ApiUntrusted,
        ApiIncompatible,
    }

    /// <summary>One snapshot published by <see cref="NlMonitor"/> to the UI. Immutable.</summary>
    internal sealed class Reading
    {
        public static readonly Reading Connecting = new Reading(LinkStatus.Connecting);

        public Reading(LinkStatus status, uint down = 0, uint up = 0, string zoneName = null,
                       string serviceVersion = null, string detail = null)
        {
            Status = status;
            Down = down;
            Up = up;
            ZoneName = zoneName;
            ServiceVersion = serviceVersion;
            Detail = detail;
        }

        public LinkStatus Status { get; }

        /// <summary>Bytes per second.</summary>
        public uint Down { get; }

        /// <summary>Bytes per second.</summary>
        public uint Up { get; }

        public string ZoneName { get; }
        public string ServiceVersion { get; }

        /// <summary>Exception type name behind a failure; never a message (messages can contain paths).</summary>
        public string Detail { get; }
    }

    /// <summary>Rate of the selected zone, bytes per second.</summary>
    internal readonly struct Rate
    {
        public Rate(uint down, uint up, bool zoneFound)
        {
            Down = down;
            Up = up;
            ZoneFound = zoneFound;
        }

        public uint Down { get; }
        public uint Up { get; }
        public bool ZoneFound { get; }
    }

    /// <summary>
    /// A live connection to the local NetLimiter service. Declared without NetLimiter types so that code holding
    /// one can be compiled by the JIT even when the API cannot be loaded.
    /// </summary>
    internal interface INlSession : System.IDisposable
    {
        string ZoneName { get; }
        string ServiceVersion { get; }
        Rate Read();
    }
}
