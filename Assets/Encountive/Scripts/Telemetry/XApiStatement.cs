using System.Collections.Generic;

namespace Encountive.Telemetry
{
    /// <summary>
    /// xAPI 1.0.3 statement, structurally complete to the
    /// AR BP Cuff Trainer xAPI profile v1 (companion spec §2). Mirrors
    /// the on-wire shape so a statement is persisted verbatim to the
    /// LRS without lossy mapping.
    /// </summary>
    public sealed class XApiStatement
    {
        public string Id { get; set; }                 // UUID v4
        public XApiActor Actor { get; set; }
        public XApiVerb Verb { get; set; }
        public XApiActivity Object { get; set; }
        public XApiResult Result { get; set; }
        public XApiContext Context { get; set; } = new XApiContext();
        public string Timestamp { get; set; }          // ISO-8601 UTC ms
        public string Version { get; set; } = "1.0.3";
    }

    public sealed class XApiActor
    {
        public string ObjectType { get; set; } = "Agent";
        public string Name { get; set; }               // pseudonymous display
        public XApiAccount Account { get; set; }
        // mbox / openid intentionally absent (profile §3 / §10.3).
    }

    public sealed class XApiAccount
    {
        public string HomePage { get; set; }
        public string Name { get; set; }               // institutional learner id
    }

    public sealed class XApiVerb
    {
        public string Id { get; set; }
        public IDictionary<string, string> Display { get; set; }
            = new Dictionary<string, string>();
    }

    public sealed class XApiActivity
    {
        public string ObjectType { get; set; } = "Activity";
        public string Id { get; set; }
        public XApiActivityDefinition Definition { get; set; }
            = new XApiActivityDefinition();
    }

    public sealed class XApiActivityDefinition
    {
        public string Type { get; set; }               // activity-type IRI
        public IDictionary<string, string> Name { get; set; }
            = new Dictionary<string, string>();
        public IDictionary<string, string> Description { get; set; }
        public IDictionary<string, object> Extensions { get; set; }
    }

    public sealed class XApiResult
    {
        public bool? Success { get; set; }
        public bool? Completion { get; set; }
        public XApiScore Score { get; set; }
        public string Duration { get; set; }           // ISO-8601 e.g. "PT12.4S"
        public string Response { get; set; }
        public IDictionary<string, object> Extensions { get; set; }
    }

    public sealed class XApiScore
    {
        public double? Scaled { get; set; }            // [-1, 1]
        public double? Raw { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
    }

    public sealed class XApiContext
    {
        public string Registration { get; set; }       // session UUID v4
        public XApiContextActivities ContextActivities { get; set; }
            = new XApiContextActivities();
        public string Platform { get; set; }
        public string Language { get; set; }
        public IDictionary<string, object> Extensions { get; set; }
            = new Dictionary<string, object>();
    }

    public sealed class XApiContextActivities
    {
        public List<XApiActivityRef> Parent { get; set; }
        public List<XApiActivityRef> Grouping { get; set; }
        public List<XApiActivityRef> Category { get; set; }
    }

    public sealed class XApiActivityRef
    {
        public string Id { get; set; }
        public XApiActivityRef() { }
        public XApiActivityRef(string id) { Id = id; }
    }
}
