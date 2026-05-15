using System.Collections.Generic;

namespace Encountive.Telemetry
{
    /// <summary>
    /// xAPI 1.0.3 statement (SDD §5.2, sample in SDD Appendix D).
    /// Mirrors the statement object shape exactly so it can be persisted
    /// verbatim to xr_xapi_statements without lossy mapping (SDD §5.3).
    /// Append-only; <see cref="Id"/> is the idempotency key.
    /// </summary>
    public sealed class XApiStatement
    {
        public string Id { get; set; }
        public XApiActor Actor { get; set; }
        public XApiVerb Verb { get; set; }
        public XApiObject Object { get; set; }
        public XApiContext Context { get; set; } = new XApiContext();
        public XApiResult Result { get; set; }

        /// <summary>ISO-8601 UTC; assigned from the injected clock.</summary>
        public string Timestamp { get; set; }
        public string Version { get; set; } = "1.0.3";
    }

    public sealed class XApiActor
    {
        public string ObjectType { get; set; } = "Agent";
        public XApiAccount Account { get; set; }
    }

    public sealed class XApiAccount
    {
        public string HomePage { get; set; } = "https://encountive.com";
        public string Name { get; set; } // learner UUID
    }

    public sealed class XApiVerb
    {
        public string Id { get; set; }
        public IDictionary<string, string> Display { get; set; }
            = new Dictionary<string, string>();
    }

    public sealed class XApiObject
    {
        public string Id { get; set; }
        public IDictionary<string, string> Name { get; set; }
            = new Dictionary<string, string>();
    }

    public sealed class XApiContext
    {
        /// <summary>Keyed by the full extension IRI under
        /// https://xapi.encountive.com/extensions/ (SDD §5.2).</summary>
        public IDictionary<string, object> Extensions { get; set; }
            = new Dictionary<string, object>();
    }

    public sealed class XApiResult
    {
        public bool? Completion { get; set; }
        public bool? Success { get; set; }
        public double? ScoreScaled { get; set; }
    }
}
