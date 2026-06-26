// A few view-model tests read and restore the process-wide AppSettings.Current singleton
// (colourblind palette, key rebinding). Running test collections in parallel could let one
// class observe another's in-flight mutation. The suite runs in well under a second, so we
// trade that parallelism for fully deterministic runs.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
