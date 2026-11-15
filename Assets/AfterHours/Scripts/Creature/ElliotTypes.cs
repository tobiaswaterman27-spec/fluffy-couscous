namespace AfterHours.Creature
{
    /// <summary>How Elliot is moving (blended smoothly by the animator).</summary>
    public enum ElliotGait
    {
        Still,      // completely motionless - one of the scariest things it does
        Idle,       // breathing, tiny sways
        SlowWalk,   // wandering
        Walk,
        FastWalk,   // investigating
        Run,        // hunting
        Crawl       // on all fours (vents, low spaces)
    }

    /// <summary>One-off actions (timed poses layered on top of the gait).</summary>
    public enum ElliotAction
    {
        None,
        LookAround,
        ReachHandle,     // open a door / cupboard / locker
        BangDoor,
        InspectLow,      // peer under / behind / into a hiding place
        PullOpen,        // yank a locker / cupboard door open
        Grab,            // catch
        Scream,
        Stare
    }
}
