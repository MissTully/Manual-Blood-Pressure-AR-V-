namespace Encountive.Domain
{
    /// <summary>Adult vs pediatric population. Drives which cuff-sizing
    /// branch family (B-1..B-5 vs B-6..B-9) the rules engine evaluates.</summary>
    public enum PopulationClass
    {
        Adult,
        Pediatric
    }

    /// <summary>Pediatric age banding per AAP 2017. Only meaningful when
    /// <see cref="PopulationClass.Pediatric"/>.</summary>
    public enum PediatricBand
    {
        None,
        Infant,
        Child,
        Adolescent,
        AdolescentAdultCrossover
    }

    /// <summary>AAMI cuff labeling classes (ANSI/AAMI/ISO 81060-2:2018).</summary>
    public enum CuffClass
    {
        None,
        SmallAdult,
        Adult,
        AdultLarge,
        AdultThigh,
        PediatricInfant,
        PediatricChild,
        PediatricAdolescent
    }

    /// <summary>Cuff Size Selection sub-scene stages S1..S5 (SDD §11.1).</summary>
    public enum CssStage
    {
        S1_IdentityConsent,
        S2_Landmarks,
        S3_Measurement,
        S4_CuffSelection,
        S5_Confirmation
    }

    /// <summary>Operating modes (SDD §2.3, §8.3).</summary>
    public enum TrainingMode
    {
        Guided,
        Practice,
        Evaluation,
        FullEncounter
    }

    /// <summary>The discrete learner action being attempted. The gate
    /// engine inspects the action plus the abstracted state to decide
    /// which deterministic gate(s) apply.</summary>
    public enum LearnerAction
    {
        AttemptPatientContact,
        AttemptMeasurement,
        AttemptCuffApplication,
        CommitCuffClass,
        CaptureRationale,
        AttemptAdvanceToStation3
    }

    /// <summary>Gate engine verdict (SDD §8.5).</summary>
    public enum GateResolutionKind
    {
        Pass,
        Fire,
        Escalate
    }
}
